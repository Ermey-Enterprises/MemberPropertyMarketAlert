using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using MemberPropertyAlert.Core.Abstractions.Messaging;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Functions.Functions;
using MemberPropertyAlert.Functions.Infrastructure.Storage;
using MemberPropertyAlert.Functions.Models;
using MemberPropertyAlert.Functions.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace MemberPropertyAlert.Functions.Tests;

public class MemberAddressImportFunctionsTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProcessMemberAddressImportAsync_FailsForMalformedCsv()
    {
        var csv = "tenantId,institutionId,addressLine1,city,stateOrProvince,postalCode,countryCode,tags\n" +
                  "tenant-1,inst-1,123 Main,,WA,98101,US,primary";

        var repository = new FakeMemberAddressRepository();
        var resolver = new FakePayloadResolver(csv);
        var statusPublisher = new FakeStatusPublisher();
        var accessor = new TenantRequestContextAccessor();
        var sut = new MemberAddressImportFunctions(repository, resolver, accessor, statusPublisher, NullLogger<MemberAddressImportFunctions>.Instance);

        var message = new MemberAddressImportMessage(
            "tenant-1",
            "inst-1",
            "import.csv",
            null,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)),
            null,
            null);

        var payload = JsonSerializer.Serialize(message, SerializerOptions);

        await Assert.ThrowsAsync<InvalidDataException>(() => sut.ProcessMemberAddressImportAsync(payload, CancellationToken.None));

        Assert.Null(repository.LastAddresses);
        Assert.Collection(statusPublisher.Events,
            e => Assert.Equal(MemberAddressImportState.Processing, e.State),
            e => Assert.Equal(MemberAddressImportState.Failed, e.State));
    }

    [Fact]
    public async Task ProcessMemberAddressImportAsync_FailsWhenTenantMismatch()
    {
        var csv = "tenantId,institutionId,addressLine1,city,stateOrProvince,postalCode,countryCode,tags\n" +
                  "tenant-2,inst-1,123 Main,Seattle,WA,98101,US,primary";

        var repository = new FakeMemberAddressRepository();
        var resolver = new FakePayloadResolver(csv);
        var statusPublisher = new FakeStatusPublisher();
        var accessor = new TenantRequestContextAccessor();
        var sut = new MemberAddressImportFunctions(repository, resolver, accessor, statusPublisher, NullLogger<MemberAddressImportFunctions>.Instance);

        var message = new MemberAddressImportMessage(
            "tenant-1",
            "inst-1",
            "import.csv",
            null,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)),
            null,
            null);

        var payload = JsonSerializer.Serialize(message, SerializerOptions);

        await Assert.ThrowsAsync<InvalidDataException>(() => sut.ProcessMemberAddressImportAsync(payload, CancellationToken.None));

        Assert.Null(repository.LastAddresses);
        Assert.Collection(statusPublisher.Events,
            e => Assert.Equal(MemberAddressImportState.Processing, e.State),
            e => Assert.Equal(MemberAddressImportState.Failed, e.State));
    }

    [Fact]
    public async Task ProcessMemberAddressImportAsync_UpsertsAddressesForValidFile()
    {
        var csv = "tenantId,institutionId,addressLine1,addressLine2,city,stateOrProvince,postalCode,countryCode,tags\n" +
                  "tenant-1,inst-1,123 Main St,,Seattle,WA,98101,US,primary;vip\n" +
                  "tenant-1,inst-1,456 Pine Ave,Apt 3,Portland,OR,97205,US,";

        var repository = new FakeMemberAddressRepository
        {
            UpsertResult = Result.Success()
        };

        var resolver = new FakePayloadResolver(csv);
        var statusPublisher = new FakeStatusPublisher();
        var accessor = new TenantRequestContextAccessor();
        var sut = new MemberAddressImportFunctions(repository, resolver, accessor, statusPublisher, NullLogger<MemberAddressImportFunctions>.Instance);

        var message = new MemberAddressImportMessage(
            "tenant-1",
            "inst-1",
            "import.csv",
            null,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)),
            "corr-1",
            new Dictionary<string, string> { ["preferredUsername"] = "admin@example.com" });

        var payload = JsonSerializer.Serialize(message, SerializerOptions);

        await sut.ProcessMemberAddressImportAsync(payload, CancellationToken.None);

        Assert.NotNull(repository.LastAddresses);
        Assert.Equal("inst-1", repository.LastInstitutionId);
        Assert.Equal(2, repository.LastAddresses!.Count);

        var first = repository.LastAddresses!.First();
        Assert.Equal("123 Main St", first.Address.Line1);
        Assert.Contains("primary", first.Tags);
        Assert.Contains("vip", first.Tags);

        Assert.Collection(statusPublisher.Events,
            e => Assert.Equal(MemberAddressImportState.Processing, e.State),
            e =>
            {
                Assert.Equal(MemberAddressImportState.Completed, e.State);
                Assert.Equal(2, e.ProcessedCount);
                Assert.Equal("corr-1", e.CorrelationId);
            });
    }

    private sealed class FakeMemberAddressRepository : IMemberAddressRepository
    {
        public IReadOnlyCollection<MemberAddress>? LastAddresses { get; private set; }
        public string? LastInstitutionId { get; private set; }
        public Result UpsertResult { get; set; } = Result.Success();

        public Task<Result<MemberAddress>> CreateAsync(MemberAddress address, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<MemberAddress?>> GetAsync(string institutionId, string addressId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PagedResult<MemberAddress>> ListByInstitutionAsync(string institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<MemberAddress>> ListByStateAsync(string stateOrProvince, string? tenantId = null, string? institutionId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> DeleteAsync(string institutionId, string addressId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> UpsertBulkAsync(string institutionId, IReadOnlyCollection<MemberAddress> addresses, CancellationToken cancellationToken = default)
        {
            LastInstitutionId = institutionId;
            LastAddresses = addresses;
            return Task.FromResult(UpsertResult);
        }
    }

    private sealed class FakePayloadResolver : IMemberAddressImportPayloadResolver
    {
        private readonly string _csv;

        public FakePayloadResolver(string csv)
        {
            _csv = csv;
        }

        public Task<Stream> OpenAsync(MemberAddressImportMessage message, CancellationToken cancellationToken)
        {
            var buffer = Encoding.UTF8.GetBytes(_csv);
            Stream stream = new MemoryStream(buffer);
            return Task.FromResult(stream);
        }
    }

    private sealed class FakeStatusPublisher : IImportStatusPublisher
    {
        public List<MemberAddressImportStatusEvent> Events { get; } = new();

        public Task PublishAsync(MemberAddressImportStatusEvent statusEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(statusEvent);
            return Task.CompletedTask;
        }
    }
}
