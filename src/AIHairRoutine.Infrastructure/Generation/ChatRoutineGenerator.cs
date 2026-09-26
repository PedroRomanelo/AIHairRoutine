using System.Text.Json;
using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation.Providers;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation;

/// <summary>
/// Provider-agnostic routine generator (the primary Strategy). Delegates the LLM call to whichever
/// <see cref="IChatModelClient"/> adapter is active (Anthropic/OpenAI/Gemini/DeepSeek) and maps the
/// returned JSON to the domain routine. Provider selection and API-key handling live behind the adapter.
/// </summary>
public sealed class ChatRoutineGenerator(
    IChatModelClient client,
    RoutinePromptBuilder promptBuilder,
    IOptions<GenerationOptions> options) : IRoutineGenerator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RoutineResult> GenerateAsync(
        ProfileResult profile,
        IReadOnlyList<ProductMatch> products,
        string locale,
        CancellationToken ct = default)
    {
        var prompt = new ChatPrompt(
            promptBuilder.BuildSystem(),
            promptBuilder.BuildUser(profile, products, locale));

        var text = await client.CompleteAsync(prompt, ct);
        var dto = ParseRoutine(text);

        return new RoutineResult
        {
            Routine = Map(dto),
            Model = options.Value.Active.Model,
            FromCache = false,
        };
    }

    private static RoutineDto ParseRoutine(string text)
    {
        // The model is asked for pure JSON, but defensively extract the outermost object.
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new InvalidOperationException("The model response did not contain a JSON object.");

        var json = text[start..(end + 1)];
        return JsonSerializer.Deserialize<RoutineDto>(json, Json)
            ?? throw new InvalidOperationException("The model JSON could not be parsed into a routine.");
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
