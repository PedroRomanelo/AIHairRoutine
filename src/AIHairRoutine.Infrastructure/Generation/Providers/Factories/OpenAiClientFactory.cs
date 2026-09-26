using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation.Providers.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation.Providers.Factories;

/// <summary>Creates the OpenAI chat-client family (adapter + Bearer-token auth strategy).</summary>
public sealed class OpenAiClientFactory(
    IHttpClientFactory httpFactory,
    IOptions<GenerationOptions> options,
    ILoggerFactory loggerFactory) : IChatModelClientFactory
{
    public AiProvider Provider => AiProvider.OpenAI;

    public IChatModelClient Create() => new OpenAiChatClient(
        httpFactory.CreateClient(HttpClients.For(Provider)),
        new BearerApiKeyAuthenticator(),
        options,
        loggerFactory.CreateLogger<OpenAiChatClient>());
}
