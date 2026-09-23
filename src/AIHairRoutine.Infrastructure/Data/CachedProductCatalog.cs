using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Data;

/// <summary>
/// Caches the active catalog in memory so matching never hits the DB on the hot path.
/// Resilient: if the database is unavailable, returns an empty catalog and logs, so the
/// diagnosis endpoint still works (with no product recommendations).
/// </summary>
public sealed class CachedProductCatalog(
    IProductRepository repository,
    HybridCache cache,
    IOptions<DatabaseOptions> options,
    ILogger<CachedProductCatalog> logger) : IProductCatalog
{
    private const string CacheKey = "catalog:active";

    public async Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return [];

        try
        {
            var entryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromSeconds(options.Value.CatalogCacheSeconds),
            };

            return await cache.GetOrCreateAsync(
                CacheKey,
                async token => (IReadOnlyList<Product>)await repository.GetActiveAsync(token),
                entryOptions,
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to load product catalog; returning empty catalog.");
            return [];
        }
    }
}
