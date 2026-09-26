using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Abstractions;

/// <summary>
/// Filters and ranks catalog products for a profile: hair type + target conditions, then removes
/// products unsafe for a recent chemical, contraindicated, or containing a ticked allergen.
/// </summary>
public interface IProductSelector
{
    ProductSelection Select(HairProfile profile, IReadOnlyList<HairPriority> priorities, IReadOnlyList<Product> catalog);
}
