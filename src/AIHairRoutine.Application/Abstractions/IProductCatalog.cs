using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Abstractions;

/// <summary>Read access to the active product catalog (cached in memory off the hot path).</summary>
public interface IProductCatalog
{
    Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken ct = default);
}

/// <summary>Persistence-facing repository for products (Dapper/SQL).</summary>
public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken ct = default);
}
