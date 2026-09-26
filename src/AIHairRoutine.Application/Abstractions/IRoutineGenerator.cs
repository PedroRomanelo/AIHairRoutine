using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Abstractions;

/// <summary>
/// Writes the narrative for an already-built schedule: summary, how/why for each product and tips.
/// The calendar itself (days, frequencies, H/N/R cycle) is deterministic; generators only add text.
/// Implementations: AI provider (adapter), template (fallback), caching decorator.
/// </summary>
public interface IRoutineGenerator
{
    Task<RoutineResult> GenerateAsync(
        ProfileResult profile,
        HairSchedule schedule,
        IReadOnlyList<Product> products,
        string locale,
        CancellationToken ct = default);
}
