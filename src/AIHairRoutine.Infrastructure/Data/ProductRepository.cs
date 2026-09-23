using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using Dapper;

namespace AIHairRoutine.Infrastructure.Data;

/// <summary>Dapper-based repository. Targets/HairTypes are stored as snake_case CSV columns.</summary>
public sealed class ProductRepository(ISqlConnectionFactory factory) : IProductRepository
{
    private const string SelectActive = """
        SELECT Id, Name, Brand, Category, TargetsCsv, HairTypesCsv, ForChemical, Description
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
        Category = r.Category,
        Targets = ParseEnums<HairPriority>(r.TargetsCsv),
        HairTypes = ParseEnums<HairType>(r.HairTypesCsv),
        ForChemical = r.ForChemical,
        Description = r.Description,
    };

    /// <summary>Parses a CSV of snake_case tokens into enum values. "all" and blanks are ignored.</summary>
    private static IReadOnlyList<T> ParseEnums<T>(string? csv) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(csv))
            return [];

        var result = new List<T>();
        foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw.Equals("all", StringComparison.OrdinalIgnoreCase))
                continue;
            var token = raw.Replace("_", string.Empty);
            if (Enum.TryParse<T>(token, ignoreCase: true, out var value))
                result.Add(value);
        }

        return result;
    }

    private sealed class ProductRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Brand { get; init; }
        public string Category { get; init; } = string.Empty;
        public string? TargetsCsv { get; init; }
        public string? HairTypesCsv { get; init; }
        public bool ForChemical { get; init; }
        public string? Description { get; init; }
    }
}
