using AIHairRoutine.Application.Models;
using AIHairRoutine.Application.Profiling;
using FluentValidation;

namespace AIHairRoutine.Application.Validation;

/// <summary>Validates the questionnaire: required answers, enums in range and a resolvable chemical date.</summary>
public sealed class HairAssessmentValidator : AbstractValidator<HairAssessment>
{
    public HairAssessmentValidator(ChemicalTimingParser timing)
    {
        RuleFor(x => x.HairType).NotNull().IsInEnum();
        RuleFor(x => x.Thickness).NotNull().IsInEnum();
        RuleFor(x => x.Tone).NotNull().IsInEnum();

        RuleFor(x => x.Conditions)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(c => c!.Distinct().Count() == c!.Count)
            .WithMessage("As condições não podem se repetir.")
            .Must(c => !IsNormalCombinedWithDryOrOily(c!))
            .WithMessage("'normal' não pode ser combinado com 'dry' ou 'oily'.");
        RuleForEach(x => x.Conditions).IsInEnum();

        When(x => x.Chemical is { HasChemical: true }, () =>
        {
            RuleFor(x => x.Chemical!.Type).NotNull().IsInEnum();
            RuleFor(x => x.Chemical!.Performed)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(50)
                .Must(text => timing.TryParseDaysSince(text, out _))
                .WithMessage("Informe quando a química foi feita, ex.: 'há 2 meses', '3 semanas' ou '10/06/2026' (sem datas futuras).");
            RuleFor(x => x.Chemical!.TouchUpFrequency).NotNull().IsInEnum();
        });

        RuleFor(x => x.MainGoal).NotEmpty().MaximumLength(1000);

        When(x => x.Allergies is not null, () =>
        {
            RuleForEach(x => x.Allergies).IsInEnum();
            RuleFor(x => x.Allergies)
                .Must(a => a!.Distinct().Count() == a!.Count)
                .WithMessage("As alergias não podem se repetir.");
        });

        RuleFor(x => x.Locale).NotEmpty().MaximumLength(35);
    }

    private static bool IsNormalCombinedWithDryOrOily(IReadOnlyList<HairCondition> conditions) =>
        conditions.Contains(HairCondition.Normal)
        && (conditions.Contains(HairCondition.Dry) || conditions.Contains(HairCondition.Oily));
}
