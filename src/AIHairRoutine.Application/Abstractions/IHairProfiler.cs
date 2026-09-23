using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Abstractions;

/// <summary>
/// Strategy that turns a raw assessment into a typed profile + ordered priorities.
/// Implementations: rule-based (deterministic), JEV (adapter), hybrid (composes both).
/// </summary>
public interface IHairProfiler
{
    Task<ProfileResult> ProfileAsync(HairAssessment assessment, CancellationToken ct = default);
}
