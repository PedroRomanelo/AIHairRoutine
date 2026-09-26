using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Profiling;

/// <summary>
/// Deterministic profiler. Turns the questionnaire into a typed profile, weighted and ordered
/// priorities, and the H/N/R treatment needs that drive the 4-week cycle. This is the default
/// strategy, the baseline JEV refines and the JEV fallback.
/// </summary>
public sealed class RuleBasedProfiler(ChemicalTimingParser timing) : IHairProfiler
{
    /// <summary>Minimum weight for a priority to be included.</summary>
    private const int PriorityThreshold = 3;

    private const int MaxPriorities = 4;

    public Task<ProfileResult> ProfileAsync(HairAssessment a, CancellationToken ct = default)
        => Task.FromResult(Profile(a));

    public ProfileResult Profile(HairAssessment a)
    {
        var goal = GoalKeywordExtractor.Extract(a.MainGoal);
        var hairType = a.HairType.GetValueOrDefault();
        var thickness = a.Thickness.GetValueOrDefault(HairThickness.Medium);
        var conditions = (a.Conditions ?? []).Distinct().ToList();
        var chemical = BuildChemicalStatus(a.Chemical);

        var profile = new HairProfile
        {
            HairType = hairType,
            Thickness = thickness,
            Tone = a.Tone.GetValueOrDefault(),
            Conditions = conditions,
            Chemical = chemical,
            TreatmentNeeds = ComputeTreatmentNeeds(hairType, thickness, conditions, chemical, goal),
            Allergies = (a.Allergies ?? []).Distinct().ToList(),
        };

        return new ProfileResult
        {
            Profile = profile,
            Priorities = RankPriorities(profile, goal),
            Source = ProfileSource.Rules,
            Model = "rules",
            Confidence = 1.0,
        };
    }

    private ChemicalStatus BuildChemicalStatus(ChemicalHistory? chemical)
    {
        if (chemical is not { HasChemical: true })
            return ChemicalStatus.None;

        return new ChemicalStatus
        {
            HasChemical = true,
            Type = chemical.Type ?? ChemicalType.Other,
            DaysSince = timing.TryParseDaysSince(chemical.Performed, out var days) ? days : null,
            TouchUpFrequency = chemical.TouchUpFrequency,
        };
    }

    /// <summary>Weighs each priority from conditions, hair type, chemistry and the stated goal.</summary>
    public static IReadOnlyList<HairPriority> RankPriorities(HairProfile p, IReadOnlyList<HairPriority> goal)
    {
        var weights = new Dictionary<HairPriority, int>();
        void Add(HairPriority priority, int weight) => weights[priority] = weights.GetValueOrDefault(priority) + weight;

        foreach (var condition in p.Conditions)
        {
            switch (condition)
            {
                case HairCondition.Dry: Add(HairPriority.Hydration, 4); Add(HairPriority.Nutrition, 2); break;
                case HairCondition.Oily: Add(HairPriority.OilControl, 4); break;
                case HairCondition.Damaged: Add(HairPriority.Reconstruction, 4); break;
                case HairCondition.Frizzy: Add(HairPriority.FrizzControl, 4); Add(HairPriority.Nutrition, 1); Add(HairPriority.Hydration, 1); break;
                case HairCondition.Dull: Add(HairPriority.Shine, 4); Add(HairPriority.Nutrition, 2); break;
            }
        }

        if (p.HairType is HairType.Curly or HairType.Coily)
        {
            Add(HairPriority.Hydration, 2);
            Add(HairPriority.Nutrition, 2);
        }
        else if (p.HairType == HairType.Wavy)
        {
            Add(HairPriority.Hydration, 1);
        }

        if (p.Thickness == HairThickness.Fine)
            Add(HairPriority.Volume, 1);
        else if (p.Thickness == HairThickness.Coarse)
            Add(HairPriority.Nutrition, 1);

        if (p.Chemical.IsRecent)
        {
            Add(HairPriority.Reconstruction, p.Chemical.Type == ChemicalType.Bleaching ? 5 : 3);
            Add(HairPriority.Hydration, 1);
        }
        else if (p.Chemical.HasChemical)
        {
            Add(HairPriority.Reconstruction, 1);
        }

        // The stated goal is the strongest signal; earlier mentions weigh more.
        for (int i = 0; i < goal.Count; i++)
            Add(goal[i], Math.Max(5 - i, 2));

        var ordered = weights
            .Where(kv => kv.Value >= PriorityThreshold)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => kv.Key)
            .ToList();

        // Always return at least two priorities so the schedule has focus.
        foreach (var fallback in weights.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => kv.Key)
                     .Concat([HairPriority.Hydration, HairPriority.Nutrition]))
        {
            if (ordered.Count >= 2)
                break;
            if (!ordered.Contains(fallback))
                ordered.Add(fallback);
        }

        return ordered.Take(MaxPriorities).ToList();
    }

    /// <summary>Base H/N/R weights by hair type, adjusted by condition, thickness, chemistry and goal.</summary>
    public static TreatmentNeeds ComputeTreatmentNeeds(
        HairType hairType,
        HairThickness thickness,
        IReadOnlyList<HairCondition> conditions,
        ChemicalStatus chemical,
        IReadOnlyList<HairPriority> goal)
    {
        var (h, n, r) = hairType switch
        {
            HairType.Wavy => (2, 2, 1),
            HairType.Curly => (3, 2, 1),
            HairType.Coily => (3, 3, 1),
            _ => (2, 1, 1),
        };

        if (conditions.Contains(HairCondition.Dry)) h += 2;
        if (conditions.Contains(HairCondition.Frizzy)) { n += 1; h += 1; }
        if (conditions.Contains(HairCondition.Dull)) n += 2;
        if (conditions.Contains(HairCondition.Damaged)) r += 2;
        if (conditions.Contains(HairCondition.Oily)) { h -= 1; n -= 1; }

        if (thickness == HairThickness.Fine) n -= 1;
        else if (thickness == HairThickness.Coarse) n += 1;

        if (chemical.IsRecent)
            r += chemical.Type == ChemicalType.Bleaching ? 3 : 2;

        if (goal.Contains(HairPriority.Hydration)) h += 1;
        if (goal.Contains(HairPriority.Nutrition)) n += 1;
        if (goal.Contains(HairPriority.Reconstruction)) r += 1;

        return TreatmentNeeds.FromWeights(Math.Max(h, 1), Math.Max(n, 0), Math.Max(r, 0));
    }
}
