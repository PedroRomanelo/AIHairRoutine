using AIHairRoutine.Infrastructure.Config;

namespace AIHairRoutine.Infrastructure.Generation.Providers.Authentication;

/// <summary>Anthropic auth: the key goes in <c>x-api-key</c>, plus the required version header.</summary>
public sealed class AnthropicApiKeyAuthenticator : IApiKeyAuthenticator
{
    public void Apply(HttpRequestMessage request, ProviderOptions options)
    {
        request.Headers.TryAddWithoutValidation("x-api-key", options.ApiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", options.AnthropicVersion);
    }
}
