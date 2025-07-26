using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MemberPropertyAlert.Functions.Configuration;

namespace MemberPropertyAlert.Functions.Services
{
    /// <summary>
    /// Health check for MockRentCastAPI external dependency
    /// </summary>
    public class MockRentCastApiHealthCheckService : IHealthCheck
    {
        private readonly HttpClient _httpClient;
        private readonly Configuration.MockRentCastApiHealthCheck _config;
        private readonly ILogger<MockRentCastApiHealthCheckService> _logger;

        public MockRentCastApiHealthCheckService(
            HttpClient httpClient,
            IOptions<HealthCheckConfiguration> config,
            ILogger<MockRentCastApiHealthCheckService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _config = config.Value?.MockRentCastApi ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            if (!_config.Enabled)
            {
                _logger.LogDebug("MockRentCastAPI health check is disabled");
                return HealthCheckResult.Healthy("Health check disabled");
            }

            try
            {
                _logger.LogDebug("Checking MockRentCastAPI health at {Url}", _config.Url);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

                var response = await _httpClient.GetAsync(_config.Url, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseTime = response.Headers.Date.HasValue 
                        ? DateTime.UtcNow - response.Headers.Date.Value.UtcDateTime 
                        : TimeSpan.Zero;

                    var data = new Dictionary<string, object>
                    {
                        ["status_code"] = (int)response.StatusCode,
                        ["response_time_ms"] = responseTime.TotalMilliseconds,
                        ["url"] = _config.Url,
                        ["timestamp"] = DateTime.UtcNow
                    };

                    _logger.LogDebug("MockRentCastAPI health check passed. Response time: {ResponseTime}ms", 
                        responseTime.TotalMilliseconds);

                    return HealthCheckResult.Healthy(
                        $"MockRentCastAPI is healthy (Response time: {responseTime.TotalMilliseconds:F0}ms)",
                        data);
                }
                else
                {
                    var data = new Dictionary<string, object>
                    {
                        ["status_code"] = (int)response.StatusCode,
                        ["url"] = _config.Url,
                        ["timestamp"] = DateTime.UtcNow
                    };

                    _logger.LogWarning("MockRentCastAPI health check failed. Status: {StatusCode}", response.StatusCode);

                    return HealthCheckResult.Unhealthy(
                        $"MockRentCastAPI returned {response.StatusCode}",
                        data: data);
                }
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
            {
                var data = new Dictionary<string, object>
                {
                    ["url"] = _config.Url,
                    ["timeout_seconds"] = _config.TimeoutSeconds,
                    ["timestamp"] = DateTime.UtcNow
                };

                _logger.LogError(ex, "MockRentCastAPI health check timed out after {Timeout}s", _config.TimeoutSeconds);

                return HealthCheckResult.Unhealthy(
                    $"MockRentCastAPI health check timed out after {_config.TimeoutSeconds}s",
                    ex,
                    data);
            }
            catch (HttpRequestException ex)
            {
                var data = new Dictionary<string, object>
                {
                    ["url"] = _config.Url,
                    ["error"] = ex.Message,
                    ["timestamp"] = DateTime.UtcNow
                };

                _logger.LogError(ex, "MockRentCastAPI health check failed with HTTP error");

                return HealthCheckResult.Unhealthy(
                    "MockRentCastAPI is not reachable",
                    ex,
                    data);
            }
            catch (Exception ex)
            {
                var data = new Dictionary<string, object>
                {
                    ["url"] = _config.Url,
                    ["error"] = ex.Message,
                    ["timestamp"] = DateTime.UtcNow
                };

                _logger.LogError(ex, "MockRentCastAPI health check failed with unexpected error");

                return HealthCheckResult.Unhealthy(
                    "MockRentCastAPI health check failed",
                    ex,
                    data);
            }
        }
    }
}
