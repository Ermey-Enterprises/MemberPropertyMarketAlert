using System.Reflection;
using MemberPropertyAlert.Functions.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace MemberPropertyAlert.Functions.Extensions;

public static class FunctionContextExtensions
{
    public static HttpRequestData? GetHttpRequestData(this FunctionContext context)
    {
    return context.Features.Get<IHttpFunctionInvocationFeature>()?.HttpRequestData;
    }

    public static void SetHttpResponseData(this FunctionContext context, HttpResponseData response)
    {
    if (context.Features.Get<IHttpFunctionInvocationFeature>() is { } httpFeature)
        {
            httpFeature.InvocationResult = response;
        }
    }

    public static MethodInfo? GetTargetFunctionMethod(this FunctionContext context)
    {
        var entryPoint = context.FunctionDefinition.EntryPoint;
        if (string.IsNullOrWhiteSpace(entryPoint))
        {
            return null;
        }

        var assemblyPath = context.FunctionDefinition.PathToAssembly;
        var lastIndex = entryPoint.LastIndexOf('.');
        if (lastIndex < 0)
        {
            return null;
        }

        var typeName = entryPoint[..lastIndex];
        var methodName = entryPoint[(lastIndex + 1)..];

        Assembly assembly;
        if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            assembly = Assembly.LoadFrom(assemblyPath);
        }
        else
        {
            assembly = Assembly.GetExecutingAssembly();
        }

        var type = assembly.GetType(typeName);
        return type?.GetMethod(methodName);
    }

    public static TenantRequestContext? GetTenantRequestContext(this FunctionContext context)
    {
        if (context.Items.TryGetValue(nameof(TenantRequestContext), out var value) && value is TenantRequestContext tenantContext)
        {
            return tenantContext;
        }

        return null;
    }
}
