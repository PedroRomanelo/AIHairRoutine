using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using Microsoft.Extensions.Logging;

namespace AIHairRoutine.Application.Services;

/// <summary>
/// Facade for the whole diagnosis pipeline. One call hides the orchestration:
/// profile → select products → build the 4-week schedule → write the narrative → assemble the response.
/// </summary>
public sealed class DiagnosisService(
    IHairProfiler profiler,
    IProductCatalog catalog,
    IProductSelector selector,
    IScheduleBuilder scheduleBuilder,
    IRoutineGenerator generator,
    ILogger<DiagnosisService> logger) : IDiagnosisService
{
    public async Task<DiagnosisResult> DiagnoseAsync(HairAssessment assessment, CancellationToken ct = default)
    {
        var profile = await profiler.ProfileAsync(assessment, ct);
        logger.LogInformation(
            "Profiled hair via {Source} (confidence {Confidence:0.00}); priorities: {Priorities}",
            profile.Source, profile.Confidence, string.Join(",", profile.Priorities));

        var products = await catalog.GetActiveAsync(ct);
        var selection = selector.Select(profile.Profile, profile.Priorities, products);
        var schedule = scheduleBuilder.Build(profile.Profile, selection.Eligible, assessment.Locale);

        var used = UsedProducts(schedule, selection.Eligible);
        var routine = await generator.GenerateAsync(
            profile, schedule, used.Select(m => m.Product).ToList(), assessment.Locale, ct);

        return new DiagnosisResult
        {
            Profile = profile.Profile,
            Priorities = profile.Priorities,
            Schedule = ApplyNarrative(schedule, routine.Narrative),
            RecommendedProducts = used
                .Select(m => new RecommendedProduct
                {
                    Id = m.Product.Id,
                    Name = m.Product.Name,
                    Brand = m.Product.Brand,
                    Category = m.Product.Category,
                    Targets = m.Product.Targets,
                    TreatmentTypes = m.Product.TreatmentTypes,
                    UsageFrequency = m.Product.UsageFrequency,
                    ActionTimeMinutes = m.Product.ActionTimeMinutes,
                    Price = m.Product.Price,
                    SizeMl = m.Product.SizeMl,
                    MatchScore = m.Score,
                })
                .ToList(),
            ExcludedProducts = selection.Excluded,
            Meta = new DiagnosisMeta
            {
                ProfileSource = profile.Source,
                Cached = routine.FromCache,
                Confidence = profile.Confidence,
                Models = new ModelInfo
                {
                    Profiler = profile.Model,
                    Generator = routine.Model,
                },
            },
        };
    }

    /// <summary>Products that made it into the schedule, in order of first appearance.</summary>
    private static List<ProductMatch> UsedProducts(HairSchedule schedule, IReadOnlyList<ProductMatch> eligible)
    {
        var byId = eligible.ToDictionary(m => m.Product.Id);
        return schedule.Weeks
            .SelectMany(w => w.Days)
            .SelectMany(d => d.Steps)
            .Where(s => s.ProductId is not null)
            .Select(s => s.ProductId!.Value)
            .Distinct()
            .Select(id => byId[id])
            .ToList();
    }

    /// <summary>Merges the generated texts into the deterministic calendar.</summary>
    private static HairSchedule ApplyNarrative(HairSchedule schedule, RoutineNarrative narrative)
    {
        var notes = narrative.ProductNotes
            .GroupBy(n => n.ProductId)
            .ToDictionary(g => g.Key, g => g.First());

        return schedule with
        {
            Summary = narrative.Summary,
            Tips = narrative.Tips,
            Weeks = schedule.Weeks
                .Select(w => w with
                {
                    Days = w.Days
                        .Select(d => d with
                        {
                            Steps = d.Steps
                                .Select(s => s.ProductId is { } id && notes.TryGetValue(id, out var note)
                                    ? s with { How = note.How, Why = note.Why }
                                    : s)
                                .ToList(),
                        })
                        .ToList(),
                })
                .ToList(),
        };
    }
}
