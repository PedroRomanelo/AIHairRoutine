using AIHairRoutine.Infrastructure.Config;

namespace AIHairRoutine.Infrastructure.Generation.Providers;

/// <summary>
/// Strategy for applying a provider's API key to an outgoing request. This is the axis that
/// varies most between providers: header name, auth scheme and extra headers all differ
/// (Anthropic uses <c>x-api-key</c> + a version header, OpenAI/DeepSeek use a Bearer token,
/// Gemini uses <c>x-goog-api-key</c>).
/// </summary>
public interface IApiKeyAuthenticator
{
    void Apply(HttpRequestMessage request, ProviderOptions options);
}
