using AIHairRoutine.Application.Models;
using AIHairRoutine.Application.Profiling;
using Shouldly;

namespace AIHairRoutine.Tests;

public sealed class GoalKeywordExtractorTests
{
    [Fact]
    public void Extracts_priorities_in_the_order_they_are_mentioned_ignoring_case_and_accents()
    {
        GoalKeywordExtractor.Extract("Quero reduzir o FRIZZ e ganhar brilho")
            .ShouldBe([HairPriority.FrizzControl, HairPriority.Shine]);

        GoalKeywordExtractor.Extract("Meu cabelo está caindo e muito ressecado")
            .ShouldBe([HairPriority.HairLossControl, HairPriority.Hydration]);
    }

    [Fact]
    public void Treats_reducing_volume_as_frizz_control_and_gaining_volume_as_volume()
    {
        GoalKeywordExtractor.Extract("preciso reduzir volume").ShouldBe([HairPriority.FrizzControl]);
        GoalKeywordExtractor.Extract("quero ganhar volume").ShouldBe([HairPriority.Volume]);
    }

    [Fact]
    public void Returns_empty_when_nothing_is_recognized()
    {
        GoalKeywordExtractor.Extract("quero ficar bonita para o casamento").ShouldBeEmpty();
        GoalKeywordExtractor.Extract(null).ShouldBeEmpty();
    }
}
