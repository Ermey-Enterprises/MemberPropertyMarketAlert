using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Functions.Models;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Infrastructure.Storage;

public sealed class MemberAddressImportPayloadResolver : IMemberAddressImportPayloadResolver
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MemberAddressImportPayloadResolver> _logger;

    public MemberAddressImportPayloadResolver(
        IHttpClientFactory httpClientFactory,
        ILogger<MemberAddressImportPayloadResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Stream> OpenAsync(MemberAddressImportMessage message, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.CsvBase64))
        {
            _logger.LogInformation(
                "Opening CSV payload for tenant {TenantId} institution {InstitutionId} from base64 content.",
                message.TenantId,
                message.InstitutionId);

            var buffer = Convert.FromBase64String(message.CsvBase64);
            return new MemoryStream(buffer, writable: false);
        }

        if (!string.IsNullOrWhiteSpace(message.BlobUri))
        {
            var client = _httpClientFactory.CreateClient("member-address-import");
            _logger.LogInformation(
                "Downloading CSV payload for tenant {TenantId} institution {InstitutionId} from {Uri}.",
                message.TenantId,
                message.InstitutionId,
                message.BlobUri);

            using var response = await client.GetAsync(message.BlobUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var stream = new MemoryStream();
            await response.Content.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
            stream.Position = 0;
            return stream;
        }

        throw new InvalidOperationException("Import message must contain either a blobUri or csvBase64 payload.");
    }
}
