using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using MemberPropertyAlert.Functions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemberPropertyAlert.Functions.Infrastructure.Secrets;

public sealed class KeyVaultSecretProvider : ISecretProvider
{
    private readonly SecretClient? _secretClient;
    private readonly ILogger<KeyVaultSecretProvider> _logger;
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public KeyVaultSecretProvider(IOptions<KeyVaultOptions> options, TokenCredential credential, ILogger<KeyVaultSecretProvider> logger)
    {
        _logger = logger;

        var vaultUri = options.Value.VaultUri;
        if (string.IsNullOrWhiteSpace(vaultUri))
        {
            _logger.LogInformation("Key Vault not configured. Falling back to configuration-only secrets.");
            return;
        }

        _secretClient = new SecretClient(new Uri(vaultUri), credential);
    }

    public async Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            return null;
        }

        if (_cache.TryGetValue(secretName, out var cached))
        {
            return cached;
        }

        if (_secretClient is null)
        {
            return null;
        }

        try
        {
            var response = await _secretClient.GetSecretAsync(secretName, null, cancellationToken);
            string? value = response.Value.Value;
            _cache[secretName] = value;
            return value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret {SecretName} was not found in Key Vault.", secretName);
            _cache[secretName] = null;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret {SecretName} from Key Vault.", secretName);
            throw;
        }
    }
}
