using AIHairRoutine.Infrastructure.Config;

namespace AIHairRoutine.Infrastructure.Generation.Providers.Authentication;

/// <summary>Gemini auth: the key goes in the <c>x-goog-api-key</c> header (kept out of the URL).</summary>
public sealed class GeminiApiKeyAuthenticator : IApiKeyAuthenticator
{
    public void Apply(HttpRequestMessage request, ProviderOptions options) =>
        request.Headers.TryAddWithoutValidation("x-goog-api-key", options.ApiKey);
}
