using AIHairRoutine.Application.Models;
using Shouldly;

namespace AIHairRoutine.Tests;

public sealed class RuleBasedProfilerTests
{
    [Fact]
    public void Combines_conditions_and_the_stated_goal_into_ordered_priorities()
    {
        var assessment = TestData.Assessment(HairType.Wavy, HairCondition.Dry, HairCondition.Frizzy) with
        {
            MainGoal = "Quero reduzir o frizz e ter mais brilho",
        };

        var result = TestData.Rules.Profile(assessment);

        result.Source.ShouldBe(ProfileSource.Rules);
        result.Priorities[0].ShouldBe(HairPriority.FrizzControl);
        result.Priorities.ShouldContain(HairPriority.Hydration);
        result.Priorities.ShouldContain(HairPriority.Shine);
        result.Priorities.Count.ShouldBeLessThanOrEqualTo(4);
    }

    [Fact]
    public void A_recent_bleaching_prioritizes_reconstruction_and_raises_its_need()
    {
        var baseline = TestData.Assessment(HairType.Straight, HairCondition.Damaged) with { MainGoal = "cabelo saudável" };
        var bleached = baseline with { Chemical = TestData.Chemical(ChemicalType.Bleaching, "há 2 meses") };

        var without = TestData.Rules.Profile(baseline);
        var with = TestData.Rules.Profile(bleached);

        with.Profile.Chemical.IsRecent.ShouldBeTrue();
        with.Profile.Chemical.DaysSince.ShouldBe(60);
        with.Priorities[0].ShouldBe(HairPriority.Reconstruction);
        with.Profile.TreatmentNeeds.Reconstruction.ShouldBeGreaterThan(without.Profile.TreatmentNeeds.Reconstruction);
    }

    [Fact]
    public void An_old_chemical_is_not_recent()
    {
        var assessment = TestData.Assessment() with { Chemical = TestData.Chemical(ChemicalType.Coloring, "1 ano") };

        TestData.Rules.Profile(assessment).Profile.Chemical.IsRecent.ShouldBeFalse();
    }

    [Fact]
    public void Oily_hair_prioritizes_oil_control()
    {
        var assessment = TestData.Assessment(HairType.Straight, HairCondition.Oily) with { MainGoal = "controlar a oleosidade" };

        TestData.Rules.Profile(assessment).Priorities[0].ShouldBe(HairPriority.OilControl);
    }

    [Fact]
    public void Always_returns_at_least_two_priorities()
    {
        var assessment = TestData.Assessment(HairType.Straight, HairCondition.Normal) with { MainGoal = "manter como está" };

        TestData.Rules.Profile(assessment).Priorities.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Treatment_needs_are_shares_that_favor_hydration_for_dry_coily_hair()
    {
        var needs = TestData.Rules.Profile(TestData.Assessment(HairType.Coily, HairCondition.Dry)).Profile.TreatmentNeeds;

        (needs.Hydration + needs.Nutrition + needs.Reconstruction).ShouldBe(1.0, tolerance: 0.02);
        needs.Hydration.ShouldBeGreaterThan(needs.Nutrition);
        needs.Nutrition.ShouldBeGreaterThan(needs.Reconstruction);
    }

    [Fact]
    public void Copies_the_ticked_allergies_without_duplicates()
    {
        var assessment = TestData.Assessment() with { Allergies = [Allergen.Sulfate, Allergen.Fragrance, Allergen.Sulfate] };

        TestData.Rules.Profile(assessment).Profile.Allergies.ShouldBe([Allergen.Sulfate, Allergen.Fragrance]);
    }
}
