using System.Text.Json.Serialization;

namespace AIHairRoutine.Infrastructure.Generation;

// Narrative shape returned by the model (provider-independent; mapped to the domain RoutineNarrative).

internal sealed class NarrativeDto
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("products")]
    public List<ProductNoteDto> Products { get; set; } = [];

    [JsonPropertyName("tips")]
    public List<string> Tips { get; set; } = [];
}

internal sealed class ProductNoteDto
{
    [JsonPropertyName("ref")]
    public string? Ref { get; set; }

    [JsonPropertyName("how")]
    public string? How { get; set; }

    [JsonPropertyName("why")]
    public string? Why { get; set; }
}
