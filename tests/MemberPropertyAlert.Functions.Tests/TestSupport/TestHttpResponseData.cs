using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace MemberPropertyAlert.Functions.Tests;

internal sealed class TestHttpResponseData : HttpResponseData
{
    private MemoryStream _bodyStream = new();
    private HttpHeadersCollection _headers = new();
    private readonly TestHttpCookies _cookies = new();

    public TestHttpResponseData(FunctionContext context)
        : base(context)
    {
    }

    public override HttpStatusCode StatusCode { get; set; }

    public override HttpHeadersCollection Headers
    {
        get => _headers;
        set => _headers = value ?? new HttpHeadersCollection();
    }

    public override Stream Body
    {
        get => _bodyStream;
        set
        {
            if (value is MemoryStream memoryStream)
            {
                _bodyStream = memoryStream;
            }
            else if (value is not null)
            {
                var copy = new MemoryStream();
                value.CopyTo(copy);
                copy.Position = 0;
                _bodyStream = copy;
            }
            else
            {
                _bodyStream = new MemoryStream();
            }
        }
    }

    public override HttpCookies Cookies => _cookies;

    public string BodyAsString => Encoding.UTF8.GetString(_bodyStream.ToArray());

    public Task WriteStringAsync(string value) => WriteStringAsync(value, Encoding.UTF8);

    public Task WriteStringAsync(string value, Encoding encoding)
    {
        var bytes = encoding.GetBytes(value);
        _bodyStream.SetLength(0);
        _bodyStream.Write(bytes, 0, bytes.Length);
        _bodyStream.Position = 0;
        return Task.CompletedTask;
    }

    private sealed class TestHttpCookies : HttpCookies
    {
        private readonly List<IHttpCookie> _cookies = new();

        public IReadOnlyList<IHttpCookie> Items => _cookies;

        public override void Append(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Cookie name is required", nameof(name));
            }

            Append(new TestHttpCookie(name, value));
        }

        public override void Append(IHttpCookie cookie)
        {
            if (cookie is null)
            {
                throw new ArgumentNullException(nameof(cookie));
            }

            _cookies.Add(cookie);
        }

        public override IHttpCookie CreateNew() => new TestHttpCookie(string.Empty, string.Empty);

        private sealed class TestHttpCookie : IHttpCookie
        {
            public TestHttpCookie(string name, string value)
            {
                Name = name;
                Value = value;
            }

            public string Name { get; }
            public string Value { get; }
            public string? Domain => null;
            public DateTimeOffset? Expires => null;
            public bool? HttpOnly => null;
            public double? MaxAge => null;
            public string? Path => null;
            public SameSite SameSite => SameSite.None;
            public bool? Secure => null;
        }
    }
}
