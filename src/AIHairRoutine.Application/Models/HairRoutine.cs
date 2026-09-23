namespace AIHairRoutine.Application.Models;

/// <summary>The final, human-readable care routine.</summary>
public sealed record HairRoutine
{
    public required string Summary { get; init; }
    public required IReadOnlyList<RoutineStep> Steps { get; init; }
    public IReadOnlyList<string> Tips { get; init; } = [];
}

/// <summary>A single step of the routine.</summary>
public sealed record RoutineStep
{
    public required int Order { get; init; }

    /// <summary>e.g. wash, condition, treatment, finish, weekly.</summary>
    public required string Phase { get; init; }

    /// <summary>Human phrase, e.g. "3x por semana", "diariamente".</summary>
    public required string Frequency { get; init; }

    /// <summary>Referenced catalog product, when the step recommends one.</summary>
    public int? ProductId { get; init; }

    public required string How { get; init; }
    public required string Why { get; init; }
}

/// <summary>Output of an <c>IRoutineGenerator</c>: the routine plus provenance.</summary>
public sealed record RoutineResult
{
    public required HairRoutine Routine { get; init; }

    /// <summary>Generator model id (e.g. "claude-sonnet-5") or "template".</summary>
    public required string Model { get; init; }

    /// <summary>True when served from cache.</summary>
    public bool FromCache { get; init; }
}
