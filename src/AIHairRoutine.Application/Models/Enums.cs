namespace AIHairRoutine.Application.Models;

/// <summary>Curl pattern of the hair.</summary>
public enum HairType
{
    Straight,
    Wavy,
    Curly,
    Coily,
}

/// <summary>Chemical process the hair has been through.</summary>
public enum ChemicalTreatment
{
    None,
    Coloring,
    Progressive,
    Relaxation,
    Other,
}

/// <summary>Overall oiliness/hydration condition of scalp and strands.</summary>
public enum HairCondition
{
    Dry,
    Normal,
    Oily,
}

/// <summary>Structural damage level of the fiber.</summary>
public enum DamageLevel
{
    None,
    Mild,
    Moderate,
    Severe,
}

/// <summary>Frizz intensity.</summary>
public enum FrizzLevel
{
    Low,
    Medium,
    High,
}

/// <summary>A care priority the routine should focus on. Serialized as snake_case (e.g. frizz_control).</summary>
public enum HairPriority
{
    Hydration,
    FrizzControl,
    DamageRepair,
    OilControl,
    HairLossControl,
}

/// <summary>Which strategy produced the profile.</summary>
public enum ProfileSource
{
    Rules,
    Jev,
}
