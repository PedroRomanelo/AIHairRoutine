namespace AIHairRoutine.Application.Models;

/// <summary>Typed profile derived from a <see cref="HairAssessment"/> (by rules, optionally refined by JEV).</summary>
public sealed record HairProfile
{
    public required HairType HairType { get; init; }
    public required HairThickness Thickness { get; init; }
    public required HairTone Tone { get; init; }
    public required IReadOnlyList<HairCondition> Conditions { get; init; }
    public required ChemicalStatus Chemical { get; init; }

    /// <summary>How the H/N/R treatment slots of the cycle should be balanced.</summary>
    public required TreatmentNeeds TreatmentNeeds { get; init; }

    public required IReadOnlyList<Allergen> Allergies { get; init; }

    public bool Has(HairCondition condition) => Conditions.Contains(condition);
}

/// <summary>Chemical status with the elapsed time already resolved.</summary>
public sealed record ChemicalStatus
{
    /// <summary>A chemical process done less than this many days ago is "recent" (3 months).</summary>
    public const int RecentThresholdDays = 90;

    public static readonly ChemicalStatus None = new() { HasChemical = false };

    public required bool HasChemical { get; init; }
    public ChemicalType? Type { get; init; }
    public int? DaysSince { get; init; }
    public TouchUpFrequency? TouchUpFrequency { get; init; }

    public bool IsRecent => HasChemical && DaysSince is < RecentThresholdDays;
}

/// <summary>Relative need for each treatment axis. The three shares sum to ~1.</summary>
public sealed record TreatmentNeeds(double Hydration, double Nutrition, double Reconstruction)
{
    public static TreatmentNeeds FromWeights(int hydration, int nutrition, int reconstruction)
    {
        double total = hydration + nutrition + reconstruction;
        return new TreatmentNeeds(
            Math.Round(hydration / total, 2),
            Math.Round(nutrition / total, 2),
            Math.Round(reconstruction / total, 2));
    }
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
