namespace AIHairRoutine.Application.Models;

/// <summary>The full response returned by the diagnosis endpoint.</summary>
public sealed record DiagnosisResult
{
    public required HairProfile Profile { get; init; }
    public required IReadOnlyList<HairPriority> Priorities { get; init; }
    public required HairSchedule Schedule { get; init; }

    /// <summary>Products used in the schedule, in order of first appearance.</summary>
    public required IReadOnlyList<RecommendedProduct> RecommendedProducts { get; init; }

    /// <summary>Relevant products left out because of allergies or chemical restrictions.</summary>
    public required IReadOnlyList<ExcludedProduct> ExcludedProducts { get; init; }

    public required DiagnosisMeta Meta { get; init; }
}

/// <summary>A recommended product, flattened for the API response.</summary>
public sealed record RecommendedProduct
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Brand { get; init; }
    public required ProductCategory Category { get; init; }
    public required IReadOnlyList<HairPriority> Targets { get; init; }
    public required IReadOnlyList<TreatmentType> TreatmentTypes { get; init; }
    public required UsageFrequency UsageFrequency { get; init; }
    public int? ActionTimeMinutes { get; init; }
    public required decimal Price { get; init; }
    public required int SizeMl { get; init; }
    public required double MatchScore { get; init; }
}

/// <summary>Provenance and operational metadata about how the result was produced.</summary>
public sealed record DiagnosisMeta
{
    public required ProfileSource ProfileSource { get; init; }
    public required bool Cached { get; init; }
    public required ModelInfo Models { get; init; }
    public required double Confidence { get; init; }
    public string Disclaimer { get; init; } =
        "Recomendação informativa gerada por IA; não substitui avaliação de um dermatologista ou profissional.";
}

/// <summary>Which models produced the profile and the narrative.</summary>
public sealed record ModelInfo
{
    public required string Profiler { get; init; }
    public required string Generator { get; init; }
}
