namespace AIHairRoutine.Infrastructure.Config;

/// <summary>Supported AI providers for routine generation. Selected via <see cref="GenerationOptions.Provider"/>.</summary>
public enum AiProvider
{
    Anthropic,
    OpenAI,
    Gemini,
    DeepSeek,
}
