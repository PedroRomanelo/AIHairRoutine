using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using static AIHairRoutine.Tests.GenerationTestData;

namespace AIHairRoutine.Tests;

/// <summary>
/// The fallback is what keeps the endpoint answering when the AI provider fails: any primary failure must
/// become a template result, except a cancellation the caller asked for, which must propagate.
/// </summary>
public sealed class FallbackRoutineGeneratorTests
{
    private static FallbackRoutineGenerator Create(StubRoutineGenerator primary, StubRoutineGenerator fallback) =>
        new(primary, fallback, NullLogger<FallbackRoutineGenerator>.Instance);

    [Fact]
    public async Task Uses_the_primary_result_and_never_calls_the_fallback_when_it_succeeds()
    {
        var primary = StubRoutineGenerator.Returning(Result("claude-test"));
        var fallback = StubRoutineGenerator.Returning(Result("template"));

        var result = await Create(primary, fallback).GenerateAsync(Profile, Schedule, Products, "pt-BR");

        result.Model.ShouldBe("claude-test");
        fallback.Calls.ShouldBe(0);
    }

    public static TheoryData<Exception> PrimaryFailures() =>
    [
        new HttpRequestException("401 Unauthorized"),
        new InvalidOperationException("The model response is missing instructions for 1 product(s)."),
        new System.Text.Json.JsonException("malformed"),
        new TaskCanceledException("HttpClient timeout"), // a timeout, not a caller cancellation
    ];

    [Theory]
    [MemberData(nameof(PrimaryFailures))]
    public async Task Falls_back_to_the_template_when_the_primary_fails(Exception failure)
    {
        var primary = StubRoutineGenerator.Throwing(failure);
        var fallback = StubRoutineGenerator.Returning(Result("template"));

        var result = await Create(primary, fallback).GenerateAsync(Profile, Schedule, Products, "pt-BR");

        result.Model.ShouldBe("template");
        primary.Calls.ShouldBe(1);
        fallback.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Gives_the_fallback_the_same_schedule_products_and_locale()
    {
        var schedule = Schedule;
        var primary = StubRoutineGenerator.Throwing(new HttpRequestException("down"));
        var fallback = StubRoutineGenerator.Returning(Result("template"));

        await Create(primary, fallback).GenerateAsync(Profile, schedule, Products, "en-US");

        fallback.LastSchedule.ShouldBeSameAs(schedule);
        fallback.LastProducts.ShouldBeSameAs(Products);
        fallback.LastLocale.ShouldBe("en-US");
    }

    [Fact]
    public async Task Propagates_a_cancellation_requested_by_the_caller()
    {
        using var cts = new CancellationTokenSource();
        var primary = new StubRoutineGenerator(ct =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Result("claude-test"));
        });
        var fallback = StubRoutineGenerator.Returning(Result("template"));

        await Should.ThrowAsync<OperationCanceledException>(
            () => Create(primary, fallback).GenerateAsync(Profile, Schedule, Products, "pt-BR", cts.Token));

        fallback.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Propagates_a_failure_of_the_fallback_itself()
    {
        var primary = StubRoutineGenerator.Throwing(new HttpRequestException("down"));
        var fallback = StubRoutineGenerator.Throwing(new InvalidOperationException("template broke"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => Create(primary, fallback).GenerateAsync(Profile, Schedule, Products, "pt-BR"));
        ex.Message.ShouldBe("template broke");
    }

    [Fact]
    public async Task An_incomplete_model_answer_ends_up_as_the_template_narrative()
    {
        // End to end with the real generators: the model forgot the mask (p2), so the chat generator
        // throws and the user still gets a complete narrative from the template.
        var client = new FakeChatModelClient("""{ "summary": "ok", "products": [ { "ref": "p1", "how": "Lave" } ] }""");
        var options = Options.Create(new GenerationOptions
        {
            Providers = { ["Anthropic"] = new ProviderOptions { ApiKey = "key", Model = "claude-test" } },
        });
        var generator = new FallbackRoutineGenerator(
            new ChatRoutineGenerator(client, new RoutinePromptBuilder(), options),
            new TemplateRoutineGenerator(),
            NullLogger<FallbackRoutineGenerator>.Instance);

        var result = await generator.GenerateAsync(Profile, Schedule, Products, "pt-BR");

        result.Model.ShouldBe("template");
        result.Narrative.ProductNotes.Select(n => n.ProductId).ShouldBe([Shampoo.Id, Mask.Id], ignoreOrder: true);
        result.Narrative.ProductNotes.ShouldAllBe(n => n.How.Length > 0 && n.Why.Length > 0);
    }
}
