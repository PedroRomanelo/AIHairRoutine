namespace AIHairRoutine.Infrastructure.Config;

/// <summary>Settings for the Claude (Anthropic) Messages API used to write the routine.</summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string BaseUrl { get; set; } = "https://api.anthropic.com/";

    /// <summary>API key. When empty, generation falls back to the template generator.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Model id. Sonnet balances quality/latency/cost for this generation task.</summary>
    public string Model { get; set; } = "claude-sonnet-5";

    public int MaxTokens { get; set; } = 1500;

    public string AnthropicVersion { get; set; } = "2023-06-01";

    public int TimeoutSeconds { get; set; } = 60;

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
