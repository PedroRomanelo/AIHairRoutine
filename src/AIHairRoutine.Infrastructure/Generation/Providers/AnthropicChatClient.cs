using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation.Providers;

/// <summary>Adapter over the Anthropic Messages API (<c>POST v1/messages</c>).</summary>
public sealed class AnthropicChatClient(
    HttpClient http,
    IApiKeyAuthenticator authenticator,
    IOptions<GenerationOptions> options,
    ILogger<AnthropicChatClient> logger) : IChatModelClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AiProvider Provider => AiProvider.Anthropic;

    public async Task<string> CompleteAsync(ChatPrompt prompt, CancellationToken ct = default)
    {
        var opt = options.Value;
        var body = new Request(
            Model: opt.Active.Model,
            MaxTokens: opt.MaxTokens,
            System: prompt.System,
            Messages: [new Message("user", prompt.User)]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
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
            ?? throw new InvalidOperationException("Anthropic returned an empty response.");

        return string.Concat(payload.Content.Where(c => c.Type == "text").Select(c => c.Text));
    }

    private sealed record Request(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] IReadOnlyList<Message> Messages);

    private sealed record Message(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed class Response
    {
        [JsonPropertyName("content")]
        public List<ContentBlock> Content { get; set; } = [];
    }

    private sealed class ContentBlock
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
