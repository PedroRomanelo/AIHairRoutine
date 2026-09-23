using System.Text.Json;
using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Jev;

/// <summary>
/// Adapter: turns a <see cref="HairAssessment"/> into JEV questions, calls JEV, and maps the
/// typed answers back into a domain <see cref="ProfileResult"/>. Priorities are derived from
/// the ranked "needs_*" noul probabilities.
/// </summary>
public sealed class JevProfiler(JevClient client, IOptions<JevOptions> options, ILogger<JevProfiler> logger)
    : IHairProfiler
{
    private static readonly IReadOnlyDictionary<string, string> ConditionCriteria = new Dictionary<string, string>
    {
        ["dry"] = "Fios/couro ressecados, sem oleosidade",
        ["normal"] = "Equilíbrio normal de oleosidade",
        ["oily"] = "Oleosidade acentuada na raiz",
    };

    private static readonly string[] DamageCriteria = ["none", "mild", "moderate", "severe"];
    private static readonly string[] FrizzCriteria = ["low", "medium", "high"];

    private static readonly (string Question, HairPriority Priority)[] NeedQuestions =
    [
        ("needs_hydration", HairPriority.Hydration),
        ("needs_frizz_control", HairPriority.FrizzControl),
        ("needs_damage_repair", HairPriority.DamageRepair),
        ("needs_oil_control", HairPriority.OilControl),
        ("needs_hairloss_control", HairPriority.HairLossControl),
    ];

    public async Task<ProfileResult> ProfileAsync(HairAssessment a, CancellationToken ct = default)
    {
        var state = BuildState(a);
        var builder = new JevRequestBuilder()
            .WithModel(options.Value.Model)
            .WithState(state)
            .AddChoice("condition", "Condição geral de oleosidade do fio e couro cabeludo", ConditionCriteria)
            .AddScore("damageLevel", "Nível de dano estrutural do fio", DamageCriteria)
            .AddScore("frizzLevel", "Intensidade do frizz", FrizzCriteria);

        builder.AddNoul("needs_hydration", "O cabelo precisa de foco em hidratação");
        builder.AddNoul("needs_frizz_control", "O cabelo precisa de controle de frizz");
        builder.AddNoul("needs_damage_repair", "O cabelo precisa de reparação de dano/reconstrução");
        builder.AddNoul("needs_oil_control", "O cabelo precisa de controle de oleosidade");
        builder.AddNoul("needs_hairloss_control", "O cabelo precisa de cuidado contra queda");

        var response = await client.EvaluateAsync(builder.Build(), ct)
            ?? throw new InvalidOperationException("JEV returned an empty response.");

        return Map(a, response);
    }

    private static Dictionary<string, object?> BuildState(HairAssessment a) => new()
    {
        ["hairType"] = a.HairType.ToString().ToLowerInvariant(),
        ["chemical"] = a.ChemicalTreatment.ToString().ToLowerInvariant(),
        ["colorTreated"] = a.ColorTreated,
        ["dryness"] = a.Concerns.Dryness,
        ["frizz"] = a.Concerns.Frizz,
        ["breakage"] = a.Concerns.Breakage,
        ["oiliness"] = a.Concerns.Oiliness,
        ["hairLoss"] = a.Concerns.HairLoss,
        ["notes"] = a.Notes ?? string.Empty,
    };

    private ProfileResult Map(HairAssessment a, JevResponse response)
    {
        var ans = response.Answers;
        var confidences = new List<double>();

        var condition = MapChoice(ans, "condition", confidences) switch
        {
            "dry" => HairCondition.Dry,
            "oily" => HairCondition.Oily,
            _ => HairCondition.Normal,
        };

        var damage = MapScore(ans, "damageLevel", DamageCriteria, confidences) switch
        {
            "severe" => DamageLevel.Severe,
            "moderate" => DamageLevel.Moderate,
            "mild" => DamageLevel.Mild,
            _ => DamageLevel.None,
        };

        var frizz = MapScore(ans, "frizzLevel", FrizzCriteria, confidences) switch
        {
            "high" => FrizzLevel.High,
            "medium" => FrizzLevel.Medium,
            _ => FrizzLevel.Low,
        };

        bool chemical = a.ChemicalTreatment != ChemicalTreatment.None || a.ColorTreated;

        var profile = new HairProfile
        {
            HairType = a.HairType,
            Condition = condition,
            DamageLevel = damage,
            FrizzLevel = frizz,
            ChemicalTreatment = chemical,
        };

        var priorities = RankPriorities(ans);
        double confidence = confidences.Count > 0 ? Math.Round(confidences.Average(), 2) : 0.7;

        return new ProfileResult
        {
            Profile = profile,
            Priorities = priorities,
            Source = ProfileSource.Jev,
            Model = response.Model ?? "jev",
            Confidence = confidence,
        };
    }

    private static string? MapChoice(Dictionary<string, JsonElement> ans, string key, List<double> confidences)
    {
        if (!ans.TryGetValue(key, out var el))
            return null;
        if (el.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number)
            confidences.Add(c.GetDouble());
        return el.TryGetProperty("choice", out var choice) ? choice.GetString() : null;
    }

    private static string MapScore(Dictionary<string, JsonElement> ans, string key, string[] criteria, List<double> confidences)
    {
        if (!ans.TryGetValue(key, out var el) || !el.TryGetProperty("score", out var scoreEl))
            return criteria[0];
        if (el.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number)
            confidences.Add(c.GetDouble());

        double score = scoreEl.GetDouble();
        int index = Math.Clamp((int)Math.Round(score), 0, criteria.Length - 1);
        return criteria[index];
    }

    private IReadOnlyList<HairPriority> RankPriorities(Dictionary<string, JsonElement> ans)
    {
        var scored = new List<(HairPriority Priority, double Prob)>();
        foreach (var (question, priority) in NeedQuestions)
        {
            if (ans.TryGetValue(question, out var el) && el.TryGetProperty("noul", out var p) && p.ValueKind == JsonValueKind.Number)
                scored.Add((priority, p.GetDouble()));
        }

        var ordered = scored.OrderByDescending(x => x.Prob).ToList();
        var picked = ordered.Where(x => x.Prob > 0.5).Select(x => x.Priority).ToList();

        if (picked.Count < 2)
            picked = ordered.Take(2).Select(x => x.Priority).ToList();

        if (picked.Count == 0)
        {
            logger.LogWarning("JEV returned no priority signals; defaulting to hydration.");
            picked = [HairPriority.Hydration];
        }

        return picked.Take(4).ToList();
    }
}
