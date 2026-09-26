using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation.Providers.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation.Providers.Factories;

/// <summary>Creates the DeepSeek chat-client family (OpenAI-compatible adapter + Bearer-token auth).</summary>
public sealed class DeepSeekClientFactory(
    IHttpClientFactory httpFactory,
    IOptions<GenerationOptions> options,
    ILoggerFactory loggerFactory) : IChatModelClientFactory
{
    public AiProvider Provider => AiProvider.DeepSeek;

    public IChatModelClient Create() => new DeepSeekChatClient(
        httpFactory.CreateClient(HttpClients.For(Provider)),
        new BearerApiKeyAuthenticator(),
        options,
        loggerFactory.CreateLogger<DeepSeekChatClient>());
}
