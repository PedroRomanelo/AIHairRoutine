using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation.Providers;

/// <summary>
/// Adapter over the OpenAI Chat Completions wire format (<c>POST v1/chat/completions</c> with a
/// <c>choices[].message.content</c> response). DeepSeek is API-compatible, so both providers share
/// this adapter and differ only in their concrete <see cref="Provider"/>, base URL and model.
/// </summary>
public abstract class OpenAiCompatibleChatClient(
    HttpClient http,
    IApiKeyAuthenticator authenticator,
    IOptions<GenerationOptions> options,
    ILogger logger) : IChatModelClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public abstract AiProvider Provider { get; }

    public async Task<string> CompleteAsync(ChatPrompt prompt, CancellationToken ct = default)
    {
        var opt = options.Value;
        var body = new Request(
            Model: opt.Active.Model,
            MaxTokens: opt.MaxTokens,
            Messages:
            [
                new Message("system", prompt.System),
                new Message("user", prompt.User),
            ]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
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
            ?? throw new InvalidOperationException($"{Provider} returned an empty response.");

        return payload.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    private sealed record Request(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("messages")] IReadOnlyList<Message> Messages);

    private sealed record Message(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed class Response
    {
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; } = [];
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }
}
