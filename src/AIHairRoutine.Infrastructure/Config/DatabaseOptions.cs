namespace AIHairRoutine.Infrastructure.Config;

/// <summary>Database and catalog-cache settings.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string? ConnectionString { get; set; }

    public bool RunMigrationsOnStartup { get; set; } = true;

    /// <summary>How long the in-memory product catalog is cached.</summary>
    public int CatalogCacheSeconds { get; set; } = 300;

    public bool Enabled => !string.IsNullOrWhiteSpace(ConnectionString);
}
