using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Matching;

/// <summary>
/// Selects products in the order the spec defines: (1) hair type + target conditions,
/// (2) recent chemical → only chemical-safe products, (3) contraindications, (4) allergies.
/// Priorities earlier in the list weigh more. Runs in memory over the cached catalog.
/// </summary>
public sealed class ProductSelector : IProductSelector
{
    public ProductSelection Select(HairProfile profile, IReadOnlyList<HairPriority> priorities, IReadOnlyList<Product> catalog)
    {
        // Priority weight decays by rank: first priority = priorities.Count, last = 1.
        var weightByPriority = new Dictionary<HairPriority, int>();
        for (int i = 0; i < priorities.Count; i++)
            weightByPriority[priorities[i]] = priorities.Count - i;

        double maxPossible = weightByPriority.Values.Sum() + 3; // + hair-type + chemical bonuses

        var eligible = new List<ProductMatch>(catalog.Count);
        var excluded = new List<ExcludedProduct>();

        foreach (var p in catalog)
        {
            if (p.HairTypes.Count > 0 && !p.HairTypes.Contains(profile.HairType))
                continue;

            // Irrelevant: targets none of the priorities. Products without targets are generic, and
            // H/N/R cycle products stay relevant because the cycle may schedule their axis anyway.
            double raw = p.Targets.Sum(t => weightByPriority.GetValueOrDefault(t));
            if (p.Targets.Count > 0 && raw <= 0 && p.TreatmentTypes.Count == 0)
                continue;

            if (FindExclusion(p, profile) is { } exclusion)
            {
                excluded.Add(exclusion);
                continue;
            }

            raw += p.HairTypes.Count > 0 ? 2 : 1; // specialist for this hair type beats generic
            if (profile.Chemical.HasChemical && p.SafeForChemical)
                raw += 1;

            eligible.Add(new ProductMatch(p, Math.Round(Math.Min(1.0, raw / maxPossible), 2)));
        }

        return new ProductSelection
        {
            Eligible = eligible
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.Product.Name, StringComparer.Ordinal)
                .ToList(),
            Excluded = excluded,
        };
    }

    private static ExcludedProduct? FindExclusion(Product p, HairProfile profile)
    {
        var allergens = p.Allergens.Intersect(profile.Allergies).ToList();
        if (allergens.Count > 0)
            return Excluded(p, ExclusionReason.Allergy) with { Allergens = allergens };

        var chemical = profile.Chemical;
        if (!chemical.IsRecent)
            return null;

        if (!p.SafeForChemical)
            return Excluded(p, ExclusionReason.UnsafeForRecentChemical) with { Chemical = chemical.Type };

        if (chemical.Type is { } type && p.ContraindicatedChemicals.Contains(type))
            return Excluded(p, ExclusionReason.Contraindication) with { Chemical = type };

        return null;
    }

    private static ExcludedProduct Excluded(Product p, ExclusionReason reason) =>
        new() { Id = p.Id, Name = p.Name, Reason = reason };
}
