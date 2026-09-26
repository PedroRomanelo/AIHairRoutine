using System.Text.Json;
using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation;
using Microsoft.Extensions.Options;
using Shouldly;
using static AIHairRoutine.Tests.GenerationTestData;

namespace AIHairRoutine.Tests;

/// <summary>
/// The generator sends the schedule with short aliases (p1 = shampoo, p2 = mask) and must map the model's
/// answer back to product ids. Anything the model gets wrong has to either be ignored safely or throw, so
/// the template fallback takes over — never a schedule with missing or misplaced instructions.
/// </summary>
public sealed class ChatRoutineGeneratorTests
{
    private const string CompleteAnswer = """
        {
          "summary": "Cronograma focado em hidratação.",
          "products": [
            { "ref": "p1", "how": "Massageie o couro cabeludo.", "why": "Limpa sem ressecar." },
            { "ref": "p2", "how": "Deixe agir 15 min.", "why": "Repõe a água dos fios." }
          ],
          "tips": ["Use água morna.", "Durma com fronha de cetim."]
        }
        """;

    private static (ChatRoutineGenerator Generator, FakeChatModelClient Client) Create(string modelAnswer)
    {
        var client = new FakeChatModelClient(modelAnswer);
        var options = Options.Create(new GenerationOptions
        {
            Provider = AiProvider.Anthropic,
            Providers = { ["Anthropic"] = new ProviderOptions { ApiKey = "key", Model = "claude-test" } },
        });

        return (new ChatRoutineGenerator(client, new RoutinePromptBuilder(), options), client);
    }

    private static Task<Application.Models.RoutineResult> Generate(ChatRoutineGenerator generator) =>
        generator.GenerateAsync(Profile, Schedule, Products, "pt-BR");

    [Fact]
    public async Task Maps_each_alias_back_to_its_product()
    {
        var (generator, _) = Create(CompleteAnswer);

        var result = await Generate(generator);

        var notes = result.Narrative.ProductNotes.ToDictionary(n => n.ProductId);
        notes.Keys.ShouldBe([Shampoo.Id, Mask.Id], ignoreOrder: true);
        notes[Shampoo.Id].How.ShouldBe("Massageie o couro cabeludo.");
        notes[Shampoo.Id].Why.ShouldBe("Limpa sem ressecar.");
        notes[Mask.Id].How.ShouldBe("Deixe agir 15 min.");
        notes[Mask.Id].Why.ShouldBe("Repõe a água dos fios.");
    }

    [Fact]
    public async Task Returns_summary_tips_and_the_active_model()
    {
        var (generator, _) = Create(CompleteAnswer);

        var result = await Generate(generator);

        result.Narrative.Summary.ShouldBe("Cronograma focado em hidratação.");
        result.Narrative.Tips.ShouldBe(["Use água morna.", "Durma com fronha de cetim."]);
        result.Model.ShouldBe("claude-test");
        result.FromCache.ShouldBeFalse();
    }

    [Fact]
    public async Task Sends_short_aliases_instead_of_product_ids()
    {
        var (generator, client) = Create(CompleteAnswer);

        await Generate(generator);

        var prompt = client.LastPrompt.ShouldNotBeNull();
        prompt.System.ShouldNotBeNullOrWhiteSpace();
        prompt.User.ShouldContain("\"ref\":\"p1\"");
        prompt.User.ShouldContain("\"ref\":\"p2\"");
        prompt.User.ShouldNotContain(Shampoo.Id.ToString());
        prompt.User.ShouldNotContain(Mask.Id.ToString());
    }

    [Fact]
    public async Task Extracts_the_json_even_when_the_model_wraps_it_in_text()
    {
        var (generator, _) = Create($"Claro! Aqui está o cronograma:\n```json\n{CompleteAnswer}\n```\nQualquer dúvida, me avise.");

        var result = await Generate(generator);

        result.Narrative.ProductNotes.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Ignores_unknown_refs_and_keeps_the_first_note_of_a_repeated_ref()
    {
        var (generator, _) = Create("""
            {
              "summary": "ok",
              "products": [
                { "ref": "p1", "how": "primeira", "why": "a" },
                { "ref": "p1", "how": "repetida", "why": "b" },
                { "ref": "p9", "how": "inventada", "why": "c" },
                { "ref": "p2", "how": "máscara", "why": "d" }
              ]
            }
            """);

        var result = await Generate(generator);

        result.Narrative.ProductNotes.Count.ShouldBe(2);
        result.Narrative.ProductNotes.Single(n => n.ProductId == Shampoo.Id).How.ShouldBe("primeira");
    }

    [Fact]
    public async Task Tolerates_missing_optional_fields()
    {
        var (generator, _) = Create("""
            { "products": [ { "ref": "p1", "how": "Lave" }, { "ref": "p2", "how": "Aplique" } ] }
            """);

        var result = await Generate(generator);

        result.Narrative.Summary.ShouldBeEmpty();
        result.Narrative.Tips.ShouldBeEmpty();
        result.Narrative.ProductNotes.ShouldAllBe(n => n.Why == string.Empty);
    }

    [Theory]
    [InlineData("""{ "summary": "ok", "products": [ { "ref": "p1", "how": "Lave", "why": "a" } ] }""")]
    [InlineData("""{ "summary": "ok", "products": [ { "ref": "p1", "how": "Lave" }, { "ref": "p2", "how": "   " } ] }""")]
    [InlineData("""{ "summary": "ok", "products": [ { "ref": "p1", "how": "Lave" }, { "how": "sem ref" } ] }""")]
    public async Task Throws_when_any_product_is_left_without_instructions(string modelAnswer)
    {
        var (generator, _) = Create(modelAnswer);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Generate(generator));
        ex.Message.ShouldContain("missing instructions for 1 product");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Desculpe, não consigo ajudar com isso.")]
    [InlineData("} texto {")]
    public async Task Throws_when_the_answer_has_no_json_object(string modelAnswer)
    {
        var (generator, _) = Create(modelAnswer);

        await Should.ThrowAsync<InvalidOperationException>(() => Generate(generator));
    }

    [Fact]
    public async Task Throws_when_the_json_is_malformed()
    {
        var (generator, _) = Create("""{ "summary": "ok", "products": [ { "ref": "p1" """ + "}");

        await Should.ThrowAsync<JsonException>(() => Generate(generator));
    }
}
