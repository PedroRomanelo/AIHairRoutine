using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation.Providers.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation.Providers.Factories;

/// <summary>Creates the Anthropic chat-client family (adapter + <c>x-api-key</c> auth strategy).</summary>
public sealed class AnthropicClientFactory(
    IHttpClientFactory httpFactory,
    IOptions<GenerationOptions> options,
    ILoggerFactory loggerFactory) : IChatModelClientFactory
{
    public AiProvider Provider => AiProvider.Anthropic;

    public IChatModelClient Create() => new AnthropicChatClient(
        httpFactory.CreateClient(HttpClients.For(Provider)),
        new AnthropicApiKeyAuthenticator(),
        options,
        loggerFactory.CreateLogger<AnthropicChatClient>());
}
