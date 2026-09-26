using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation.Providers;
using AIHairRoutine.Infrastructure.Generation.Providers.Authentication;
using Shouldly;

namespace AIHairRoutine.Tests;

/// <summary>
/// Each provider expects the API key in a different place. A wrong header only shows up as a 401 that the
/// template fallback hides, so the exact headers of every strategy are pinned here.
/// </summary>
public sealed class ApiKeyAuthenticatorTests
{
    private const string ApiKey = "test-key-123";
    private const string RequestUri = "https://provider.test/v1/endpoint";

    private static readonly string[] AuthHeaders = ["x-api-key", "anthropic-version", "x-goog-api-key", "Authorization"];

    private static HttpRequestMessage Apply(IApiKeyAuthenticator authenticator, ProviderOptions? options = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RequestUri);
        authenticator.Apply(request, options ?? new ProviderOptions { ApiKey = ApiKey });
        return request;
    }

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? values.Single() : null;

    /// <summary>Header names the strategy set, so a test can assert nothing else leaked in.</summary>
    private static IEnumerable<string> SetHeaders(HttpRequestMessage request) =>
        AuthHeaders.Where(name => request.Headers.Contains(name));

    [Fact]
    public void Anthropic_sends_the_key_in_x_api_key_with_the_version_header()
    {
        var request = Apply(new AnthropicApiKeyAuthenticator());

        Header(request, "x-api-key").ShouldBe(ApiKey);
        Header(request, "anthropic-version").ShouldBe("2023-06-01");
        SetHeaders(request).ShouldBe(["x-api-key", "anthropic-version"], ignoreOrder: true);
    }

    [Fact]
    public void Anthropic_uses_the_configured_version()
    {
        var request = Apply(new AnthropicApiKeyAuthenticator(), new ProviderOptions { ApiKey = ApiKey, AnthropicVersion = "2024-10-01" });

        Header(request, "anthropic-version").ShouldBe("2024-10-01");
    }

    [Fact]
    public void Bearer_sends_the_key_as_an_authorization_bearer_token()
    {
        var request = Apply(new BearerApiKeyAuthenticator());

        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe(ApiKey);
        SetHeaders(request).ShouldBe(["Authorization"]);
    }

    [Fact]
    public void Gemini_sends_the_key_in_x_goog_api_key()
    {
        var request = Apply(new GeminiApiKeyAuthenticator());

        Header(request, "x-goog-api-key").ShouldBe(ApiKey);
        SetHeaders(request).ShouldBe(["x-goog-api-key"]);
    }

    public static TheoryData<IApiKeyAuthenticator> AllAuthenticators() =>
    [
        new AnthropicApiKeyAuthenticator(),
        new BearerApiKeyAuthenticator(),
        new GeminiApiKeyAuthenticator(),
    ];

    [Theory]
    [MemberData(nameof(AllAuthenticators))]
    public void Never_puts_the_key_in_the_url_or_the_body(IApiKeyAuthenticator authenticator)
    {
        var request = Apply(authenticator);

        request.RequestUri!.ToString().ShouldBe(RequestUri);
        request.RequestUri.ToString().ShouldNotContain(ApiKey);
        request.Content.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(AllAuthenticators))]
    public void Only_touches_its_own_request(IApiKeyAuthenticator authenticator)
    {
        var first = Apply(authenticator, new ProviderOptions { ApiKey = "key-A" });
        var second = Apply(authenticator, new ProviderOptions { ApiKey = "key-B" });

        // Strategies are stateless singletons per factory: one request's key must never reach another.
        SetHeaders(first).ShouldNotBeEmpty();
        first.Headers.ToString().ShouldContain("key-A");
        first.Headers.ToString().ShouldNotContain("key-B");
        second.Headers.ToString().ShouldContain("key-B");
        second.Headers.ToString().ShouldNotContain("key-A");
    }
}
