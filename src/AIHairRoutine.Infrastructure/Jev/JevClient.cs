using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AIHairRoutine.Infrastructure.Jev;

/// <summary>Thin typed HttpClient over the JEV /v1/systemone endpoint.</summary>
public sealed class JevClient(HttpClient http, ILogger<JevClient> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Calls JEV. Throws on transport/HTTP errors so the caller can fall back.</summary>
    public async Task<JevResponse?> EvaluateAsync(object body, CancellationToken ct)
    {
        using var resp = await http.PostAsJsonAsync("v1/systemone", body, Json, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(ct);
            logger.LogWarning("JEV returned {Status}: {Detail}", (int)resp.StatusCode, detail);
            resp.EnsureSuccessStatusCode();
        }

        return await resp.Content.ReadFromJsonAsync<JevResponse>(Json, ct);
    }
}

/// <summary>JEV response envelope. Answers are kept as JsonElement so each question type is read on demand.</summary>
public sealed class JevResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("answers")]
    public Dictionary<string, JsonElement> Answers { get; set; } = new();
}
