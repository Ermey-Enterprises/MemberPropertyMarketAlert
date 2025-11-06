using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Services;
using MemberPropertyAlert.Functions.Extensions;
using MemberPropertyAlert.Functions.Models;
using MemberPropertyAlert.Functions.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;

namespace MemberPropertyAlert.Functions.Functions;

public sealed class ListingMatchFunctions
{
    private readonly IListingMatchService _listingMatchService;
    public ListingMatchFunctions(IListingMatchService listingMatchService)
    {
        _listingMatchService = listingMatchService;
    }

    [Function("GetTenantMatches")]
    [OpenApiOperation("GetTenantMatches", tags: new[] { "ListingMatches" })]
    [OpenApiParameter("tenantId", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Tenant identifier.")]
    [OpenApiParameter("institutionId", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Optional institution identifier to scope matches.")]
    [OpenApiParameter("pageNumber", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page number (1-based)." )]
    [OpenApiParameter("pageSize", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page size (max 200)." )]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedResponse<ListingMatchResponse>))]
    public async Task<HttpResponseData> GetTenantMatches(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId}/matches")] HttpRequestData req,
        string tenantId,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var tenantContext = context.GetTenantRequestContext();
        if (tenantContext is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Authentication is required.");
        }

        var query = FunctionHttpHelpers.ParseQuery(req);
        var (pageNumber, pageSize) = ResolvePagination(query);

        if (!tenantContext.IsPlatformAdmin && !string.Equals(tenantContext.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, "Not authorized to access matches for this tenant.");
        }

        var requestedInstitutionId = query.TryGetValue("institutionId", out var institutionIdQuery)
            ? institutionIdQuery
            : null;

        var effectiveInstitutionId = ResolveInstitutionId(tenantContext, tenantId, requestedInstitutionId, allowAdminOverride: tenantContext.IsPlatformAdmin);
        if (!tenantContext.IsPlatformAdmin && requestedInstitutionId is not null && !string.Equals(requestedInstitutionId, effectiveInstitutionId, StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, "Not authorized to access matches for this institution.");
        }

        var result = await _listingMatchService.GetRecentMatchesAsync(effectiveInstitutionId, pageNumber, pageSize, cancellationToken);
        var responsePayload = new PagedResponse<ListingMatchResponse>(
            result.Items.Select(ListingMatchResponse.FromDomain).ToArray(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteJsonAsync(responsePayload);
        return response;
    }

    [Function("GetMatches")]
    [OpenApiOperation("GetMatches", tags: new[] { "ListingMatches" })]
    [OpenApiParameter("tenantId", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Optional tenant identifier (platform admins only).")]
    [OpenApiParameter("institutionId", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Optional institution identifier (platform admins only).")]
    [OpenApiParameter("pageNumber", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page number (1-based)." )]
    [OpenApiParameter("pageSize", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page size (max 200)." )]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedResponse<ListingMatchResponse>))]
    public async Task<HttpResponseData> GetMatches(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "matches")] HttpRequestData req,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var tenantContext = context.GetTenantRequestContext();
        if (tenantContext is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Authentication is required.");
        }

        var query = FunctionHttpHelpers.ParseQuery(req);
        var (pageNumber, pageSize) = ResolvePagination(query);

        string? requestedInstitutionId = null;
        if (query.TryGetValue("institutionId", out var institutionIdQuery))
        {
            requestedInstitutionId = institutionIdQuery;
        }

        string? tenantFilter = null;
        if (query.TryGetValue("tenantId", out var tenantIdQuery))
        {
            tenantFilter = tenantIdQuery;
        }

        string? effectiveInstitutionId;
        if (tenantContext.IsPlatformAdmin)
        {
            effectiveInstitutionId = ResolveInstitutionIdForAdmin(tenantFilter, requestedInstitutionId);
        }
        else
        {
            effectiveInstitutionId = ResolveInstitutionId(tenantContext, tenantContext.TenantId, requestedInstitutionId, allowAdminOverride: false);
            if ((requestedInstitutionId is not null && !string.Equals(requestedInstitutionId, effectiveInstitutionId, StringComparison.OrdinalIgnoreCase))
                || (tenantFilter is not null && !string.Equals(tenantFilter, tenantContext.TenantId, StringComparison.OrdinalIgnoreCase)))
            {
                return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, "Not authorized to access matches for this tenant or institution.");
            }
        }

        var result = await _listingMatchService.GetRecentMatchesAsync(effectiveInstitutionId, pageNumber, pageSize, cancellationToken);
        var responsePayload = new PagedResponse<ListingMatchResponse>(
            result.Items.Select(ListingMatchResponse.FromDomain).ToArray(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteJsonAsync(responsePayload);
        return response;
    }

    private static (int PageNumber, int PageSize) ResolvePagination(IReadOnlyDictionary<string, string> query)
    {
        var pageNumber = FunctionHttpHelpers.GetPositiveInt(query, "pageNumber", 1);
        var pageSize = FunctionHttpHelpers.GetPositiveInt(query, "pageSize", 50);
        pageSize = Math.Clamp(pageSize, 1, 200);
        return (pageNumber, pageSize);
    }

    private static string? ResolveInstitutionId(TenantRequestContext tenantContext, string tenantId, string? requestedInstitutionId, bool allowAdminOverride)
    {
        if (allowAdminOverride)
        {
            if (!string.IsNullOrWhiteSpace(requestedInstitutionId))
            {
                return requestedInstitutionId;
            }

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                return tenantId;
            }

            return null;
        }

        var contextInstitutionId = tenantContext.InstitutionId;
        if (!string.IsNullOrWhiteSpace(contextInstitutionId))
        {
            return contextInstitutionId;
        }

        return tenantContext.TenantId;
    }

    private static string? ResolveInstitutionIdForAdmin(string? tenantFilter, string? requestedInstitutionId)
    {
        if (!string.IsNullOrWhiteSpace(requestedInstitutionId))
        {
            return requestedInstitutionId;
        }

        if (!string.IsNullOrWhiteSpace(tenantFilter))
        {
            return tenantFilter;
        }

        return null;
    }
}
