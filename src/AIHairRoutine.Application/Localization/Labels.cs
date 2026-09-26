using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Localization;

/// <summary>pt-BR / en labels for domain values, shared by the schedule builder and the template narrative.</summary>
public static class Labels
{
    public static bool IsEnglish(string? locale) =>
        locale?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;

    public static string Of(HairType type, bool en) => (type, en) switch
    {
        (HairType.Straight, false) => "liso",
        (HairType.Wavy, false) => "ondulado",
        (HairType.Curly, false) => "cacheado",
        (HairType.Coily, false) => "crespo",
        (HairType.Straight, true) => "straight",
        (HairType.Wavy, true) => "wavy",
        (HairType.Curly, true) => "curly",
        (HairType.Coily, true) => "coily",
        _ => type.ToString(),
    };

    public static string Of(HairPriority priority, bool en) => (priority, en) switch
    {
        (HairPriority.Hydration, false) => "hidratação",
        (HairPriority.Nutrition, false) => "nutrição",
        (HairPriority.Reconstruction, false) => "reconstrução",
        (HairPriority.FrizzControl, false) => "controle de frizz",
        (HairPriority.OilControl, false) => "controle de oleosidade",
        (HairPriority.Shine, false) => "brilho",
        (HairPriority.HairLossControl, false) => "controle da queda",
        (HairPriority.Volume, false) => "volume",
        (HairPriority.Hydration, true) => "hydration",
        (HairPriority.Nutrition, true) => "nutrition",
        (HairPriority.Reconstruction, true) => "reconstruction",
        (HairPriority.FrizzControl, true) => "frizz control",
        (HairPriority.OilControl, true) => "oil control",
        (HairPriority.Shine, true) => "shine",
        (HairPriority.HairLossControl, true) => "hair loss control",
        (HairPriority.Volume, true) => "volume",
        _ => priority.ToString(),
    };

    public static string Of(TreatmentType treatment, bool en) => (treatment, en) switch
    {
        (TreatmentType.Hydration, false) => "hidratação",
        (TreatmentType.Nutrition, false) => "nutrição",
        (TreatmentType.Reconstruction, false) => "reconstrução",
        (TreatmentType.Hydration, true) => "hydration",
        (TreatmentType.Nutrition, true) => "nutrition",
        (TreatmentType.Reconstruction, true) => "reconstruction",
        _ => treatment.ToString(),
    };

    public static string Of(ChemicalType chemical, bool en) => (chemical, en) switch
    {
        (ChemicalType.Relaxation, false) => "relaxamento",
        (ChemicalType.Straightening, false) => "alisamento",
        (ChemicalType.Perm, false) => "ondulação",
        (ChemicalType.Coloring, false) => "coloração",
        (ChemicalType.Bleaching, false) => "descoloração",
        (ChemicalType.Other, false) => "química",
        (ChemicalType.Relaxation, true) => "relaxer",
        (ChemicalType.Straightening, true) => "straightening",
        (ChemicalType.Perm, true) => "perm",
        (ChemicalType.Coloring, true) => "coloring",
        (ChemicalType.Bleaching, true) => "bleaching",
        (ChemicalType.Other, true) => "chemical treatment",
        _ => chemical.ToString(),
    };

    public static string Of(Allergen allergen, bool en) => (allergen, en) switch
    {
        (Allergen.Fragrance, false) => "fragrância",
        (Allergen.Sulfate, false) => "sulfato",
        (Allergen.Paraben, false) => "parabeno",
        (Allergen.Silicone, false) => "silicone",
        (Allergen.CoconutOil, false) => "óleo de coco",
        (Allergen.NutOils, false) => "óleos de castanhas",
        (Allergen.Lanolin, false) => "lanolina",
        (Allergen.WheatProtein, false) => "proteína do trigo",
        (Allergen.EssentialOils, false) => "óleos essenciais",
        (Allergen.Formaldehyde, false) => "formol",
        (Allergen.Fragrance, true) => "fragrance",
        (Allergen.Sulfate, true) => "sulfate",
        (Allergen.Paraben, true) => "paraben",
        (Allergen.Silicone, true) => "silicone",
        (Allergen.CoconutOil, true) => "coconut oil",
        (Allergen.NutOils, true) => "nut oils",
        (Allergen.Lanolin, true) => "lanolin",
        (Allergen.WheatProtein, true) => "wheat protein",
        (Allergen.EssentialOils, true) => "essential oils",
        (Allergen.Formaldehyde, true) => "formaldehyde",
        _ => allergen.ToString(),
    };

    public static string DayShort(DayOfWeek day, bool en) => (day, en) switch
    {
        (DayOfWeek.Monday, false) => "seg",
        (DayOfWeek.Tuesday, false) => "ter",
        (DayOfWeek.Wednesday, false) => "qua",
        (DayOfWeek.Thursday, false) => "qui",
        (DayOfWeek.Friday, false) => "sex",
        (DayOfWeek.Saturday, false) => "sáb",
        (DayOfWeek.Sunday, false) => "dom",
        _ => day.ToString()[..3],
    };

    /// <summary>"a, b e c" / "a, b and c".</summary>
    public static string JoinList(IEnumerable<string> items, bool en)
    {
        var list = items.ToList();
        return list.Count switch
        {
            0 => string.Empty,
            1 => list[0],
            _ => $"{string.Join(", ", list.Take(list.Count - 1))} {(en ? "and" : "e")} {list[^1]}",
        };
    }

    public static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
