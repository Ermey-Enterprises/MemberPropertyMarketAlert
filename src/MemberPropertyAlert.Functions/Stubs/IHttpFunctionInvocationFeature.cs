using Microsoft.Azure.Functions.Worker.Http;

namespace Microsoft.Azure.Functions.Worker.Http;

/// <summary>
/// Minimal stub of IHttpFunctionInvocationFeature to enable local compilation until
/// the corresponding type becomes available via a package reference.
/// </summary>
public interface IHttpFunctionInvocationFeature
{
    HttpRequestData? HttpRequestData { get; }
    object? InvocationResult { get; set; }
}
