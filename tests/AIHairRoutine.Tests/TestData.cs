using System.Security.Cryptography;
using System.Text;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Application.Profiling;

namespace AIHairRoutine.Tests;

/// <summary>Clock pinned to 2026-09-25 so relative/absolute chemical dates are deterministic.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static readonly FixedTimeProvider Default = new(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => now;
}

internal static class TestData
{
    public static readonly ChemicalTimingParser Timing = new(FixedTimeProvider.Default);
    public static readonly RuleBasedProfiler Rules = new(Timing);

    /// <summary>A valid baseline questionnaire; tests tweak it with <c>with</c>.</summary>
    public static HairAssessment Assessment(
        HairType hairType = HairType.Wavy,
        params HairCondition[] conditions) => new()
    {
        HairType = hairType,
        Thickness = HairThickness.Medium,
        Tone = HairTone.Brown,
        Conditions = conditions.Length > 0 ? conditions : [HairCondition.Dry],
        MainGoal = "quero hidratar o cabelo",
        Allergies = [],
        Locale = "pt-BR",
    };

    public static ChemicalHistory Chemical(ChemicalType type, string performed) => new()
    {
        HasChemical = true,
        Type = type,
        Performed = performed,
        TouchUpFrequency = TouchUpFrequency.Quarterly,
    };
}

/// <summary>Small in-memory catalog covering every role the schedule needs.</summary>
internal static class TestCatalog
{
    public static readonly Product ShampooWithFragrance = Make("Shampoo Hidratante", ProductCategory.Shampoo,
        targets: [HairPriority.Hydration], allergens: [Allergen.Fragrance], order: 1);

    public static readonly Product ShampooFragranceFree = Make("Shampoo Sem Fragrância", ProductCategory.Shampoo,
        targets: [HairPriority.Hydration, HairPriority.FrizzControl], order: 1);

    public static readonly Product OilControlShampoo = Make("Shampoo Antioleosidade", ProductCategory.Shampoo,
        targets: [HairPriority.OilControl], hairTypes: [HairType.Straight, HairType.Wavy], safeForChemical: false, order: 1);

    public static readonly Product Conditioner = Make("Condicionador Hidratante", ProductCategory.Conditioner,
        targets: [HairPriority.Hydration, HairPriority.FrizzControl], order: 2);

    public static readonly Product HydrationMask = Make("Máscara Hidratação", ProductCategory.Mask,
        targets: [HairPriority.Hydration], treatments: [TreatmentType.Hydration], minutes: 15, order: 3, minInterval: 2);

    public static readonly Product NutritionMask = Make("Máscara Nutrição", ProductCategory.Mask,
        targets: [HairPriority.Nutrition, HairPriority.Shine], treatments: [TreatmentType.Nutrition], minutes: 20, order: 3, minInterval: 3);

    public static readonly Product KeratinMask = Make("Máscara Reconstrução Queratina", ProductCategory.Mask,
        targets: [HairPriority.Reconstruction], treatments: [TreatmentType.Reconstruction],
        contraindicated: [ChemicalType.Relaxation, ChemicalType.Straightening],
        allergens: [Allergen.Fragrance], minutes: 10, order: 3, minInterval: 7);

    public static readonly Product VeganReconstructionMask = Make("Máscara Reconstrução Vegana", ProductCategory.Mask,
        targets: [HairPriority.Reconstruction], treatments: [TreatmentType.Reconstruction], minutes: 10, order: 3, minInterval: 7);

    public static readonly Product CurlyLeaveIn = Make("Leave-in Cachos", ProductCategory.LeaveIn,
        targets: [HairPriority.FrizzControl, HairPriority.Hydration], hairTypes: [HairType.Curly, HairType.Coily], order: 5);

    public static readonly Product EndsSerum = Make("Sérum de Pontas", ProductCategory.Serum,
        targets: [HairPriority.FrizzControl, HairPriority.Shine], order: 6);

    public static readonly Product HairLossTonic = Make("Tônico Antiqueda", ProductCategory.Treatment,
        targets: [HairPriority.HairLossControl], safeForChemical: false, order: 4);

    public static readonly Product VolumeSpray = Make("Spray Volume", ProductCategory.Finisher,
        targets: [HairPriority.Volume, HairPriority.Shine], hairTypes: [HairType.Straight, HairType.Wavy],
        frequency: UsageFrequency.TwiceWeekly, order: 7);

    public static IReadOnlyList<Product> All =>
    [
        ShampooWithFragrance, ShampooFragranceFree, OilControlShampoo, Conditioner, HydrationMask, NutritionMask,
        KeratinMask, VeganReconstructionMask, CurlyLeaveIn, EndsSerum, HairLossTonic, VolumeSpray,
    ];

    public static Product Make(
        string name,
        ProductCategory category,
        HairPriority[]? targets = null,
        TreatmentType[]? treatments = null,
        HairType[]? hairTypes = null,
        bool safeForChemical = true,
        ChemicalType[]? contraindicated = null,
        Allergen[]? allergens = null,
        UsageFrequency frequency = UsageFrequency.Daily,
        int? minutes = null,
        int order = 1,
        int minInterval = 0) => new()
    {
        // Deterministic id per name so test data is stable across runs.
        Id = new Guid(MD5.HashData(Encoding.UTF8.GetBytes(name))),
        Name = name,
        Category = category,
        Targets = targets ?? [],
        TreatmentTypes = treatments ?? [],
        HairTypes = hairTypes ?? [],
        SafeForChemical = safeForChemical,
        ContraindicatedChemicals = contraindicated ?? [],
        Allergens = allergens ?? [],
        UsageFrequency = frequency,
        ActionTimeMinutes = minutes,
        ApplicationOrder = order,
        MinIntervalDays = minInterval,
        Price = 39.90m,
        SizeMl = 250,
    };
}
