using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation.Providers;

/// <summary>Adapter over the DeepSeek API, which is OpenAI Chat Completions-compatible.</summary>
public sealed class DeepSeekChatClient(
    HttpClient http,
    IApiKeyAuthenticator authenticator,
    IOptions<GenerationOptions> options,
    ILogger<DeepSeekChatClient> logger)
    : OpenAiCompatibleChatClient(http, authenticator, options, logger)
{
    public override AiProvider Provider => AiProvider.DeepSeek;
}
