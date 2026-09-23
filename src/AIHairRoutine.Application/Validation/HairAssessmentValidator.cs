using AIHairRoutine.Application.Models;
using FluentValidation;

namespace AIHairRoutine.Application.Validation;

/// <summary>Validates questionnaire input: enums in range, scores 0..10, sane locale.</summary>
public sealed class HairAssessmentValidator : AbstractValidator<HairAssessment>
{
    public HairAssessmentValidator()
    {
        RuleFor(x => x.HairType).IsInEnum();
        RuleFor(x => x.ChemicalTreatment).IsInEnum();
        RuleFor(x => x.Locale).NotEmpty().MaximumLength(35);

        RuleFor(x => x.Concerns).NotNull();
        When(x => x.Concerns is not null, () =>
        {
            RuleFor(x => x.Concerns.Dryness).InclusiveBetween(0, 10);
            RuleFor(x => x.Concerns.Frizz).InclusiveBetween(0, 10);
            RuleFor(x => x.Concerns.Breakage).InclusiveBetween(0, 10);
            RuleFor(x => x.Concerns.Oiliness).InclusiveBetween(0, 10);
            RuleFor(x => x.Concerns.HairLoss).InclusiveBetween(0, 10);
        });

        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
