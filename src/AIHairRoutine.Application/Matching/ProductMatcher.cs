using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Matching;

/// <summary>
/// Ranks products by how well they serve the profile's priorities, hair type and
/// chemical status. Priorities earlier in the list weigh more. Runs in-memory over the
/// cached catalog, so it never touches the database on the hot path.
/// </summary>
public sealed class ProductMatcher : IProductMatcher
{
    public IReadOnlyList<ProductMatch> Match(
        HairProfile profile,
        IReadOnlyList<HairPriority> priorities,
        IReadOnlyList<Product> catalog,
        int max = 6)
    {
        if (catalog.Count == 0)
            return [];

        // Priority weight decays by rank: first priority = priorities.Count, last = 1.
        var weightByPriority = new Dictionary<HairPriority, int>();
        for (int i = 0; i < priorities.Count; i++)
            weightByPriority[priorities[i]] = priorities.Count - i;

        double maxPossible = weightByPriority.Values.Sum() + 3; // + hair-type + chemical bonuses

        var scored = new List<ProductMatch>(catalog.Count);
        foreach (var p in catalog)
        {
            double raw = 0;
            foreach (var target in p.Targets)
                if (weightByPriority.TryGetValue(target, out var w))
                    raw += w;

            if (raw <= 0)
                continue; // product addresses none of the priorities

            bool hairTypeMatch = p.HairTypes.Count == 0 || p.HairTypes.Contains(profile.HairType);
            if (hairTypeMatch)
                raw += 2;

            if (profile.ChemicalTreatment && p.ForChemical)
                raw += 1;

            double score = Math.Round(Math.Min(1.0, raw / maxPossible), 2);
            scored.Add(new ProductMatch(p, score));
        }

        // Prefer variety of categories: keep the best product per category, then fill by score.
        var bestPerCategory = scored
            .GroupBy(m => m.Product.Category)
            .Select(g => g.OrderByDescending(m => m.Score).First());

        return bestPerCategory
            .OrderByDescending(m => m.Score)
            .Take(max)
            .ToList();
    }
}
