using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Profiling;
using AIHairRoutine.Infrastructure.Claude;
using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Data;
using AIHairRoutine.Infrastructure.Jev;
using AIHairRoutine.Infrastructure.Profiling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIHairRoutine.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Wires JEV, Claude, data access, caching and resilience. External calls degrade gracefully.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var jev = Bind<JevOptions>(services, config, JevOptions.SectionName);
        var anthropic = Bind<AnthropicOptions>(services, config, AnthropicOptions.SectionName);
        Bind<DatabaseOptions>(services, config, DatabaseOptions.SectionName);
        Bind<RoutineCacheOptions>(services, config, RoutineCacheOptions.SectionName);

        services.AddHybridCache();

        AddProfiling(services, jev);
        AddGeneration(services, anthropic);
        AddData(services);

        return services;
    }

    // --- Profiling (rules + optional JEV, composed by the hybrid strategy) ---

    private static void AddProfiling(IServiceCollection services, JevOptions jev)
    {
        if (jev.Enabled)
        {
            services.AddHttpClient<JevClient>(c =>
            {
                c.BaseAddress = new Uri(jev.BaseUrl);
                c.DefaultRequestHeaders.Add("Authorization", $"Bearer {jev.ApiKey}");
                c.Timeout = Timeout.InfiniteTimeSpan; // resilience handler owns the timeouts
            })
            .AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(jev.TimeoutSeconds);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(jev.TimeoutSeconds * 3);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(jev.TimeoutSeconds * 2);
                o.Retry.MaxRetryAttempts = 2;
            });

            services.AddTransient<JevProfiler>();
        }

        // Hybrid resolves JevProfiler optionally (null when JEV is disabled).
        services.AddScoped<IHairProfiler>(sp => new HybridProfiler(
            sp.GetRequiredService<RuleBasedProfiler>(),
            sp.GetRequiredService<ILogger<HybridProfiler>>(),
            sp.GetService<JevProfiler>()));
    }

    // --- Generation (Claude primary + template fallback, wrapped by caching) ---

    private static void AddGeneration(IServiceCollection services, AnthropicOptions anthropic)
    {
        services.AddSingleton<ClaudePromptBuilder>();
        services.AddSingleton<TemplateRoutineGenerator>();

        if (anthropic.Enabled)
        {
            services.AddHttpClient<ClaudeRoutineGenerator>(c =>
            {
                c.BaseAddress = new Uri(anthropic.BaseUrl);
                c.DefaultRequestHeaders.Add("x-api-key", anthropic.ApiKey);
                c.DefaultRequestHeaders.Add("anthropic-version", anthropic.AnthropicVersion);
                c.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(anthropic.TimeoutSeconds);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(anthropic.TimeoutSeconds * 2 + 10);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(anthropic.TimeoutSeconds * 2);
                o.Retry.MaxRetryAttempts = 1; // LLM calls are costly; retry sparingly
            });
        }

        services.AddScoped<IRoutineGenerator>(sp =>
        {
            var template = sp.GetRequiredService<TemplateRoutineGenerator>();
            var claude = sp.GetService<ClaudeRoutineGenerator>();

            IRoutineGenerator core = claude is not null
                ? new FallbackRoutineGenerator(claude, template, sp.GetRequiredService<ILogger<FallbackRoutineGenerator>>())
                : template;

            return new CachingRoutineGenerator(
                core,
                sp.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RoutineCacheOptions>>());
        });
    }

    // --- Data access ---

    private static void AddData(IServiceCollection services)
    {
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<IProductCatalog, CachedProductCatalog>();
        services.AddSingleton<DatabaseMigrator>();
    }

    private static T Bind<T>(IServiceCollection services, IConfiguration config, string section) where T : class, new()
    {
        var configSection = config.GetSection(section);
        services.AddOptions<T>().Bind(configSection).ValidateOnStart();
        return configSection.Get<T>() ?? new T();
    }
}
