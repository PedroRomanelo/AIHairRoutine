using System.Security.Cryptography;
using System.Text;
using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation;

/// <summary>
/// Decorator that caches generated routines keyed by the normalized profile+priorities+products+locale.
/// This collapses the expensive LLM calls for equivalent inputs — the biggest scale lever.
/// </summary>
public sealed class CachingRoutineGenerator(
    IRoutineGenerator inner,
    HybridCache cache,
    IOptions<RoutineCacheOptions> options) : IRoutineGenerator
{
    private const string KeyVersion = "routine:v1:";

    public async Task<RoutineResult> GenerateAsync(
        ProfileResult profile,
        IReadOnlyList<ProductMatch> products,
        string locale,
        CancellationToken ct = default)
    {
        var key = BuildKey(profile, products, locale);
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
                return await inner.GenerateAsync(profile, products, locale, token);
            },
            entryOptions,
            cancellationToken: ct);

        return result with { FromCache = !generated };
    }

    private static string BuildKey(ProfileResult profile, IReadOnlyList<ProductMatch> products, string locale)
    {
        var p = profile.Profile;
        var canonical = new StringBuilder()
            .Append(p.HairType).Append('|')
            .Append(p.Condition).Append('|')
            .Append(p.DamageLevel).Append('|')
            .Append(p.FrizzLevel).Append('|')
            .Append(p.ChemicalTreatment).Append('|')
            .Append(string.Join(',', profile.Priorities)).Append('|')
            .Append(string.Join(',', products.Select(m => m.Product.Id).OrderBy(id => id))).Append('|')
            .Append(locale)
            .ToString();

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return KeyVersion + Convert.ToHexString(hash);
    }
}
