using AIHairRoutine.Application.Models;
using AIHairRoutine.Application.Profiling;
using Shouldly;

namespace AIHairRoutine.Tests;

public sealed class RuleBasedProfilerTests
{
    private readonly RuleBasedProfiler _sut = new();

    [Fact]
    public void Maps_the_reference_example_to_the_expected_profile_and_priorities()
    {
        var assessment = new HairAssessment
        {
            HairType = HairType.Wavy,
            ChemicalTreatment = ChemicalTreatment.Progressive,
            ColorTreated = false,
            Concerns = new HairConcerns { Dryness = 8, Frizz = 7, Breakage = 4, Oiliness = 2, HairLoss = 1 },
        };

        var result = _sut.Profile(assessment);

        result.Profile.Condition.ShouldBe(HairCondition.Dry);
        result.Profile.DamageLevel.ShouldBe(DamageLevel.Moderate);
        result.Profile.FrizzLevel.ShouldBe(FrizzLevel.High);
        result.Profile.ChemicalTreatment.ShouldBeTrue();
        result.Source.ShouldBe(ProfileSource.Rules);

        result.Priorities.ShouldBe(new[]
        {
            HairPriority.Hydration,
            HairPriority.FrizzControl,
            HairPriority.DamageRepair,
        });
    }

    [Fact]
    public void Classifies_oily_scalp_and_prioritizes_oil_control()
    {
        var assessment = new HairAssessment
        {
            HairType = HairType.Straight,
            Concerns = new HairConcerns { Oiliness = 9, Dryness = 1, Frizz = 1, Breakage = 0 },
        };

        var result = _sut.Profile(assessment);

        result.Profile.Condition.ShouldBe(HairCondition.Oily);
        result.Priorities.ShouldContain(HairPriority.OilControl);
    }

    [Fact]
    public void Always_returns_at_least_two_priorities_even_for_healthy_hair()
    {
        var assessment = new HairAssessment { HairType = HairType.Straight, Concerns = new HairConcerns() };

        var result = _sut.Profile(assessment);

        result.Priorities.Count.ShouldBeGreaterThanOrEqualTo(2);
    }
}
