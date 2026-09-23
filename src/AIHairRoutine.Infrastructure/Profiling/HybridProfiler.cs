using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Application.Profiling;
using AIHairRoutine.Infrastructure.Jev;
using Microsoft.Extensions.Logging;

namespace AIHairRoutine.Infrastructure.Profiling;

/// <summary>
/// Strategy selector: uses deterministic rules for the common case and JEV only when the input
/// is ambiguous or has free text. If JEV fails or times out, it degrades gracefully to rules.
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

    /// <summary>JEV earns its cost when the signal is fuzzy: free text, "Other" chemistry, or conflicting scores.</summary>
    private static bool ShouldUseJev(HairAssessment a)
    {
        if (!string.IsNullOrWhiteSpace(a.Notes))
            return true;
        if (a.ChemicalTreatment == ChemicalTreatment.Other)
            return true;

        // Conflicting oiliness vs dryness in the mid range is genuinely ambiguous for simple rules.
        var c = a.Concerns;
        bool conflicting = c.Dryness is >= 4 and <= 7 && c.Oiliness is >= 4 and <= 7;
        return conflicting;
    }
}
