using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using static AIHairRoutine.Tests.GenerationTestData;

namespace AIHairRoutine.Tests;

/// <summary>
/// The cache is the biggest cost lever (equal profiles → one LLM call), but its key must include everything
/// that changes the text: a narrow key would hand one user's text to another profile, locale or schedule.
/// Each test gets its own in-memory HybridCache.
/// </summary>
public sealed class CachingRoutineGeneratorTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();

    public void Dispose() => _services.Dispose();

    private CachingRoutineGenerator Create(StubRoutineGenerator inner) => new(
        inner,
        _services.GetRequiredService<HybridCache>(),
        Options.Create(new RoutineCacheOptions { ExpirationMinutes = 60 }));

    private static StubRoutineGenerator CountingInner() =>
        new(_ => Task.FromResult(Result("claude-test")));

    [Fact]
    public async Task Serves_an_identical_request_from_the_cache_without_calling_the_model_again()
    {
        var inner = CountingInner();
        var cache = Create(inner);

        var first = await cache.GenerateAsync(Profile, Schedule, Products, "pt-BR");
        var second = await cache.GenerateAsync(Profile, Schedule, Products, "pt-BR");

        inner.Calls.ShouldBe(1);
        first.FromCache.ShouldBeFalse();
        second.FromCache.ShouldBeTrue();
        second.Model.ShouldBe(first.Model);
        second.Narrative.Summary.ShouldBe(first.Narrative.Summary);
    }

    [Fact]
    public async Task Rebuilt_but_equal_inputs_hit_the_same_entry()
    {
        // Every call rebuilds the profile and schedule objects: the key depends on values, not references.
        var inner = CountingInner();
        var cache = Create(inner);

        await cache.GenerateAsync(TestData.Rules.Profile(TestData.Assessment()), Schedule, [Shampoo, Mask], "pt-BR");
        await cache.GenerateAsync(TestData.Rules.Profile(TestData.Assessment()), Schedule, [Shampoo, Mask], "pt-BR");

        inner.Calls.ShouldBe(1);
    }

    public static TheoryData<string, Func<CachingRoutineGenerator, Task>> Variations() => new()
    {
        { "locale", c => c.GenerateAsync(Profile, Schedule, Products, "en-US") },
        {
            "allergies",
            c => c.GenerateAsync(TestData.Rules.Profile(TestData.Assessment() with { Allergies = [Allergen.Fragrance] }), Schedule, Products, "pt-BR")
        },
        {
            "priorities",
            c => c.GenerateAsync(Profile with { Priorities = [HairPriority.Volume, HairPriority.Shine] }, Schedule, Products, "pt-BR")
        },
        { "schedule", c => c.GenerateAsync(Profile, Schedule with { WashDays = [DayOfWeek.Tuesday] }, Products, "pt-BR") },
        { "special care", c => c.GenerateAsync(Profile, Schedule with { SpecialCare = ["Outro cuidado."] }, Products, "pt-BR") },
        { "products", c => c.GenerateAsync(Profile, Schedule, [Shampoo], "pt-BR") },
    };

    [Theory]
    [MemberData(nameof(Variations))]
    public async Task Generates_again_when_anything_that_shapes_the_text_changes(string changed, Func<CachingRoutineGenerator, Task> variation)
    {
        var inner = CountingInner();
        var cache = Create(inner);
        await cache.GenerateAsync(Profile, Schedule, Products, "pt-BR");

        await variation(cache);

        inner.Calls.ShouldBe(2, $"changing the {changed} must not reuse the cached text");
    }

    [Fact]
    public async Task Does_not_cache_failures()
    {
        int attempt = 0;
        var inner = new StubRoutineGenerator(_ => ++attempt == 1
            ? Task.FromException<RoutineResult>(new HttpRequestException("down"))
            : Task.FromResult(Result("claude-test")));
        var cache = Create(inner);

        await Should.ThrowAsync<HttpRequestException>(() => cache.GenerateAsync(Profile, Schedule, Products, "pt-BR"));
        var retry = await cache.GenerateAsync(Profile, Schedule, Products, "pt-BR");

        inner.Calls.ShouldBe(2);
        retry.FromCache.ShouldBeFalse();
    }

    [Fact]
    public async Task Concurrent_identical_requests_share_a_single_model_call()
    {
        var release = new TaskCompletionSource();
        var inner = new StubRoutineGenerator(async _ =>
        {
            await release.Task;
            return Result("claude-test");
        });
        var cache = Create(inner);

        var requests = Enumerable.Range(0, 5)
            .Select(_ => cache.GenerateAsync(Profile, Schedule, Products, "pt-BR"))
            .ToList();
        release.SetResult();
        var results = await Task.WhenAll(requests);

        inner.Calls.ShouldBe(1);
        results.Count(r => !r.FromCache).ShouldBe(1);
    }
}
