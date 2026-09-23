namespace AIHairRoutine.Infrastructure.Config;

/// <summary>Cache settings for generated routines (the expensive Claude calls).</summary>
public sealed class RoutineCacheOptions
{
    public const string SectionName = "RoutineCache";

    public int ExpirationMinutes { get; set; } = 60;
}
