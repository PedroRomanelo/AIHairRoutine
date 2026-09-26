using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation.Providers.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation.Providers.Factories;

/// <summary>Creates the Gemini chat-client family (adapter + <c>x-goog-api-key</c> auth strategy).</summary>
public sealed class GeminiClientFactory(
    IHttpClientFactory httpFactory,
    IOptions<GenerationOptions> options,
    ILoggerFactory loggerFactory) : IChatModelClientFactory
{
    public AiProvider Provider => AiProvider.Gemini;

    public IChatModelClient Create() => new GeminiChatClient(
        httpFactory.CreateClient(HttpClients.For(Provider)),
        new GeminiApiKeyAuthenticator(),
        options,
        loggerFactory.CreateLogger<GeminiChatClient>());
}
