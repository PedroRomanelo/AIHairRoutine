using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Matching;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Application.Profiling;
using AIHairRoutine.Application.Services;
using AIHairRoutine.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AIHairRoutine.Application;

public static class DependencyInjection
{
    /// <summary>Registers the pure domain services (profiling rules, matching, facade, validators).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<RuleBasedProfiler>();
        services.AddSingleton<IProductMatcher, ProductMatcher>();
        services.AddScoped<IDiagnosisService, DiagnosisService>();
        services.AddScoped<IValidator<HairAssessment>, HairAssessmentValidator>();
        return services;
    }
}
