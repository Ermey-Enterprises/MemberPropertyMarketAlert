using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;

namespace MemberPropertyAlert.Functions.Extensions;

public static class HttpRequestDataExtensions
{
    public static async Task<T?> ReadJsonBodyAsync<T>(this HttpRequestData request, JsonSerializerOptions? options = null)
    {
        options ??= new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await using var stream = new MemoryStream();
        await request.Body.CopyToAsync(stream);
        stream.Position = 0;
        return await JsonSerializer.DeserializeAsync<T>(stream, options);
    }

    public static async Task WriteJsonAsync<T>(this HttpResponseData response, T body, JsonSerializerOptions? options = null)
    {
        options ??= new JsonSerializerOptions(JsonSerializerDefaults.Web);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(body, options));
    }
}
