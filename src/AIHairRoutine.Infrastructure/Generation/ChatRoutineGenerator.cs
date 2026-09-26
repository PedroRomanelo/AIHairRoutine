using System.Text.Json;
using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation.Providers;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation;

/// <summary>
/// Provider-agnostic narrative generator (the primary Strategy). Delegates the LLM call to whichever
/// <see cref="IChatModelClient"/> adapter is active (Anthropic/OpenAI/Gemini/DeepSeek) and maps the
/// returned JSON to the domain narrative. Provider selection and API-key handling live behind the adapter.
/// </summary>
public sealed class ChatRoutineGenerator(
    IChatModelClient client,
    RoutinePromptBuilder promptBuilder,
    IOptions<GenerationOptions> options) : IRoutineGenerator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RoutineResult> GenerateAsync(
        ProfileResult profile,
        HairSchedule schedule,
        IReadOnlyList<Product> products,
        string locale,
        CancellationToken ct = default)
    {
        var prompt = promptBuilder.Build(profile, schedule, products, locale);

        var text = await client.CompleteAsync(new ChatPrompt(prompt.System, prompt.User), ct);
        var dto = ParseNarrative(text);

        return new RoutineResult
        {
            Narrative = Map(dto, prompt.ProductAliases, products),
            Model = options.Value.Active.Model,
            FromCache = false,
        };
    }

    private static NarrativeDto ParseNarrative(string text)
    {
        // The model is asked for pure JSON, but defensively extract the outermost object.
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new InvalidOperationException("The model response did not contain a JSON object.");

        var json = text[start..(end + 1)];
        return JsonSerializer.Deserialize<NarrativeDto>(json, Json)
            ?? throw new InvalidOperationException("The model JSON could not be parsed into a narrative.");
    }

    /// <summary>Maps aliases back to product ids. An incomplete answer throws so the template fallback takes over.</summary>
    private static RoutineNarrative Map(NarrativeDto dto, IReadOnlyDictionary<string, Guid> aliases, IReadOnlyList<Product> products)
    {
        var notes = dto.Products
            .Where(n => n.Ref is not null && aliases.ContainsKey(n.Ref) && !string.IsNullOrWhiteSpace(n.How))
            .Select(n => new ProductNote(aliases[n.Ref!], n.How!, n.Why ?? string.Empty))
            .DistinctBy(n => n.ProductId)
            .ToList();

        var missing = products.Count(p => notes.All(n => n.ProductId != p.Id));
        if (missing > 0)
            throw new InvalidOperationException($"The model response is missing instructions for {missing} product(s).");

        return new RoutineNarrative
        {
            Summary = dto.Summary ?? string.Empty,
            ProductNotes = notes,
            Tips = dto.Tips,
        };
    }
}
