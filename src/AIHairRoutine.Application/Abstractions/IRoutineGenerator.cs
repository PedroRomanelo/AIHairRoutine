using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Abstractions;

/// <summary>
/// Generates the final routine from a profile and matched products.
/// Implementations: Claude (adapter), template (fallback), caching decorator.
/// </summary>
public interface IRoutineGenerator
{
    Task<RoutineResult> GenerateAsync(
        ProfileResult profile,
        IReadOnlyList<ProductMatch> products,
        string locale,
        CancellationToken ct = default);
}
