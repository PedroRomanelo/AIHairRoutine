using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Matching;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Application.Profiling;
using AIHairRoutine.Application.Scheduling;
using AIHairRoutine.Application.Services;
using AIHairRoutine.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AIHairRoutine.Application;

public static class DependencyInjection
{
    /// <summary>Registers the pure domain services (profiling rules, selection, scheduling, facade, validators).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ChemicalTimingParser>();
        services.AddSingleton<RuleBasedProfiler>();
        services.AddSingleton<IProductSelector, ProductSelector>();
        services.AddSingleton<IScheduleBuilder, ScheduleBuilder>();
        services.AddScoped<IDiagnosisService, DiagnosisService>();
        services.AddScoped<IValidator<HairAssessment>, HairAssessmentValidator>();
        return services;
    }
}
