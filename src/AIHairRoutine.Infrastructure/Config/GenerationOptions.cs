namespace AIHairRoutine.Infrastructure.Config;

/// <summary>
/// Routine-generation settings. Selects one active AI provider (fixed by configuration) and holds
/// the per-provider connection settings. The active provider's settings are resolved via
/// <see cref="Active"/>; token/timeout budgets are shared across providers.
/// </summary>
public sealed class GenerationOptions
{
    public const string SectionName = "Generation";

    /// <summary>Active provider. Chosen once at startup.</summary>
    public AiProvider Provider { get; set; } = AiProvider.Anthropic;

    public int MaxTokens { get; set; } = 1500;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Per-provider settings, keyed by provider name (case-insensitive).</summary>
    public Dictionary<string, ProviderOptions> Providers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Settings for the active provider, or an empty instance when none is configured.</summary>
    public ProviderOptions Active =>
        Providers.TryGetValue(Provider.ToString(), out var options) ? options : new ProviderOptions();

    /// <summary>When false (no API key for the active provider), generation uses the template.</summary>
    public bool Enabled => Active.HasApiKey;
}
