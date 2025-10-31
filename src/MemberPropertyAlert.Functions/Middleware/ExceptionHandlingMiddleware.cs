using System;
using System.Net;
using MemberPropertyAlert.Functions.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Middleware;

public sealed class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
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
            _logger.LogError(ex, "Unhandled exception in function {FunctionName}", context.FunctionDefinition.Name);
            var request = context.GetHttpRequestData();
            if (request is null)
            {
                throw;
            }

            var response = request.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteJsonAsync(new
            {
                type = "https://memberpropertyalerts.io/problems/unhandled-exception",
                title = "An unexpected error occurred.",
                status = (int)HttpStatusCode.InternalServerError,
                detail = ex.Message,
                traceId = context.InvocationId
            });

            context.SetHttpResponseData(response);
        }
    }
}
