using System.Net;
using System.Text;

namespace AIHairRoutine.Tests;

/// <summary>Records the outgoing request and answers with a canned response.</summary>
internal sealed class FakeHttpHandler(HttpStatusCode status, string responseJson) : HttpMessageHandler
{
    public HttpMethod? Method { get; private set; }
    public Uri? Uri { get; private set; }
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Body { get; private set; }
    public int Calls { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Captured here because the caller disposes the request (and its content) after sending.
        Calls++;
        Method = request.Method;
        Uri = request.RequestUri;
        foreach (var header in request.Headers)
            Headers[header.Key] = string.Join(",", header.Value);
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>Fails every request with the given exception (e.g. a timeout surfacing as TaskCanceledException).</summary>
internal sealed class ThrowingHttpHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        Task.FromException<HttpResponseMessage>(exception);
}

/// <summary>Hands out clients over one fake handler, with the base address a named client would have.</summary>
internal sealed class FakeHttpClientFactory(HttpMessageHandler handler, string baseAddress) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri(baseAddress) };
}
