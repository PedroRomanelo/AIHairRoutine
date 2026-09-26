using AIHairRoutine.Application.Matching;
using AIHairRoutine.Application.Models;
using Shouldly;

namespace AIHairRoutine.Tests;

public sealed class ProductSelectorTests
{
    private readonly ProductSelector _sut = new();

    private ProductSelection Select(HairAssessment assessment)
    {
        var profile = TestData.Rules.Profile(assessment);
        return _sut.Select(profile.Profile, profile.Priorities, TestCatalog.All);
    }

    [Fact]
    public void A_recent_chemical_excludes_products_that_are_not_chemical_safe()
    {
        var assessment = TestData.Assessment(HairType.Straight, HairCondition.Oily) with
        {
            MainGoal = "controlar a oleosidade",
            Chemical = TestData.Chemical(ChemicalType.Coloring, "há 1 mês"),
        };

        var selection = Select(assessment);

        selection.Eligible.ShouldNotContain(m => m.Product.Id == TestCatalog.OilControlShampoo.Id);
        var excluded = selection.Excluded.ShouldHaveSingleItem();
        excluded.Id.ShouldBe(TestCatalog.OilControlShampoo.Id);
        excluded.Reason.ShouldBe(ExclusionReason.UnsafeForRecentChemical);
    }

    [Fact]
    public void An_old_chemical_keeps_those_products()
    {
        var assessment = TestData.Assessment(HairType.Straight, HairCondition.Oily) with
        {
            MainGoal = "controlar a oleosidade",
            Chemical = TestData.Chemical(ChemicalType.Coloring, "1 ano"),
        };

        Select(assessment).Eligible.ShouldContain(m => m.Product.Id == TestCatalog.OilControlShampoo.Id);
    }

    [Fact]
    public void A_recent_relaxation_excludes_contraindicated_keratin()
    {
        var assessment = TestData.Assessment(HairType.Curly, HairCondition.Damaged) with
        {
            Chemical = TestData.Chemical(ChemicalType.Relaxation, "há 2 meses"),
        };

        var selection = Select(assessment);

        var keratin = selection.Excluded.ShouldHaveSingleItem();
        keratin.Id.ShouldBe(TestCatalog.KeratinMask.Id);
        keratin.Reason.ShouldBe(ExclusionReason.Contraindication);
        keratin.Chemical.ShouldBe(ChemicalType.Relaxation);
        selection.Eligible.ShouldContain(m => m.Product.Id == TestCatalog.VeganReconstructionMask.Id);
    }

    [Fact]
    public void Ticked_allergens_exclude_products_that_contain_them()
    {
        var assessment = TestData.Assessment() with { Allergies = [Allergen.Fragrance] };

        var selection = Select(assessment);

        var shampoo = selection.Excluded.Single(e => e.Id == TestCatalog.ShampooWithFragrance.Id);
        shampoo.Reason.ShouldBe(ExclusionReason.Allergy);
        shampoo.Allergens.ShouldBe([Allergen.Fragrance]);
        selection.Eligible.ShouldNotContain(m => m.Product.Allergens.Contains(Allergen.Fragrance));
        selection.Eligible.ShouldContain(m => m.Product.Id == TestCatalog.ShampooFragranceFree.Id);
    }

    [Fact]
    public void Filters_by_hair_type_without_reporting_it_as_an_exclusion()
    {
        var selection = Select(TestData.Assessment(HairType.Coily, HairCondition.Dry) with { MainGoal = "quero volume" });

        selection.Eligible.ShouldNotContain(m => m.Product.Id == TestCatalog.VolumeSpray.Id);
        selection.Excluded.ShouldNotContain(e => e.Id == TestCatalog.VolumeSpray.Id);
    }

    [Fact]
    public void Ranks_eligible_products_best_first_and_drops_irrelevant_ones()
    {
        var selection = Select(TestData.Assessment(HairType.Wavy, HairCondition.Dry));

        selection.Eligible.ShouldNotContain(m => m.Product.Id == TestCatalog.HairLossTonic.Id);
        selection.Eligible.Select(m => m.Score).ShouldBe(selection.Eligible.Select(m => m.Score).OrderByDescending(s => s));
    }
}
