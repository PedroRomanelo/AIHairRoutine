using AIHairRoutine.Infrastructure.Config;

namespace AIHairRoutine.Infrastructure.Generation.Providers;

/// <summary>Named-HttpClient identifiers for the AI providers, and the provider→name mapping.</summary>
internal static class HttpClients
{
    public const string Anthropic = "ai-anthropic";
    public const string OpenAI = "ai-openai";
    public const string Gemini = "ai-gemini";
    public const string DeepSeek = "ai-deepseek";

    public static string For(AiProvider provider) => provider switch
    {
        AiProvider.Anthropic => Anthropic,
        AiProvider.OpenAI => OpenAI,
        AiProvider.Gemini => Gemini,
        AiProvider.DeepSeek => DeepSeek,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown AI provider."),
    };
}
