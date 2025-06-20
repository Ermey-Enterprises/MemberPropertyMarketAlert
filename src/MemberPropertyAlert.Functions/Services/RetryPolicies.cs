using Polly;
using Polly.Extensions.Http;
using System.Net;

namespace MemberPropertyAlert.Functions.Services
{
    public static class RetryPolicies
    {
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode && msg.StatusCode != HttpStatusCode.NotFound)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        // Log retry attempts - logger would need to be injected separately
                        System.Diagnostics.Debug.WriteLine($"Retry {retryCount} in {timespan.TotalMilliseconds}ms");
                    });
        }

        public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (exception, timespan) =>
                    {
                        // Log circuit breaker opened
                    },
                    onReset: () =>
                    {
                        // Log circuit breaker closed
                    });
        }
    }
}
