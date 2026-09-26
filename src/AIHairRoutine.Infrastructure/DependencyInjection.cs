using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Profiling;
using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Data;
using AIHairRoutine.Infrastructure.Generation;
using AIHairRoutine.Infrastructure.Generation.Providers;
using AIHairRoutine.Infrastructure.Generation.Providers.Factories;
using AIHairRoutine.Infrastructure.Jev;
using AIHairRoutine.Infrastructure.Profiling;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Wires JEV, Claude, data access, caching and resilience. External calls degrade gracefully.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var jev = Bind<JevOptions>(services, config, JevOptions.SectionName);
        var generation = Bind<GenerationOptions>(services, config, GenerationOptions.SectionName);
        Bind<DatabaseOptions>(services, config, DatabaseOptions.SectionName);
        Bind<RoutineCacheOptions>(services, config, RoutineCacheOptions.SectionName);

        services.AddHybridCache();

        AddProfiling(services, jev);
        AddGeneration(services, generation);
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

    // --- Generation (active AI provider primary + template fallback, wrapped by caching) ---
    //
    // Patterns:
    //   Strategy         → IRoutineGenerator (interchangeable generation algorithms) and
    //                      IApiKeyAuthenticator (per-provider auth scheme).
    //   Adapter          → IChatModelClient wraps each foreign LLM API behind a uniform contract.
    //   AbstractFactory  → IChatModelClientFactory builds a provider's client+auth family;
    //                      ChatModelClientFactory selects the one for the configured provider.

    private static void AddGeneration(IServiceCollection services, GenerationOptions generation)
    {
        services.AddSingleton<RoutinePromptBuilder>();
        services.AddSingleton<TemplateRoutineGenerator>();

        // One abstract factory per provider, plus the selector that resolves the active one.
        services.AddSingleton<IChatModelClientFactory, AnthropicClientFactory>();
        services.AddSingleton<IChatModelClientFactory, OpenAiClientFactory>();
        services.AddSingleton<IChatModelClientFactory, GeminiClientFactory>();
        services.AddSingleton<IChatModelClientFactory, DeepSeekClientFactory>();
        services.AddSingleton<ChatModelClientFactory>();

        // Only the active provider is ever created, so only its resilient HttpClient is registered.
        // Auth is applied per-request by the strategy, keeping the client configuration key-agnostic.
        if (generation.Enabled)
        {
            services.AddHttpClient(HttpClients.For(generation.Provider), c =>
            {
                c.BaseAddress = new Uri(generation.Active.BaseUrl);
                c.Timeout = Timeout.InfiniteTimeSpan; // resilience handler owns the timeouts
            })
            .AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(generation.TimeoutSeconds);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(generation.TimeoutSeconds * 2 + 10);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(generation.TimeoutSeconds * 2);
                o.Retry.MaxRetryAttempts = 1; // LLM calls are costly; retry sparingly
            });
        }

        services.AddScoped<IRoutineGenerator>(sp =>
        {
            var template = sp.GetRequiredService<TemplateRoutineGenerator>();

            IRoutineGenerator core = template;
            if (generation.Enabled)
            {
                var client = sp.GetRequiredService<ChatModelClientFactory>().CreateActive();
                var primary = new ChatRoutineGenerator(
                    client,
                    sp.GetRequiredService<RoutinePromptBuilder>(),
                    sp.GetRequiredService<IOptions<GenerationOptions>>());

                core = new FallbackRoutineGenerator(
                    primary, template, sp.GetRequiredService<ILogger<FallbackRoutineGenerator>>());
            }

            return new CachingRoutineGenerator(
                core,
                sp.GetRequiredService<HybridCache>(),
                sp.GetRequiredService<IOptions<RoutineCacheOptions>>());
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
