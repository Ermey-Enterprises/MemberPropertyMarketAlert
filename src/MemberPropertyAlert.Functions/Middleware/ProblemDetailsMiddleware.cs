using System;
using System.Net;
using System.Text.Json;
using MemberPropertyAlert.Functions.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Middleware;

public sealed class ProblemDetailsMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<ProblemDetailsMiddleware> _logger;

    public ProblemDetailsMiddleware(ILogger<ProblemDetailsMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        await next(context);

        var response = context.Features.Get<IHttpFunctionInvocationFeature>()?.InvocationResult as HttpResponseData;
        if (response is null)
        {
            return;
        }

        if (response.StatusCode < HttpStatusCode.BadRequest)
        {
            return;
        }

        if (response.Body.Length > 0)
        {
            return;
        }

        var problem = new
        {
            type = "https://memberpropertyalerts.io/problems/http-error",
            title = "Request failed",
            status = (int)response.StatusCode,
            traceId = context.InvocationId,
            timestampUtc = DateTimeOffset.UtcNow
        };

        await response.WriteStringAsync(JsonSerializer.Serialize(problem, SerializerOptions));
        response.Headers.Add("Content-Type", "application/problem+json");
        _logger.LogWarning("Generated problem details for {FunctionName} with status {Status}", context.FunctionDefinition.Name, response.StatusCode);
    }
}
