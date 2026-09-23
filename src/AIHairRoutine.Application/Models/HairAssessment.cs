namespace AIHairRoutine.Application.Models;

/// <summary>Raw questionnaire answers submitted by the client. This is the API input.</summary>
public sealed record HairAssessment
{
    public HairType HairType { get; init; }

    public ChemicalTreatment ChemicalTreatment { get; init; } = ChemicalTreatment.None;

    /// <summary>Whether the hair is color treated (independent from <see cref="ChemicalTreatment"/>).</summary>
    public bool ColorTreated { get; init; }

    /// <summary>Self-reported intensity of each concern, 0..10.</summary>
    public HairConcerns Concerns { get; init; } = new();

    /// <summary>Optional free text. When present, the hybrid profiler prefers JEV.</summary>
    public string? Notes { get; init; }

    /// <summary>BCP-47 language tag for the generated routine text. Defaults to pt-BR.</summary>
    public string Locale { get; init; } = "pt-BR";
}

/// <summary>Scores 0..10 for each perceived problem.</summary>
public sealed record HairConcerns
{
    public int Dryness { get; init; }
    public int Frizz { get; init; }
    public int Breakage { get; init; }
    public int Oiliness { get; init; }
    public int HairLoss { get; init; }
}
