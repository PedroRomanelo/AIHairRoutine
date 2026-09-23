using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Profiling;

/// <summary>
/// Deterministic profiler. Maps the 0..10 scores to a typed profile and a weighted,
/// ordered priority list. This is the default strategy and also the JEV fallback.
/// </summary>
public sealed class RuleBasedProfiler : IHairProfiler
{
    /// <summary>Minimum weight for a priority to be included.</summary>
    private const int PriorityThreshold = 4;

    public Task<ProfileResult> ProfileAsync(HairAssessment a, CancellationToken ct = default)
        => Task.FromResult(Profile(a));

    public ProfileResult Profile(HairAssessment a)
    {
        var c = a.Concerns;
        bool chemical = a.ChemicalTreatment != ChemicalTreatment.None || a.ColorTreated;

        var condition = c.Oiliness >= 7 ? HairCondition.Oily
            : c.Dryness >= 6 ? HairCondition.Dry
            : HairCondition.Normal;

        int damageScore = c.Breakage + (chemical ? 3 : 0) + (a.ColorTreated ? 2 : 0);
        var damage = damageScore >= 8 ? DamageLevel.Severe
            : damageScore >= 5 ? DamageLevel.Moderate
            : damageScore >= 2 ? DamageLevel.Mild
            : DamageLevel.None;

        var frizz = c.Frizz >= 7 ? FrizzLevel.High
            : c.Frizz >= 4 ? FrizzLevel.Medium
            : FrizzLevel.Low;

        var profile = new HairProfile
        {
            HairType = a.HairType,
            Condition = condition,
            DamageLevel = damage,
            FrizzLevel = frizz,
            ChemicalTreatment = chemical,
        };

        var priorities = RankPriorities(a, condition, damageScore);

        return new ProfileResult
        {
            Profile = profile,
            Priorities = priorities,
            Source = ProfileSource.Rules,
            Model = "rules",
            Confidence = 1.0,
        };
    }

    /// <summary>Weighs each priority and returns them ordered desc, keeping meaningful ones.</summary>
    public static IReadOnlyList<HairPriority> RankPriorities(HairAssessment a, HairCondition condition, int damageScore)
    {
        var c = a.Concerns;
        var weights = new Dictionary<HairPriority, int>
        {
            [HairPriority.Hydration] = c.Dryness + (condition == HairCondition.Dry ? 2 : 0),
            [HairPriority.FrizzControl] = c.Frizz,
            [HairPriority.DamageRepair] = damageScore,
            [HairPriority.OilControl] = c.Oiliness,
            [HairPriority.HairLossControl] = c.HairLoss,
        };

        var ordered = weights
            .Where(kv => kv.Value >= PriorityThreshold)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();

        // Always return at least the two strongest signals so the routine has focus.
        if (ordered.Count < 2)
        {
            ordered = weights
                .OrderByDescending(kv => kv.Value)
                .Take(2)
                .Select(kv => kv.Key)
                .ToList();
        }

        return ordered.Take(4).ToList();
    }
}
