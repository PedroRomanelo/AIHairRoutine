using System.Text.Json.Serialization;

namespace AIHairRoutine.Infrastructure.Claude;

// Request

internal sealed record AnthropicRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("system")] string System,
    [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages);

internal sealed record AnthropicMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

// Response

internal sealed class AnthropicResponse
{
    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; set; } = new();

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}

internal sealed class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

// Routine shape returned by the model (mapped to the domain HairRoutine)

internal sealed class RoutineDto
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("steps")]
    public List<RoutineStepDto> Steps { get; set; } = new();

    [JsonPropertyName("tips")]
    public List<string> Tips { get; set; } = new();
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
