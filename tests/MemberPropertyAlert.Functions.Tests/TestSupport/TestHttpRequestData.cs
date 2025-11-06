using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;

namespace MemberPropertyAlert.Functions.Tests;

internal sealed class TestHttpRequestData : HttpRequestData
{
    private readonly MemoryStream _bodyStream = new();
    private readonly HttpHeadersCollection _headers = new();
    private readonly Uri _url;
    private readonly string _method;

    public TestHttpRequestData(FunctionContext context, Uri url, string method = "GET")
        : base(context)
    {
        _url = url;
        _method = method;
    }

    public override Stream Body => _bodyStream;

    public override HttpHeadersCollection Headers => _headers;

    public override IReadOnlyCollection<IHttpCookie> Cookies => Array.Empty<IHttpCookie>();

    public override Uri Url => _url;

    public override string Method => _method;

    public override IEnumerable<ClaimsIdentity> Identities => Array.Empty<ClaimsIdentity>();

    public override HttpResponseData CreateResponse()
    {
        return new TestHttpResponseData(FunctionContext)
        {
            StatusCode = HttpStatusCode.OK
        };
    }

    public override HttpResponseData CreateResponse(HttpStatusCode statusCode)
    {
        return new TestHttpResponseData(FunctionContext)
        {
            StatusCode = statusCode
        };
    }
}
