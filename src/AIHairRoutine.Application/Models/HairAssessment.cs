namespace AIHairRoutine.Application.Models;

/// <summary>
/// Questionnaire answers submitted by the client. This is the API input. Single-choice answers are
/// nullable so a missing answer is reported by the validator instead of silently defaulting.
/// </summary>
public sealed record HairAssessment
{
    public HairType? HairType { get; init; }

    public HairThickness? Thickness { get; init; }

    public HairTone? Tone { get; init; }

    /// <summary>Current conditions (multiple choice).</summary>
    public IReadOnlyList<HairCondition>? Conditions { get; init; }

    /// <summary>Chemical history. Null or <c>hasChemical = false</c> means no chemical process.</summary>
    public ChemicalHistory? Chemical { get; init; }

    /// <summary>Free text: main complaint or goal (queda, ressecamento, volume, brilho...).</summary>
    public string? MainGoal { get; init; }

    /// <summary>Ticked allergens (checkbox). Empty means no known allergy.</summary>
    public IReadOnlyList<Allergen>? Allergies { get; init; }

    /// <summary>BCP-47 language tag for the generated texts. Defaults to pt-BR.</summary>
    public string Locale { get; init; } = "pt-BR";
}

/// <summary>Chemical history answers.</summary>
public sealed record ChemicalHistory
{
    public bool HasChemical { get; init; }

    public ChemicalType? Type { get; init; }

    /// <summary>When it was done: "há 2 meses", "3 semanas", "10/06/2026", "2026-06-10" or "06/2026".</summary>
    public string? Performed { get; init; }

    public TouchUpFrequency? TouchUpFrequency { get; init; }
}
