using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation;

/// <summary>
/// Decorator that caches the narrative keyed by the profile, priorities, schedule, products and locale.
/// Equivalent profiles produce identical schedules, so this collapses the expensive LLM calls — the
/// biggest scale lever.
/// </summary>
public sealed class CachingRoutineGenerator(
    IRoutineGenerator inner,
    HybridCache cache,
    IOptions<RoutineCacheOptions> options) : IRoutineGenerator
{
    private const string KeyVersion = "routine:v2:";

    private static readonly JsonSerializerOptions KeyJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<RoutineResult> GenerateAsync(
        ProfileResult profile,
        HairSchedule schedule,
        IReadOnlyList<Product> products,
        string locale,
        CancellationToken ct = default)
    {
        var key = BuildKey(profile, schedule, products, locale);
        bool generated = false;

        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(options.Value.ExpirationMinutes),
        };

        var result = await cache.GetOrCreateAsync(
            key,
            async token =>
            {
                generated = true;
                return await inner.GenerateAsync(profile, schedule, products, locale, token);
            },
            entryOptions,
            cancellationToken: ct);

        return result with { FromCache = !generated };
    }

    private static string BuildKey(ProfileResult profile, HairSchedule schedule, IReadOnlyList<Product> products, string locale)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            profile.Profile,
            profile.Priorities,
            schedule,
            products = products.Select(p => p.Id),
            locale,
        }, KeyJson);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return KeyVersion + Convert.ToHexString(hash);
    }
}
