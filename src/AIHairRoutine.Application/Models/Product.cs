namespace AIHairRoutine.Application.Models;

/// <summary>A cosmetic product from the catalog.</summary>
public sealed record Product
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Brand { get; init; }
    public string? Description { get; init; }
    public required ProductCategory Category { get; init; }

    /// <summary>Hair types this product suits. Empty means "all".</summary>
    public IReadOnlyList<HairType> HairTypes { get; init; } = [];

    /// <summary>Priorities this product addresses (condição alvo).</summary>
    public IReadOnlyList<HairPriority> Targets { get; init; } = [];

    /// <summary>H/N/R axes this product serves in the cycle. A product may serve more than one.</summary>
    public IReadOnlyList<TreatmentType> TreatmentTypes { get; init; } = [];

    /// <summary>Safe for chemically treated hair.</summary>
    public bool SafeForChemical { get; init; }

    /// <summary>Chemical processes this product must not follow while they are recent.</summary>
    public IReadOnlyList<ChemicalType> ContraindicatedChemicals { get; init; } = [];

    public UsageFrequency UsageFrequency { get; init; }

    /// <summary>Leave-on time in minutes (masks and treatments).</summary>
    public int? ActionTimeMinutes { get; init; }

    /// <summary>Position in the application sequence (1 = first).</summary>
    public int ApplicationOrder { get; init; }

    /// <summary>Minimum days between two uses of this product.</summary>
    public int MinIntervalDays { get; init; }

    public IReadOnlyList<string> KeyIngredients { get; init; } = [];

    /// <summary>Allergens this product contains.</summary>
    public IReadOnlyList<Allergen> Allergens { get; init; } = [];

    public decimal Price { get; init; }
    public int SizeMl { get; init; }
}

/// <summary>A product paired with its relevance score for a given profile.</summary>
public sealed record ProductMatch(Product Product, double Score);

/// <summary>Result of product selection: eligible products (best first) and the ones left out.</summary>
public sealed record ProductSelection
{
    public required IReadOnlyList<ProductMatch> Eligible { get; init; }
    public required IReadOnlyList<ExcludedProduct> Excluded { get; init; }
}

/// <summary>A relevant product that was excluded, with the reason.</summary>
public sealed record ExcludedProduct
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required ExclusionReason Reason { get; init; }

    /// <summary>Allergens in common with the user (when <see cref="Reason"/> is allergy).</summary>
    public IReadOnlyList<Allergen> Allergens { get; init; } = [];

    /// <summary>The chemical process that triggered the exclusion, when applicable.</summary>
    public ChemicalType? Chemical { get; init; }
}
