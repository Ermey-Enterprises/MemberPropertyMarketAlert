using System.Net;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MemberPropertyAlert.Functions.Configuration;
using MemberPropertyAlert.Functions.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemberPropertyAlert.Functions.Middleware;

public sealed class ApiKeyAuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IOptions<ApiKeyOptions> _options;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(IOptions<ApiKeyOptions> options, ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpRequest = context.GetHttpRequestData();
        if (httpRequest is null)
        {
            await next(context);
            return;
        }

        var methodInfo = context.GetTargetFunctionMethod();
        if (methodInfo is not null && Attribute.IsDefined(methodInfo, typeof(AllowAnonymousAttribute)))
        {
            await next(context);
            return;
        }

        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.AdminKey))
        {
            _logger.LogWarning("API key authentication is disabled because no ApiKey:AdminKey value is configured.");
            await next(context);
            return;
        }

        var headerName = string.IsNullOrWhiteSpace(options.HeaderName) ? "x-api-key" : options.HeaderName;
        if (!httpRequest.Headers.TryGetValues(headerName, out var providedValues))
        {
            await RejectAsync(context, httpRequest, headerName, "missing");
            return;
        }

        var providedKey = providedValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            await RejectAsync(context, httpRequest, headerName, "empty");
            return;
        }

        var expectedHash = ComputeHash(options.AdminKey, options.HashAlgorithm);
        var providedHash = ComputeHash(providedKey, options.HashAlgorithm);

        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expectedHash), Convert.FromHexString(providedHash)))
        {
            await RejectAsync(context, httpRequest, headerName, "invalid");
            return;
        }

        await next(context);
    }

    private static async Task RejectAsync(FunctionContext context, HttpRequestData request, string headerName, string reason)
    {
        var response = request.CreateResponse(HttpStatusCode.Unauthorized);
            await response.WriteJsonAsync(new
        {
            error = "unauthorized",
            message = $"API key header '{headerName}' is {reason}."
        });

        context.SetHttpResponseData(response);
    }

    private static string ComputeHash(string value, string? algorithm)
    {
        using var hashAlgorithm = SelectHashAlgorithm(algorithm);
        var bytes = Encoding.UTF8.GetBytes(value);
        var hashBytes = hashAlgorithm.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private static HashAlgorithm SelectHashAlgorithm(string? algorithm)
    {
        return (algorithm?.Trim().ToUpperInvariant()) switch
        {
            "SHA512" => SHA512.Create(),
            "SHA384" => SHA384.Create(),
            "SHA1" => SHA1.Create(),
            _ => SHA256.Create()
        };
    }
}
