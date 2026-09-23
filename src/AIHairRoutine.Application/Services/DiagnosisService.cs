using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using Microsoft.Extensions.Logging;

namespace AIHairRoutine.Application.Services;

/// <summary>
/// Facade for the whole diagnosis pipeline. One call hides the orchestration:
/// profile → match products → generate routine → assemble the response.
/// </summary>
public sealed class DiagnosisService(
    IHairProfiler profiler,
    IProductCatalog catalog,
    IProductMatcher matcher,
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
        var matches = matcher.Match(profile.Profile, profile.Priorities, products);

        var routine = await generator.GenerateAsync(profile, matches, assessment.Locale, ct);

        return new DiagnosisResult
        {
            Profile = profile.Profile,
            Priorities = profile.Priorities,
            RecommendedProducts = matches
                .Select(m => new RecommendedProduct
                {
                    Id = m.Product.Id,
                    Name = m.Product.Name,
                    Brand = m.Product.Brand,
                    Category = m.Product.Category,
                    Targets = m.Product.Targets,
                    MatchScore = m.Score,
                })
                .ToList(),
            Routine = routine.Routine,
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
}
