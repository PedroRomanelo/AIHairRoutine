namespace AIHairRoutine.Application.Models;

/// <summary>
/// The 4-week hair schedule (cronograma capilar). The calendar is computed deterministically;
/// <see cref="Summary"/>, <see cref="Tips"/> and each step's how/why come from the narrative generator.
/// </summary>
public sealed record HairSchedule
{
    public string Summary { get; init; } = string.Empty;
    public required IReadOnlyList<DayOfWeek> WashDays { get; init; }

    /// <summary>Wash days that carry the H/N/R treatment slot.</summary>
    public required IReadOnlyList<DayOfWeek> TreatmentDays { get; init; }

    public required IReadOnlyList<ScheduleWeek> Weeks { get; init; }

    /// <summary>Grouped view ("Diário", "2x por semana (seg e qui)"...).</summary>
    public required IReadOnlyList<ScheduleGroup> Overview { get; init; }

    public required IReadOnlyList<string> SpecialCare { get; init; }
    public IReadOnlyList<string> Tips { get; init; } = [];
}

public sealed record ScheduleWeek
{
    public required int Number { get; init; }

    /// <summary>H/N/R axes treated this week, in slot order.</summary>
    public required IReadOnlyList<TreatmentType> Focus { get; init; }

    public required IReadOnlyList<ScheduleDay> Days { get; init; }
}

public sealed record ScheduleDay
{
    public required DayOfWeek Day { get; init; }
    public required IReadOnlyList<ScheduleStep> Steps { get; init; }
}

public sealed record ScheduleStep
{
    public required int Order { get; init; }

    /// <summary>Null when no catalog product fits and the step is generic.</summary>
    public Guid? ProductId { get; init; }

    public required string ProductName { get; init; }
    public required ProductCategory Category { get; init; }

    /// <summary>The H/N/R axis, for treatment-slot steps.</summary>
    public TreatmentType? Treatment { get; init; }

    public int? ActionMinutes { get; init; }
    public string How { get; init; } = string.Empty;
    public string Why { get; init; } = string.Empty;
}

public sealed record ScheduleGroup
{
    public required string Label { get; init; }
    public required IReadOnlyList<DayOfWeek> Days { get; init; }
    public required IReadOnlyList<ScheduleGroupItem> Items { get; init; }
}

public sealed record ScheduleGroupItem
{
    public Guid? ProductId { get; init; }
    public required string Name { get; init; }
    public int? ActionMinutes { get; init; }
    public IReadOnlyList<TreatmentType> Treatments { get; init; } = [];

    /// <summary>Weeks of the cycle (1..4) in which the item is used.</summary>
    public required IReadOnlyList<int> Weeks { get; init; }
}

/// <summary>Text written for a schedule: summary, per-product how/why and tips.</summary>
public sealed record RoutineNarrative
{
    public required string Summary { get; init; }
    public required IReadOnlyList<ProductNote> ProductNotes { get; init; }
    public IReadOnlyList<string> Tips { get; init; } = [];
}

public sealed record ProductNote(Guid ProductId, string How, string Why);

/// <summary>Output of an <c>IRoutineGenerator</c>: the narrative plus provenance.</summary>
public sealed record RoutineResult
{
    public required RoutineNarrative Narrative { get; init; }

    /// <summary>Generator model id (e.g. "claude-sonnet-5") or "template".</summary>
    public required string Model { get; init; }

    /// <summary>True when served from cache.</summary>
    public bool FromCache { get; init; }
}
