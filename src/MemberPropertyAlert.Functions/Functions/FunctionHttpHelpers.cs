using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using MemberPropertyAlert.Functions.Extensions;
using Microsoft.Azure.Functions.Worker.Http;

namespace MemberPropertyAlert.Functions.Functions;

internal static class FunctionHttpHelpers
{
    public static IReadOnlyDictionary<string, string> ParseQuery(HttpRequestData req)
    {
        var query = req.Url.Query.TrimStart('?');
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
        {
            return values;
        }

        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            values[key] = value;
        }

        return values;
    }

    public static int GetPositiveInt(IReadOnlyDictionary<string, string> query, string key, int fallback)
    {
        return query.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }

    public static async Task<HttpResponseData> CreateErrorResponseAsync(HttpRequestData req, HttpStatusCode statusCode, string? message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteJsonAsync(new { error = message ?? "An error occurred." });
        return response;
    }
}
