using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation.Providers;

/// <summary>Adapter over the OpenAI Chat Completions API.</summary>
public sealed class OpenAiChatClient(
    HttpClient http,
    IApiKeyAuthenticator authenticator,
    IOptions<GenerationOptions> options,
    ILogger<OpenAiChatClient> logger)
    : OpenAiCompatibleChatClient(http, authenticator, options, logger)
{
    public override AiProvider Provider => AiProvider.OpenAI;
}
