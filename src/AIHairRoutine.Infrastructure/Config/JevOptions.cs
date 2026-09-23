namespace AIHairRoutine.Infrastructure.Config;

/// <summary>Settings for the JEV (TypeSafe AI) System One classifier.</summary>
public sealed class JevOptions
{
    public const string SectionName = "Jev";

    public string BaseUrl { get; set; } = "https://api.typesafe.ai/";

    /// <summary>API key. When empty, JEV is disabled and the profiler stays rule-based.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "jev-latest";

    public int TimeoutSeconds { get; set; } = 5;

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
