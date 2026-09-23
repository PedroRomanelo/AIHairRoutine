using AIHairRoutine.Application.Matching;
using AIHairRoutine.Application.Models;
using Shouldly;

namespace AIHairRoutine.Tests;

public sealed class ProductMatcherTests
{
    private readonly ProductMatcher _sut = new();

    private static readonly HairProfile WavyDry = new()
    {
        HairType = HairType.Wavy,
        Condition = HairCondition.Dry,
        DamageLevel = DamageLevel.Moderate,
        FrizzLevel = FrizzLevel.High,
        ChemicalTreatment = true,
    };

    private static IReadOnlyList<Product> Catalog =>
    [
        new Product { Id = 1, Name = "Shampoo Hidratante", Category = "shampoo", Targets = [HairPriority.Hydration], HairTypes = [] },
        new Product { Id = 2, Name = "Máscara Reconstrução", Category = "mask", Targets = [HairPriority.DamageRepair], HairTypes = [], ForChemical = true },
        new Product { Id = 3, Name = "Leave-in Antifrizz", Category = "leave_in", Targets = [HairPriority.FrizzControl, HairPriority.Hydration], HairTypes = [HairType.Wavy, HairType.Curly] },
        new Product { Id = 4, Name = "Shampoo Antioleosidade", Category = "shampoo", Targets = [HairPriority.OilControl], HairTypes = [HairType.Straight] },
    ];

    [Fact]
    public void Ranks_products_that_serve_the_priorities_and_drops_irrelevant_ones()
    {
        var priorities = new[] { HairPriority.Hydration, HairPriority.FrizzControl, HairPriority.DamageRepair };

        var matches = _sut.Match(WavyDry, priorities, Catalog);

        // Product 4 targets only oil_control (not a priority) -> excluded.
        matches.ShouldNotContain(m => m.Product.Id == 4);
        matches.ShouldContain(m => m.Product.Id == 3); // frizz + hydration + hair-type match
        matches.ShouldAllBe(m => m.Score > 0);

        // Scores are ordered descending.
        matches.Select(m => m.Score).ShouldBe(matches.Select(m => m.Score).OrderByDescending(s => s));
    }

    [Fact]
    public void Returns_empty_for_empty_catalog()
    {
        _sut.Match(WavyDry, [HairPriority.Hydration], []).ShouldBeEmpty();
    }

    [Fact]
    public void Respects_the_max_limit()
    {
        var matches = _sut.Match(WavyDry, [HairPriority.Hydration, HairPriority.FrizzControl, HairPriority.DamageRepair], Catalog, max: 1);
        matches.Count.ShouldBe(1);
    }
}
