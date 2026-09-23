namespace AIHairRoutine.Application.Models;

/// <summary>Typed classification derived from a <see cref="HairAssessment"/> (by rules or JEV).</summary>
public sealed record HairProfile
{
    public required HairType HairType { get; init; }
    public required HairCondition Condition { get; init; }
    public required DamageLevel DamageLevel { get; init; }
    public required FrizzLevel FrizzLevel { get; init; }
    public required bool ChemicalTreatment { get; init; }
}

/// <summary>Output of an <c>IHairProfiler</c>: the profile plus ordered priorities and provenance.</summary>
public sealed record ProfileResult
{
    public required HairProfile Profile { get; init; }

    /// <summary>Care priorities ordered from most to least important.</summary>
    public required IReadOnlyList<HairPriority> Priorities { get; init; }

    /// <summary>Which strategy produced this result.</summary>
    public required ProfileSource Source { get; init; }

    /// <summary>Classifier model id (e.g. "jev-1.13.0") or "rules".</summary>
    public required string Model { get; init; }

    /// <summary>Confidence 0..1 (1 for deterministic rules).</summary>
    public double Confidence { get; init; }
}
