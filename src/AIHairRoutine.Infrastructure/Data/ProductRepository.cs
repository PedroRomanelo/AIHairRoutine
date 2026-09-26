using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using Dapper;

namespace AIHairRoutine.Infrastructure.Data;

/// <summary>Dapper-based repository. Multi-valued fields are stored as snake_case CSV columns.</summary>
public sealed class ProductRepository(ISqlConnectionFactory factory) : IProductRepository
{
    private const string SelectActive = """
        SELECT Id, Name, Brand, Description, Category, HairTypesCsv, TargetsCsv, TreatmentTypesCsv,
               SafeForChemical, ContraindicatedChemicalsCsv, UsageFrequency, ActionTimeMinutes,
               ApplicationOrder, MinIntervalDays, KeyIngredientsCsv, AllergensCsv, Price, SizeMl
        FROM dbo.Products
        WHERE Active = 1
        """;

    public async Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken ct = default)
    {
        await using var conn = factory.Create();
        var command = new CommandDefinition(SelectActive, cancellationToken: ct);
        var rows = await conn.QueryAsync<ProductRow>(command);
        return rows.Select(Map).ToList();
    }

    private static Product Map(ProductRow r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Brand = r.Brand,
        Description = r.Description,
        Category = ParseEnum<ProductCategory>(r.Category),
        HairTypes = ParseEnums<HairType>(r.HairTypesCsv),
        Targets = ParseEnums<HairPriority>(r.TargetsCsv),
        TreatmentTypes = ParseEnums<TreatmentType>(r.TreatmentTypesCsv),
        SafeForChemical = r.SafeForChemical,
        ContraindicatedChemicals = ParseEnums<ChemicalType>(r.ContraindicatedChemicalsCsv),
        UsageFrequency = ParseEnum<UsageFrequency>(r.UsageFrequency),
        ActionTimeMinutes = r.ActionTimeMinutes,
        ApplicationOrder = r.ApplicationOrder,
        MinIntervalDays = r.MinIntervalDays,
        KeyIngredients = ParseList(r.KeyIngredientsCsv),
        Allergens = ParseEnums<Allergen>(r.AllergensCsv),
        Price = r.Price,
        SizeMl = r.SizeMl,
    };

    /// <summary>Parses a CSV of snake_case tokens into enum values. "all" and blanks are ignored.</summary>
    private static IReadOnlyList<T> ParseEnums<T>(string? csv) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(csv))
            return [];

        var result = new List<T>();
        foreach (var raw in ParseList(csv))
        {
            if (raw.Equals("all", StringComparison.OrdinalIgnoreCase))
                continue;
            var token = raw.Replace("_", string.Empty);
            if (Enum.TryParse<T>(token, ignoreCase: true, out var value))
                result.Add(value);
        }

        return result;
    }

    /// <summary>Parses a single snake_case token (guarded by CHECK constraints in the table).</summary>
    private static T ParseEnum<T>(string raw) where T : struct, Enum =>
        Enum.TryParse<T>(raw.Replace("_", string.Empty), ignoreCase: true, out var value)
            ? value
            : throw new InvalidOperationException($"Unknown {typeof(T).Name} '{raw}' in the product catalog.");

    private static IReadOnlyList<string> ParseList(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed class ProductRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Brand { get; init; }
        public string? Description { get; init; }
        public string Category { get; init; } = string.Empty;
        public string? HairTypesCsv { get; init; }
        public string? TargetsCsv { get; init; }
        public string? TreatmentTypesCsv { get; init; }
        public bool SafeForChemical { get; init; }
        public string? ContraindicatedChemicalsCsv { get; init; }
        public string UsageFrequency { get; init; } = string.Empty;
        public int? ActionTimeMinutes { get; init; }
        public int ApplicationOrder { get; init; }
        public int MinIntervalDays { get; init; }
        public string? KeyIngredientsCsv { get; init; }
        public string? AllergensCsv { get; init; }
        public decimal Price { get; init; }
        public int SizeMl { get; init; }
    }
}
