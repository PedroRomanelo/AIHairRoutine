using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Abstractions;

/// <summary>Builds the deterministic 4-week H/N/R calendar from the profile and the eligible products.</summary>
public interface IScheduleBuilder
{
    HairSchedule Build(HairProfile profile, IReadOnlyList<ProductMatch> eligible, string locale);
}
