using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace MemberPropertyAlert.Functions.Middleware
{
    public class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
    {
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in function {FunctionName}", 
                    context.FunctionDefinition.Name);

                var httpRequestData = await context.GetHttpRequestDataAsync();
                if (httpRequestData != null)
                {
                    var response = await CreateErrorResponse(httpRequestData, ex);
                    context.GetInvocationResult().Value = response;
                }
                else
                {
                    // For non-HTTP triggered functions, just log the error
                    _logger.LogError(ex, "Function {FunctionName} failed with exception", 
                        context.FunctionDefinition.Name);
                    throw;
                }
            }
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData request, Exception exception)
        {
            var (statusCode, message) = GetErrorDetails(exception);

            var response = request.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json");

            var errorResponse = new
            {
                Error = message,
                Type = exception.GetType().Name,
                Timestamp = DateTime.UtcNow,
                TraceId = System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString()
            };

            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await response.WriteStringAsync(json);
            return response;
        }

        private static (HttpStatusCode statusCode, string message) GetErrorDetails(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException => (HttpStatusCode.BadRequest, "Required parameter is missing"),
                ArgumentException => (HttpStatusCode.BadRequest, "Invalid request parameters"),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Access denied"),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found"),
                InvalidOperationException => (HttpStatusCode.Conflict, "Operation not allowed in current state"),
                TimeoutException => (HttpStatusCode.RequestTimeout, "Request timed out"),
                NotSupportedException => (HttpStatusCode.NotImplemented, "Operation not supported"),
                _ => (HttpStatusCode.InternalServerError, "An internal server error occurred")
            };
        }
    }

    public class RequestLoggingMiddleware : IFunctionsWorkerMiddleware
    {
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            var httpRequestData = await context.GetHttpRequestDataAsync();
            if (httpRequestData != null)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var functionName = context.FunctionDefinition.Name;
                var method = httpRequestData.Method;
                var url = httpRequestData.Url.ToString();
                var userAgent = httpRequestData.Headers.GetValues("User-Agent").FirstOrDefault() ?? "Unknown";
                var remoteIp = httpRequestData.Headers.GetValues("X-Forwarded-For").FirstOrDefault() ?? "Unknown";

                _logger.LogInformation("HTTP {Method} {Url} started - Function: {FunctionName}, UserAgent: {UserAgent}, RemoteIP: {RemoteIP}",
                    method, url, functionName, userAgent, remoteIp);

                try
                {
                    await next(context);

                    stopwatch.Stop();
                    var result = context.GetInvocationResult();
                    var statusCode = "Unknown";

                    if (result.Value is HttpResponseData response)
                    {
                        statusCode = ((int)response.StatusCode).ToString();
                    }

                    _logger.LogInformation("HTTP {Method} {Url} completed - Function: {FunctionName}, StatusCode: {StatusCode}, Duration: {Duration}ms",
                        method, url, functionName, statusCode, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "HTTP {Method} {Url} failed - Function: {FunctionName}, Duration: {Duration}ms",
                        method, url, functionName, stopwatch.ElapsedMilliseconds);
                    throw;
                }
            }
            else
            {
                // Non-HTTP function
                var functionName = context.FunctionDefinition.Name;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                _logger.LogInformation("Function {FunctionName} started", functionName);

                try
                {
                    await next(context);
                    stopwatch.Stop();
                    _logger.LogInformation("Function {FunctionName} completed - Duration: {Duration}ms",
                        functionName, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "Function {FunctionName} failed - Duration: {Duration}ms",
                        functionName, stopwatch.ElapsedMilliseconds);
                    throw;
                }
            }
        }
    }
}
