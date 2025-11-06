using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace MemberPropertyAlert.Functions.Tests;

internal sealed class TestHttpRequestData : HttpRequestData
{
    private readonly MemoryStream _bodyStream = new();
    private readonly HttpHeadersCollection _headers = new();
    private readonly List<IHttpCookie> _cookies = new();
    private readonly List<ClaimsIdentity> _identities = new();
    private readonly NameValueCollection _query = new();
    private readonly Uri _url;
    private readonly string _method;

    public TestHttpRequestData(FunctionContext context, Uri url, string method = "GET")
        : base(context)
    {
        _url = url;
        _method = method;
        PopulateQueryFromUri(url);
    }

    public override Stream Body => _bodyStream;

    public override HttpHeadersCollection Headers => _headers;

    public override IReadOnlyCollection<IHttpCookie> Cookies => _cookies;

    public override Uri Url => _url;

    public override string Method => _method;

    public override IEnumerable<ClaimsIdentity> Identities => _identities;

    public override NameValueCollection Query => _query;

    public void SetQueryParameter(string key, string value)
    {
        _query[key] = value;
    }

    public void AddCookie(IHttpCookie cookie)
    {
        _cookies.Add(cookie);
    }

    public void AddIdentity(ClaimsIdentity identity)
    {
        _identities.Add(identity);
    }

    private void PopulateQueryFromUri(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.Query))
        {
            return;
        }

        var pairs = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            _query[key] = value;
        }
    }

    public override HttpResponseData CreateResponse()
    {
        return new TestHttpResponseData(FunctionContext)
        {
            StatusCode = System.Net.HttpStatusCode.OK
        };
    }
}
