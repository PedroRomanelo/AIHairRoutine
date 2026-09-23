namespace AIHairRoutine.Application.Models;

/// <summary>A cosmetic product from the catalog.</summary>
public sealed record Product
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? Brand { get; init; }

    /// <summary>e.g. shampoo, conditioner, mask, leave_in, oil, tonic.</summary>
    public required string Category { get; init; }

    /// <summary>Priorities this product addresses.</summary>
    public required IReadOnlyList<HairPriority> Targets { get; init; }

    /// <summary>Hair types this product suits. Empty means "all".</summary>
    public required IReadOnlyList<HairType> HairTypes { get; init; }

    /// <summary>Safe/recommended for chemically treated hair.</summary>
    public bool ForChemical { get; init; }

    public string? Description { get; init; }
}

/// <summary>A product paired with its relevance score for a given profile.</summary>
public sealed record ProductMatch(Product Product, double Score);
