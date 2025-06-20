using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace MemberPropertyAlert.Functions.Middleware
{
    public class ApiKeyAuthenticationMiddleware : IFunctionsWorkerMiddleware
    {
        private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
        private readonly IConfiguration _configuration;

        public ApiKeyAuthenticationMiddleware(
            ILogger<ApiKeyAuthenticationMiddleware> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            var httpRequestData = await context.GetHttpRequestDataAsync();
            if (httpRequestData != null)
            {
                // Skip authentication for certain endpoints
                var functionName = context.FunctionDefinition.Name;
                if (ShouldSkipAuthentication(functionName))
                {
                    await next(context);
                    return;
                }

                if (!await IsValidApiKey(httpRequestData))
                {
                    _logger.LogWarning("Unauthorized API request from {RemoteIpAddress}", 
                        httpRequestData.Headers.GetValues("X-Forwarded-For").FirstOrDefault() ?? "Unknown");

                    var response = httpRequestData.CreateResponse(HttpStatusCode.Unauthorized);
                    await response.WriteAsJsonAsync(new 
                    { 
                        Error = "Invalid or missing API key",
                        Timestamp = DateTime.UtcNow 
                    });

                    context.GetInvocationResult().Value = response;
                    return;
                }

                // Log successful authentication
                _logger.LogDebug("API key authentication successful for function {FunctionName}", functionName);
            }

            await next(context);
        }

        private Task<bool> IsValidApiKey(HttpRequestData request)
        {
            var headerName = _configuration["ApiKey:HeaderName"] ?? "X-API-Key";
            var validKeys = _configuration["ApiKey:ValidKeys"]?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            if (!request.Headers.TryGetValues(headerName, out var headerValues))
            {
                return Task.FromResult(false);
            }

            var providedKey = headerValues.FirstOrDefault();
            if (string.IsNullOrEmpty(providedKey))
            {
                return Task.FromResult(false);
            }

            // In production, you might want to hash the keys or use Azure Key Vault
            return Task.FromResult(validKeys.Contains(providedKey));
        }

        private static bool ShouldSkipAuthentication(string functionName)
        {
            // Skip authentication for health check, swagger, etc.
            var skipAuthFunctions = new[]
            {
                "HealthCheck",
                "SwaggerUI",
                "SwaggerJson"
            };

            return skipAuthFunctions.Contains(functionName);
        }
    }
}
