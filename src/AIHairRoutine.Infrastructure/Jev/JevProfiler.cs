using System.Text.Json;
using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Application.Profiling;
using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Jev;

/// <summary>
/// Adapter: sends the questionnaire to JEV and uses its ranked "needs_*" probabilities to refine the
/// priorities. The typed profile itself (conditions, chemistry, H/N/R needs) comes from the rules,
/// since those answers are already explicit in the questionnaire.
/// </summary>
public sealed class JevProfiler(
    JevClient client,
    RuleBasedProfiler rules,
    IOptions<JevOptions> options,
    ILogger<JevProfiler> logger) : IHairProfiler
{
    private static readonly (string Question, HairPriority Priority, string Instructions)[] NeedQuestions =
    [
        ("needs_hydration", HairPriority.Hydration, "O cabelo precisa de foco em hidratação (reposição de água)"),
        ("needs_nutrition", HairPriority.Nutrition, "O cabelo precisa de nutrição (reposição de lipídios/óleos)"),
        ("needs_reconstruction", HairPriority.Reconstruction, "O cabelo precisa de reconstrução (reposição de massa/proteínas)"),
        ("needs_frizz_control", HairPriority.FrizzControl, "O cabelo precisa de controle de frizz"),
        ("needs_oil_control", HairPriority.OilControl, "O cabelo precisa de controle de oleosidade"),
        ("needs_shine", HairPriority.Shine, "O cabelo precisa de brilho"),
        ("needs_hairloss_control", HairPriority.HairLossControl, "O cabelo precisa de cuidado contra queda"),
        ("needs_volume", HairPriority.Volume, "O cabelo precisa de volume"),
    ];

    public async Task<ProfileResult> ProfileAsync(HairAssessment a, CancellationToken ct = default)
    {
        var baseline = rules.Profile(a);

        var builder = new JevRequestBuilder()
            .WithModel(options.Value.Model)
            .WithState(BuildState(baseline.Profile, a.MainGoal));
        foreach (var (question, _, instructions) in NeedQuestions)
            builder.AddNoul(question, instructions);

        var response = await client.EvaluateAsync(builder.Build(), ct)
            ?? throw new InvalidOperationException("JEV returned an empty response.");

        var confidences = new List<double>();
        var priorities = RankPriorities(response.Answers, confidences);
        if (priorities.Count == 0)
        {
            logger.LogWarning("JEV returned no priority signals; keeping rule-based priorities.");
            priorities = baseline.Priorities;
        }

        return baseline with
        {
            Priorities = priorities,
            Source = ProfileSource.Jev,
            Model = response.Model ?? "jev",
            Confidence = confidences.Count > 0 ? Math.Round(confidences.Average(), 2) : 0.7,
        };
    }

    private static Dictionary<string, object?> BuildState(HairProfile p, string? goal) => new()
    {
        ["hairType"] = p.HairType.ToString().ToLowerInvariant(),
        ["thickness"] = p.Thickness.ToString().ToLowerInvariant(),
        ["conditions"] = p.Conditions.Select(c => c.ToString().ToLowerInvariant()).ToList(),
        ["chemical"] = p.Chemical.HasChemical ? p.Chemical.Type?.ToString().ToLowerInvariant() : "none",
        ["chemicalDaysAgo"] = p.Chemical.DaysSince,
        ["mainGoal"] = goal ?? string.Empty,
    };

    private static IReadOnlyList<HairPriority> RankPriorities(Dictionary<string, JsonElement> answers, List<double> confidences)
    {
        var scored = new List<(HairPriority Priority, double Prob)>();
        foreach (var (question, priority, _) in NeedQuestions)
        {
            if (!answers.TryGetValue(question, out var el) || !el.TryGetProperty("noul", out var p) || p.ValueKind != JsonValueKind.Number)
                continue;

            scored.Add((priority, p.GetDouble()));
            if (el.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number)
                confidences.Add(c.GetDouble());
        }

        var ordered = scored.OrderByDescending(x => x.Prob).ToList();
        var picked = ordered.Where(x => x.Prob > 0.5).Select(x => x.Priority).ToList();

        if (picked.Count < 2)
            picked = ordered.Take(2).Select(x => x.Priority).ToList();

        return picked.Take(4).ToList();
    }
}
