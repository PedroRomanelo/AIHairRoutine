using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Abstractions;

/// <summary>Strategy that ranks catalog products against a profile and its priorities.</summary>
public interface IProductMatcher
{
    IReadOnlyList<ProductMatch> Match(
        HairProfile profile,
        IReadOnlyList<HairPriority> priorities,
        IReadOnlyList<Product> catalog,
        int max = 6);
}
