using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Application.Profiling;
using AIHairRoutine.Infrastructure.Jev;
using Microsoft.Extensions.Logging;

namespace AIHairRoutine.Infrastructure.Profiling;

/// <summary>
/// Strategy selector: uses deterministic rules for the common case and JEV only when the input
/// is ambiguous. If JEV fails or times out, it degrades gracefully to rules.
/// </summary>
public sealed class HybridProfiler(
    RuleBasedProfiler rules,
    ILogger<HybridProfiler> logger,
    JevProfiler? jev = null) : IHairProfiler
{
    public async Task<ProfileResult> ProfileAsync(HairAssessment a, CancellationToken ct = default)
    {
        if (jev is null || !ShouldUseJev(a))
            return await rules.ProfileAsync(a, ct);

        try
        {
            return await jev.ProfileAsync(a, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "JEV profiling failed; falling back to rule-based profiler.");
            return await rules.ProfileAsync(a, ct);
        }
    }

    /// <summary>
    /// JEV earns its cost when the signal is fuzzy: an unspecified ("other") chemical, or a goal
    /// written in words the keyword rules don't recognize.
    /// </summary>
    private static bool ShouldUseJev(HairAssessment a) =>
        a.Chemical is { HasChemical: true, Type: ChemicalType.Other }
        || (!string.IsNullOrWhiteSpace(a.MainGoal) && GoalKeywordExtractor.Extract(a.MainGoal).Count == 0);
}
