using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace MemberPropertyAlert.Functions.Tests;

internal sealed class TestHttpResponseData : HttpResponseData
{
    private readonly MemoryStream _bodyStream = new();
    private readonly HttpHeadersCollection _headers = new();
    private readonly HttpCookies _cookies = new();

    public TestHttpResponseData(FunctionContext context)
        : base(context)
    {
    }

    public override HttpStatusCode StatusCode { get; set; }

    public override HttpHeadersCollection Headers => _headers;

    public override Stream Body => _bodyStream;

    public override HttpCookies Cookies => _cookies;

    public string BodyAsString => Encoding.UTF8.GetString(_bodyStream.ToArray());

    public override Task WriteStringAsync(string value)
    {
        return WriteStringAsync(value, Encoding.UTF8);
    }

    public override Task WriteStringAsync(string value, Encoding encoding)
    {
        var bytes = encoding.GetBytes(value);
        _bodyStream.SetLength(0);
        _bodyStream.Write(bytes, 0, bytes.Length);
        _bodyStream.Position = 0;
        return Task.CompletedTask;
    }
}
