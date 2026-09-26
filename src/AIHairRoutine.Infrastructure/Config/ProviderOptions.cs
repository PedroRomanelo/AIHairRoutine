namespace AIHairRoutine.Infrastructure.Config;

/// <summary>
/// Connection settings for a single AI provider. The <see cref="ApiKey"/> is the same concept
/// everywhere, but the scheme by which it is sent varies per provider — that variation is handled
/// by the API-key authentication strategy, not here.
/// </summary>
public sealed class ProviderOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>API key. When empty, generation falls back to the template generator.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Model id for this provider (e.g. <c>claude-sonnet-5</c>, <c>gpt-4o-mini</c>).</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Anthropic-only version header. Ignored by other providers.</summary>
    public string AnthropicVersion { get; set; } = "2023-06-01";

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
