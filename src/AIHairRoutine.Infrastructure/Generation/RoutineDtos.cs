using System.Text.Json.Serialization;

namespace AIHairRoutine.Infrastructure.Generation;

// Routine shape returned by the model (provider-independent; mapped to the domain HairRoutine).

internal sealed class RoutineDto
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("steps")]
    public List<RoutineStepDto> Steps { get; set; } = [];

    [JsonPropertyName("tips")]
    public List<string> Tips { get; set; } = [];
}

internal sealed class RoutineStepDto
{
    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    [JsonPropertyName("frequency")]
    public string? Frequency { get; set; }

    [JsonPropertyName("productId")]
    public int? ProductId { get; set; }

    [JsonPropertyName("how")]
    public string? How { get; set; }

    [JsonPropertyName("why")]
    public string? Why { get; set; }
}
