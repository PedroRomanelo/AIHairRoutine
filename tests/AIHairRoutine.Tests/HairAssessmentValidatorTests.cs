using AIHairRoutine.Application.Models;
using AIHairRoutine.Application.Validation;
using Shouldly;

namespace AIHairRoutine.Tests;

public sealed class HairAssessmentValidatorTests
{
    private readonly HairAssessmentValidator _sut = new(TestData.Timing);

    [Fact]
    public void Accepts_a_complete_questionnaire()
    {
        var assessment = TestData.Assessment() with
        {
            Chemical = TestData.Chemical(ChemicalType.Coloring, "há 2 meses"),
            Allergies = [Allergen.Fragrance, Allergen.Sulfate],
        };

        _sut.Validate(assessment).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Requires_the_single_choice_answers_and_the_goal()
    {
        var result = _sut.Validate(TestData.Assessment() with { HairType = null, Tone = null, MainGoal = " " });

        result.Errors.Select(e => e.PropertyName).ShouldBe(["HairType", "Tone", "MainGoal"], ignoreOrder: true);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("não sei")]
    [InlineData("10/12/2026")] // future
    public void Requires_a_resolvable_past_date_when_there_is_chemistry(string? performed)
    {
        var assessment = TestData.Assessment() with { Chemical = TestData.Chemical(ChemicalType.Bleaching, performed!) };

        _sut.Validate(assessment).Errors.ShouldContain(e => e.PropertyName == "Chemical.Performed");
    }

    [Fact]
    public void Ignores_chemical_details_when_there_is_no_chemistry()
    {
        _sut.Validate(TestData.Assessment() with { Chemical = new ChemicalHistory { HasChemical = false } })
            .IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(new[] { HairCondition.Normal, HairCondition.Oily })]
    [InlineData(new[] { HairCondition.Dry, HairCondition.Dry })]
    [InlineData(new HairCondition[0])]
    public void Rejects_invalid_condition_combinations(HairCondition[] conditions)
    {
        _sut.Validate(TestData.Assessment() with { Conditions = conditions })
            .Errors.ShouldContain(e => e.PropertyName == "Conditions");
    }

    [Fact]
    public void Rejects_duplicate_or_unknown_allergies()
    {
        _sut.Validate(TestData.Assessment() with { Allergies = [Allergen.Sulfate, Allergen.Sulfate] }).IsValid.ShouldBeFalse();
        _sut.Validate(TestData.Assessment() with { Allergies = [(Allergen)99] }).IsValid.ShouldBeFalse();
    }
}
