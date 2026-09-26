namespace AIHairRoutine.Application.Models;

// All enums are serialized as snake_case strings (e.g. light_blonde, frizz_control, twice_weekly).

/// <summary>Curl pattern of the hair (liso / ondulado 2A-2C / cacheado 3A-3C / crespo 4A-4C).</summary>
public enum HairType
{
    Straight,
    Wavy,
    Curly,
    Coily,
}

/// <summary>Strand thickness (fino / médio / grosso).</summary>
public enum HairThickness
{
    Fine,
    Medium,
    Coarse,
}

/// <summary>Hair tone.</summary>
public enum HairTone
{
    LightBlonde,
    Blonde,
    LightBrown,
    Brown,
    DarkBrown,
    Black,
}

/// <summary>Current condition of the hair. Multiple can apply at once.</summary>
public enum HairCondition
{
    Dry,
    Normal,
    Oily,
    Damaged,
    Frizzy,
    Dull,
}

/// <summary>Chemical process the hair has been through.</summary>
public enum ChemicalType
{
    Relaxation,
    Straightening,
    Perm,
    Coloring,
    Bleaching,
    Other,
}

/// <summary>How often the chemical process is touched up.</summary>
public enum TouchUpFrequency
{
    Monthly,
    Bimonthly,
    Quarterly,
    Semiannual,
    Annual,
}

/// <summary>A care priority the schedule should focus on.</summary>
public enum HairPriority
{
    Hydration,
    Nutrition,
    Reconstruction,
    FrizzControl,
    OilControl,
    Shine,
    HairLossControl,
    Volume,
}

/// <summary>The three treatment axes of a hair schedule (cronograma H/N/R).</summary>
public enum TreatmentType
{
    Hydration,
    Nutrition,
    Reconstruction,
}

/// <summary>Product category.</summary>
public enum ProductCategory
{
    Shampoo,
    Conditioner,
    Mask,
    LeaveIn,
    Oil,
    Serum,
    Finisher,
    Treatment,
}

/// <summary>Recommended usage frequency of a product.</summary>
public enum UsageFrequency
{
    Daily,
    TwiceWeekly,
    Weekly,
    Biweekly,
    Monthly,
}

/// <summary>Closed list of allergens the user can tick; products declare which ones they contain.</summary>
public enum Allergen
{
    Fragrance,
    Sulfate,
    Paraben,
    Silicone,
    CoconutOil,
    NutOils,
    Lanolin,
    WheatProtein,
    EssentialOils,
    Formaldehyde,
}

/// <summary>Why a product that would otherwise fit was left out.</summary>
public enum ExclusionReason
{
    Allergy,
    UnsafeForRecentChemical,
    Contraindication,
}

/// <summary>Which strategy produced the profile.</summary>
public enum ProfileSource
{
    Rules,
    Jev,
}
