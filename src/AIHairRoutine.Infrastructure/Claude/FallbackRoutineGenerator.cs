using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using Microsoft.Extensions.Logging;

namespace AIHairRoutine.Infrastructure.Claude;

/// <summary>
/// Tries the primary generator (Claude); on any failure degrades gracefully to the fallback
/// (template). Keeps the endpoint responsive even when the LLM is down or rate-limited.
/// </summary>
public sealed class FallbackRoutineGenerator(
    IRoutineGenerator primary,
    IRoutineGenerator fallback,
    ILogger<FallbackRoutineGenerator> logger) : IRoutineGenerator
{
    public async Task<RoutineResult> GenerateAsync(
        ProfileResult profile,
        IReadOnlyList<ProductMatch> products,
        string locale,
        CancellationToken ct = default)
    {
        try
        {
            return await primary.GenerateAsync(profile, products, locale, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Primary routine generator failed; using template fallback.");
            return await fallback.GenerateAsync(profile, products, locale, ct);
        }
    }
}
