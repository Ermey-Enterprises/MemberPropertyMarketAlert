using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Functions.Functions;
using MemberPropertyAlert.Functions.Security;
using Microsoft.Azure.Functions.Worker;
using NSubstitute;

namespace MemberPropertyAlert.Functions.Tests;

public class ListingMatchFunctionsTests
{
    [Fact]
    public async Task GetTenantMatches_ReturnsMatches_ForTenantUser()
    {
        var tenantContext = CreateTenantContext(isPlatformAdmin: false, tenantId: "tenant-a", institutionId: "institution-a");
        var functionContext = Substitute.For<FunctionContext>();
        functionContext.Items.Returns(new Dictionary<object, object?> { [nameof(TenantRequestContext)] = tenantContext });

        var request = new TestHttpRequestData(functionContext, new Uri("https://localhost/tenants/tenant-a/matches"));

        var listingMatch = CreateListingMatch();
        var fakeService = new FakeListingMatchService
        {
            ResultToReturn = new PagedResult<ListingMatch>(new[] { listingMatch }, totalCount: 1, pageNumber: 1, pageSize: 50)
        };

        var functions = new ListingMatchFunctions(fakeService);

        var response = await functions.GetTenantMatches(request, "tenant-a", functionContext, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("institution-a", fakeService.LastInstitutionId);
        Assert.Equal(1, fakeService.LastPageNumber);
        Assert.Equal(50, fakeService.LastPageSize);

        using var payload = JsonDocument.Parse(((TestHttpResponseData)response).BodyAsString);
        Assert.Equal(1, payload.RootElement.GetProperty("totalCount").GetInt32());
        var firstMatch = payload.RootElement.GetProperty("items").EnumerateArray().First();
        Assert.Equal("address-1", firstMatch.GetProperty("matchedAddressIds").EnumerateArray().First().GetString());
    }

    [Fact]
    public async Task GetTenantMatches_ReturnsForbidden_WhenTenantDoesNotMatch()
    {
        var tenantContext = CreateTenantContext(isPlatformAdmin: false, tenantId: "tenant-a", institutionId: "institution-a");
        var functionContext = Substitute.For<FunctionContext>();
        functionContext.Items.Returns(new Dictionary<object, object?> { [nameof(TenantRequestContext)] = tenantContext });

        var request = new TestHttpRequestData(functionContext, new Uri("https://localhost/tenants/tenant-b/matches"));
        var fakeService = new FakeListingMatchService();
        var functions = new ListingMatchFunctions(fakeService);

        var response = await functions.GetTenantMatches(request, "tenant-b", functionContext, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(fakeService.LastInstitutionId);
    }

    [Fact]
    public async Task GetMatches_AllowsPlatformAdminWithoutFilters()
    {
        var tenantContext = CreateTenantContext(isPlatformAdmin: true, tenantId: "platform", institutionId: null);
        var functionContext = Substitute.For<FunctionContext>();
        functionContext.Items.Returns(new Dictionary<object, object?> { [nameof(TenantRequestContext)] = tenantContext });

        var request = new TestHttpRequestData(functionContext, new Uri("https://localhost/matches"));
        var fakeService = new FakeListingMatchService
        {
            ResultToReturn = new PagedResult<ListingMatch>(Array.Empty<ListingMatch>(), totalCount: 0, pageNumber: 1, pageSize: 50)
        };
        var functions = new ListingMatchFunctions(fakeService);

        var response = await functions.GetMatches(request, functionContext, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(fakeService.LastInstitutionId);
        Assert.Equal(1, fakeService.LastPageNumber);
        Assert.Equal(50, fakeService.LastPageSize);
    }

    [Fact]
    public async Task GetMatches_ClampsPageSizeToMaximum()
    {
        var tenantContext = CreateTenantContext(isPlatformAdmin: true, tenantId: "platform", institutionId: null);
        var functionContext = Substitute.For<FunctionContext>();
        functionContext.Items.Returns(new Dictionary<object, object?> { [nameof(TenantRequestContext)] = tenantContext });

        var request = new TestHttpRequestData(functionContext, new Uri("https://localhost/matches?pageNumber=3&pageSize=500"));
        var fakeService = new FakeListingMatchService
        {
            ResultToReturn = new PagedResult<ListingMatch>(Array.Empty<ListingMatch>(), totalCount: 0, pageNumber: 3, pageSize: 200)
        };
        var functions = new ListingMatchFunctions(fakeService);

        var response = await functions.GetMatches(request, functionContext, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, fakeService.LastPageNumber);
        Assert.Equal(200, fakeService.LastPageSize);
    }

    [Fact]
    public async Task GetMatches_ReturnsForbidden_WhenTenantOverridesFilters()
    {
        var tenantContext = CreateTenantContext(isPlatformAdmin: false, tenantId: "tenant-a", institutionId: "institution-a");
        var functionContext = Substitute.For<FunctionContext>();
        functionContext.Items.Returns(new Dictionary<object, object?> { [nameof(TenantRequestContext)] = tenantContext });

        var request = new TestHttpRequestData(functionContext, new Uri("https://localhost/matches?tenantId=tenant-b"));
        var fakeService = new FakeListingMatchService();
        var functions = new ListingMatchFunctions(fakeService);

        var response = await functions.GetMatches(request, functionContext, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(fakeService.LastInstitutionId);
    }

    private static TenantRequestContext CreateTenantContext(bool isPlatformAdmin, string tenantId, string? institutionId)
    {
        return new TenantRequestContext(
            principal: null!,
            TenantId: tenantId,
            InstitutionId: institutionId,
            IsPlatformAdmin: isPlatformAdmin,
            ObjectId: "object",
            PreferredUsername: "user",
            CorrelationId: Guid.NewGuid().ToString(),
            Roles: Array.Empty<string>());
    }

    private static ListingMatch CreateListingMatch()
    {
        var address = Address.Create("123 Main St", null, "City", "ST", "12345", "US");
        return ListingMatch.Rehydrate(
            Guid.NewGuid().ToString("N"),
            "listing-1",
            address,
            1500,
            new Uri("https://example.com/listing/1"),
            AlertSeverity.Warning,
            new[] { "address-1" },
            DateTimeOffset.UtcNow,
            "region",
            new Dictionary<string, object> { ["key"] = "value" },
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeListingMatchService : MemberPropertyAlert.Core.Abstractions.Services.IListingMatchService
    {
        public string? LastInstitutionId { get; private set; }
        public int LastPageNumber { get; private set; }
        public int LastPageSize { get; private set; }
        public PagedResult<ListingMatch>? ResultToReturn { get; set; }

        public Task<Result<IReadOnlyCollection<ListingMatch>>> FindMatchesAsync(string stateOrProvince, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyCollection<ListingMatch>>.Failure("Not implemented"));

        public Task<Result> PublishMatchesAsync(IReadOnlyCollection<ListingMatch> matches, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure("Not implemented"));

        public Task<PagedResult<ListingMatch>> GetRecentMatchesAsync(string? institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            LastInstitutionId = institutionId;
            LastPageNumber = pageNumber;
            LastPageSize = pageSize;
            return Task.FromResult(ResultToReturn ?? new PagedResult<ListingMatch>(Array.Empty<ListingMatch>(), 0, pageNumber, pageSize));
        }
    }
}
