using System.Net.Http.Json;
using System.Text.Json;
using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Claude;

/// <summary>Adapter over the Anthropic Messages API. Asks Claude for the routine as strict JSON.</summary>
public sealed class ClaudeRoutineGenerator(
    HttpClient http,
    ClaudePromptBuilder promptBuilder,
    IOptions<AnthropicOptions> options,
    ILogger<ClaudeRoutineGenerator> logger) : IRoutineGenerator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RoutineResult> GenerateAsync(
        ProfileResult profile,
        IReadOnlyList<ProductMatch> products,
        string locale,
        CancellationToken ct = default)
    {
        var opt = options.Value;
        var request = new AnthropicRequest(
            Model: opt.Model,
            MaxTokens: opt.MaxTokens,
            System: promptBuilder.BuildSystem(),
            Messages: [new AnthropicMessage("user", promptBuilder.BuildUser(profile, products, locale))]);

        using var resp = await http.PostAsJsonAsync("v1/messages", request, Json, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Claude returned {Status}: {Detail}", (int)resp.StatusCode, detail);
            resp.EnsureSuccessStatusCode();
        }

        var body = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(Json, ct)
            ?? throw new InvalidOperationException("Claude returned an empty response.");

        var text = string.Concat(body.Content.Where(c => c.Type == "text").Select(c => c.Text));
        var dto = ParseRoutine(text);

        return new RoutineResult
        {
            Routine = Map(dto),
            Model = opt.Model,
            FromCache = false,
        };
    }

    private static RoutineDto ParseRoutine(string text)
    {
        // The model is asked for pure JSON, but defensively extract the outermost object.
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new InvalidOperationException("Claude response did not contain a JSON object.");

        var json = text[start..(end + 1)];
        return JsonSerializer.Deserialize<RoutineDto>(json, Json)
            ?? throw new InvalidOperationException("Claude JSON could not be parsed into a routine.");
    }

    private static HairRoutine Map(RoutineDto dto) => new()
    {
        Summary = dto.Summary ?? string.Empty,
        Steps = dto.Steps.Select(s => new RoutineStep
        {
            Order = s.Order,
            Phase = s.Phase ?? "step",
            Frequency = s.Frequency ?? string.Empty,
            ProductId = s.ProductId,
            How = s.How ?? string.Empty,
            Why = s.Why ?? string.Empty,
        }).ToList(),
        Tips = dto.Tips,
    };
}
