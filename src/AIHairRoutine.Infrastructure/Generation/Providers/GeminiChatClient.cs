using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation.Providers;

/// <summary>Adapter over the Google Gemini API (<c>POST v1beta/models/{model}:generateContent</c>).</summary>
public sealed class GeminiChatClient(
    HttpClient http,
    IApiKeyAuthenticator authenticator,
    IOptions<GenerationOptions> options,
    ILogger<GeminiChatClient> logger) : IChatModelClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AiProvider Provider => AiProvider.Gemini;

    public async Task<string> CompleteAsync(ChatPrompt prompt, CancellationToken ct = default)
    {
        var opt = options.Value;
        var body = new Request(
            SystemInstruction: new Content { Parts = [new Part(prompt.System)] },
            Contents: [new Content { Role = "user", Parts = [new Part(prompt.User)] }],
            GenerationConfig: new Config(opt.MaxTokens));

        var uri = $"v1beta/models/{opt.Active.Model}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body, options: Json),
        };
        authenticator.Apply(request, opt.Active);

        using var resp = await http.SendAsync(request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(ct);
            logger.LogWarning("{Provider} returned {Status}: {Detail}", Provider, (int)resp.StatusCode, detail);
            resp.EnsureSuccessStatusCode();
        }

        var payload = await resp.Content.ReadFromJsonAsync<Response>(Json, ct)
            ?? throw new InvalidOperationException("Gemini returned an empty response.");

        var parts = payload.Candidates.FirstOrDefault()?.Content?.Parts ?? [];
        return string.Concat(parts.Select(p => p.Text));
    }

    private sealed record Request(
        [property: JsonPropertyName("system_instruction")] Content SystemInstruction,
        [property: JsonPropertyName("contents")] IReadOnlyList<Content> Contents,
        [property: JsonPropertyName("generationConfig")] Config GenerationConfig);

    private sealed record Config(
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens);

    private sealed class Content
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public IReadOnlyList<Part> Parts { get; set; } = [];
    }

    private sealed record Part([property: JsonPropertyName("text")] string? Text);

    private sealed class Response
    {
        [JsonPropertyName("candidates")]
        public List<Candidate> Candidates { get; set; } = [];
    }

    private sealed class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }
}
