using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Abstractions.Services;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Functions.Extensions;
using MemberPropertyAlert.Functions.Models;
using MemberPropertyAlert.Functions.Infrastructure.Telemetry;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace MemberPropertyAlert.Functions.Functions;

public sealed class InstitutionFunctions
{
    private readonly IInstitutionService _institutionService;
    private readonly IMemberAddressRepository _memberAddressRepository;
    private readonly ILogger<InstitutionFunctions> _logger;
    private readonly IAuditLogger _auditLogger;

    public InstitutionFunctions(
        IInstitutionService institutionService,
        IMemberAddressRepository memberAddressRepository,
        IAuditLogger auditLogger,
        ILogger<InstitutionFunctions> logger)
    {
        _institutionService = institutionService;
        _memberAddressRepository = memberAddressRepository;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    [Function("GetInstitutions")]
    [OpenApiOperation("GetInstitutions", tags: new[] { "Institutions" })]
    [OpenApiParameter("pageNumber", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page number (1-based)." )]
    [OpenApiParameter("pageSize", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page size (max 200)." )]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedResponse<InstitutionResponse>))]
    public async Task<HttpResponseData> GetInstitutions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "institutions")] HttpRequestData req,
        FunctionContext context)
    {
        var tenantContext = context.GetTenantRequestContext();
        if (tenantContext is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Authentication is required.");
        }

        var query = FunctionHttpHelpers.ParseQuery(req);
        var pageNumber = FunctionHttpHelpers.GetPositiveInt(query, "pageNumber", 1);
        var pageSize = Math.Clamp(FunctionHttpHelpers.GetPositiveInt(query, "pageSize", 50), 1, 200);

        if (!tenantContext.IsPlatformAdmin)
        {
            var institutionId = tenantContext.InstitutionId ?? tenantContext.TenantId;
            if (string.IsNullOrWhiteSpace(institutionId))
            {
                return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, "Tenant is not associated with an institution.");
            }

            var institutionResult = await _institutionService.GetAsync(institutionId);
            if (institutionResult.IsFailure)
            {
                return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, institutionResult.Error ?? "Failed to retrieve institution.");
            }

            if (institutionResult.Value is null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var payload = new PagedResponse<InstitutionResponse>(
                new[] { MapInstitutionDetail(institutionResult.Value) },
                1,
                1,
                1);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteJsonAsync(payload);
            return response;
        }

        var result = await _institutionService.ListAsync(pageNumber, pageSize);
        var adminPayload = new PagedResponse<InstitutionResponse>(
            result.Items.Select(MapInstitutionSummary).ToList(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize);

        var adminResponse = req.CreateResponse(HttpStatusCode.OK);
        await adminResponse.WriteJsonAsync(adminPayload);
        return adminResponse;
    }

    [Function("GetInstitutionById")]
    [OpenApiOperation("GetInstitutionById", tags: new[] { "Institutions" })]
    [OpenApiParameter("id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Institution identifier.")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(InstitutionResponse))]
    [OpenApiResponseWithoutBody(HttpStatusCode.NotFound, Description = "Institution not found.")]
    public async Task<HttpResponseData> GetInstitutionById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "institutions/{id}")] HttpRequestData req,
        string id,
        FunctionContext context)
    {
        var tenantContext = context.GetTenantRequestContext();
        if (tenantContext is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Authentication is required.");
        }

        var result = await _institutionService.GetAsync(id);
        if (result.IsFailure)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, result.Error);
        }

        if (result.Value is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var institution = result.Value;
        if (!tenantContext.IsPlatformAdmin && !string.Equals(institution.TenantId, tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, "Not authorized to access this institution.");
        }

        var responsePayload = MapInstitutionDetail(institution);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteJsonAsync(responsePayload);
        return response;
    }

    [Function("CreateInstitution")]
    [OpenApiOperation("CreateInstitution", tags: new[] { "Institutions" })]
    [OpenApiRequestBody("application/json", typeof(CreateInstitutionRequest), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.Created, "application/json", typeof(InstitutionResponse))]
    public async Task<HttpResponseData> CreateInstitution(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "institutions")] HttpRequestData req,
        FunctionContext context)
    {
        var tenantContext = context.GetTenantRequestContext();
        if (tenantContext is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Authentication is required.");
        }

        if (!tenantContext.IsPlatformAdmin)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, "Only platform administrators can create institutions.");
        }

    var payload = await MemberPropertyAlert.Functions.Extensions.HttpRequestDataExtensions.ReadJsonBodyAsync<CreateInstitutionRequest>(req);
        if (payload is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(payload.TenantId))
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "TenantId is required.");
        }

        var result = await _institutionService.CreateAsync(payload.TenantId, payload.Name, payload.TimeZoneId, payload.PrimaryContactEmail);
        if (result.IsFailure || result.Value is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, result.Error ?? "Failed to create institution.");
        }

        var response = req.CreateResponse(HttpStatusCode.Created);
        var createdInstitution = result.Value;
        await response.WriteJsonAsync(MapInstitutionDetail(createdInstitution));
        response.Headers.Add("Location", $"/institutions/{result.Value.Id}");

        await _auditLogger.TrackEventAsync("InstitutionCreated", new Dictionary<string, string?>
        {
            ["institutionId"] = createdInstitution.Id,
            ["targetTenantId"] = createdInstitution.TenantId
        });

        return response;
    }

    [Function("UpdateInstitution")]
    [OpenApiOperation("UpdateInstitution", tags: new[] { "Institutions" })]
    [OpenApiParameter("id", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiRequestBody("application/json", typeof(UpdateInstitutionRequest), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(InstitutionResponse))]
    public async Task<HttpResponseData> UpdateInstitution(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "institutions/{id}")] HttpRequestData req,
        string id,
        FunctionContext context)
    {
        var tenantContext = context.GetTenantRequestContext();
        if (tenantContext is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Authentication is required.");
        }

        var payload = await MemberPropertyAlert.Functions.Extensions.HttpRequestDataExtensions.ReadJsonBodyAsync<UpdateInstitutionRequest>(req);
        if (payload is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Request body is required.");
        }

        var existingResult = await _institutionService.GetAsync(id);
        if (existingResult.IsFailure)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, existingResult.Error ?? "Unable to retrieve institution.");
        }

        var existing = existingResult.Value;
        if (existing is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        if (!tenantContext.IsPlatformAdmin && !string.Equals(existing.TenantId, tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, "Not authorized to modify this institution.");
        }

        var updateResult = await _institutionService.UpdateAsync(id, payload.Name, payload.PrimaryContactEmail, payload.Status);
        if (updateResult.IsFailure)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, updateResult.Error ?? "Failed to update institution.");
        }

        var refreshed = await _institutionService.GetAsync(id);
        if (refreshed.IsFailure || refreshed.Value is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, refreshed.Error ?? "Unable to retrieve updated institution.");
        }

        await _auditLogger.TrackEventAsync("InstitutionUpdated", new Dictionary<string, string?>
        {
            ["institutionId"] = id,
            ["targetTenantId"] = refreshed.Value.TenantId
        });

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteJsonAsync(MapInstitutionDetail(refreshed.Value));
        return response;
    }

    [Function("DeleteInstitution")]
    [OpenApiOperation("DeleteInstitution", tags: new[] { "Institutions" })]
    [OpenApiParameter("id", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiResponseWithoutBody(HttpStatusCode.NoContent)]
    public async Task<HttpResponseData> DeleteInstitution(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "institutions/{id}")] HttpRequestData req,
        string id,
        FunctionContext context)
    {
        var tenantContext = context.GetTenantRequestContext();
        if (tenantContext is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Authentication is required.");
        }

        var existingResult = await _institutionService.GetAsync(id);
        if (existingResult.IsFailure)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, existingResult.Error ?? "Unable to retrieve institution.");
        }

        var existing = existingResult.Value;
        if (existing is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        if (!tenantContext.IsPlatformAdmin && !string.Equals(existing.TenantId, tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, "Not authorized to delete this institution.");
        }

        var result = await _institutionService.DeleteAsync(id);
        if (result.IsFailure)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, result.Error ?? "Failed to delete institution.");
        }

        await _auditLogger.TrackEventAsync("InstitutionDeleted", new Dictionary<string, string?>
        {
            ["institutionId"] = id,
            ["targetTenantId"] = existing.TenantId
        });

        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    [Function("ListInstitutionAddresses")]
    [OpenApiOperation("ListInstitutionAddresses", tags: new[] { "Institution Addresses" })]
    [OpenApiParameter("institutionId", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiParameter("pageNumber", In = ParameterLocation.Query, Required = false, Type = typeof(int))]
    [OpenApiParameter("pageSize", In = ParameterLocation.Query, Required = false, Type = typeof(int))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedResponse<MemberAddressResponse>))]
    public async Task<HttpResponseData> ListAddresses(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "institutions/{institutionId}/addresses")] HttpRequestData req,
        string institutionId,
        FunctionContext context)
    {
        var tenantContext = context.GetTenantRequestContext();
        if (tenantContext is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Authentication is required.");
        }

        if (!tenantContext.IsPlatformAdmin &&
            !string.IsNullOrWhiteSpace(tenantContext.InstitutionId) &&
            !string.Equals(tenantContext.InstitutionId, institutionId, StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, "Not authorized to access these addresses.");
        }

        var query = FunctionHttpHelpers.ParseQuery(req);
        var pageNumber = FunctionHttpHelpers.GetPositiveInt(query, "pageNumber", 1);
        var pageSize = Math.Clamp(FunctionHttpHelpers.GetPositiveInt(query, "pageSize", 50), 1, 200);

        var result = await _memberAddressRepository.ListByInstitutionAsync(institutionId, pageNumber, pageSize);
        if (result.Items.Count == 0 && pageNumber > 1)
        {
            return req.CreateResponse(HttpStatusCode.NoContent);
        }

        var payload = new PagedResponse<MemberAddressResponse>(
            result.Items.Select(MapMemberAddress).ToList(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteJsonAsync(payload);
        return response;
    }

    [Function("CreateInstitutionAddress")]
    [OpenApiOperation("CreateInstitutionAddress", tags: new[] { "Institution Addresses" })]
    [OpenApiParameter("institutionId", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiRequestBody("application/json", typeof(MemberAddressRequest), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.Created, "application/json", typeof(MemberAddressResponse))]
    public async Task<HttpResponseData> CreateAddress(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "institutions/{institutionId}/addresses")] HttpRequestData req,
        string institutionId,
        FunctionContext context)
    {
        var tenantContext = context.GetTenantRequestContext();
        if (tenantContext is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Authentication is required.");
        }

        if (!tenantContext.IsPlatformAdmin &&
            !string.IsNullOrWhiteSpace(tenantContext.InstitutionId) &&
            !string.Equals(tenantContext.InstitutionId, institutionId, StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, "Not authorized to manage addresses for this institution.");
        }

        var payload = await MemberPropertyAlert.Functions.Extensions.HttpRequestDataExtensions.ReadJsonBodyAsync<MemberAddressRequest>(req);
        if (payload is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Request body is required.");
        }

        Address address;
        try
        {
            address = CreateAddressValueObject(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid address payload for institution {InstitutionId}", institutionId);
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, ex.Message);
        }

        var result = await _institutionService.AddAddressAsync(institutionId, address, payload.Tags, default);
        if (result.IsFailure || result.Value is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, result.Error ?? "Failed to create address.");
        }

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteJsonAsync(MapMemberAddress(result.Value));

        await _auditLogger.TrackEventAsync("InstitutionAddressCreated", new Dictionary<string, string?>
        {
            ["institutionId"] = institutionId,
            ["addressId"] = result.Value.Id
        });

        return response;
    }

    [Function("DeleteInstitutionAddress")]
    [OpenApiOperation("DeleteInstitutionAddress", tags: new[] { "Institution Addresses" })]
    [OpenApiParameter("institutionId", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiParameter("addressId", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiResponseWithoutBody(HttpStatusCode.NoContent)]
    public async Task<HttpResponseData> DeleteAddress(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "institutions/{institutionId}/addresses/{addressId}")] HttpRequestData req,
        string institutionId,
        string addressId,
        FunctionContext context)
    {
        var tenantContext = context.GetTenantRequestContext();
        if (tenantContext is null)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Authentication is required.");
        }

        if (!tenantContext.IsPlatformAdmin &&
            !string.IsNullOrWhiteSpace(tenantContext.InstitutionId) &&
            !string.Equals(tenantContext.InstitutionId, institutionId, StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, "Not authorized to manage addresses for this institution.");
        }

        var result = await _institutionService.RemoveAddressAsync(institutionId, addressId);
        if (result.IsFailure)
        {
            return await FunctionHttpHelpers.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, result.Error ?? "Failed to delete address.");
        }

        await _auditLogger.TrackEventAsync("InstitutionAddressDeleted", new Dictionary<string, string?>
        {
            ["institutionId"] = institutionId,
            ["addressId"] = addressId
        });

        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    private static InstitutionResponse MapInstitutionSummary(Institution institution)
    {
        return new InstitutionResponse(
            institution.Id,
            institution.TenantId,
            institution.Name,
            institution.Status,
            institution.TimeZoneId,
            institution.PrimaryContactEmail,
            institution.CreatedAtUtc,
            institution.UpdatedAtUtc,
            institution.Addresses.Count,
            null);
    }

    private static InstitutionResponse MapInstitutionDetail(Institution institution)
    {
        var addresses = institution.Addresses.Select(MapMemberAddress).ToList();
        return new InstitutionResponse(
            institution.Id,
            institution.TenantId,
            institution.Name,
            institution.Status,
            institution.TimeZoneId,
            institution.PrimaryContactEmail,
            institution.CreatedAtUtc,
            institution.UpdatedAtUtc,
            addresses.Count,
            addresses);
    }

    private static MemberAddressResponse MapMemberAddress(MemberAddress address)
    {
        return new MemberAddressResponse(
            address.Id,
            address.InstitutionId,
            address.IsActive,
            address.CreatedAtUtc,
            address.UpdatedAtUtc,
            address.LastMatchedAtUtc,
            address.LastMatchedListingId,
            address.Tags.ToArray(),
            AddressResponse.FromValueObject(address.Address));
    }

    private static Address CreateAddressValueObject(MemberAddressRequest request)
    {
        GeoCoordinate? coordinate = null;
        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            coordinate = GeoCoordinate.Create(request.Latitude.Value, request.Longitude.Value);
        }

        return Address.Create(
            request.Line1,
            request.Line2,
            request.City,
            request.StateOrProvince,
            request.PostalCode,
            request.CountryCode,
            coordinate);
    }
}
