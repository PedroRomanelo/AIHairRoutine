using System.Net;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Profiling;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AIHairRoutine.Tests;

/// <summary>
/// JEV costs money and latency, so the hybrid calls it only when the rules can't read the input
/// (an "other" chemical or a goal with no recognized keyword), and falls back to the rules if it fails.
/// </summary>
public sealed class HybridProfilerTests
{
    private static readonly string JevAnswer =
        JevTestData.Response("jev-1", ("needs_hairloss_control", 0.9), ("needs_volume", 0.8));

    private static (HybridProfiler Hybrid, FakeHttpHandler Handler) Create(string responseJson = "", HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new FakeHttpHandler(status, responseJson.Length > 0 ? responseJson : JevAnswer);
        return (Create(JevTestData.Create(handler)), handler);
    }

    private static HybridProfiler Create(Infrastructure.Jev.JevProfiler? jev) =>
        new(TestData.Rules, NullLogger<HybridProfiler>.Instance, jev);

    private static HairAssessment WithGoal(string goal) => TestData.Assessment() with { MainGoal = goal };

    [Fact]
    public async Task Uses_rules_without_calling_jev_when_the_goal_is_recognized()
    {
        var (hybrid, handler) = Create();

        var result = await hybrid.ProfileAsync(WithGoal("quero hidratar e reduzir o frizz"));

        result.Source.ShouldBe(ProfileSource.Rules);
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Calls_jev_when_the_goal_has_no_recognized_keyword()
    {
        var (hybrid, handler) = Create();

        var result = await hybrid.ProfileAsync(WithGoal(JevTestData.AmbiguousGoal));

        handler.Calls.ShouldBe(1);
        result.Source.ShouldBe(ProfileSource.Jev);
        result.Priorities.ShouldBe([HairPriority.HairLossControl, HairPriority.Volume]);
    }

    [Fact]
    public async Task Calls_jev_for_an_unspecified_chemical_even_with_a_clear_goal()
    {
        var (hybrid, handler) = Create();
        var assessment = WithGoal("quero hidratar") with { Chemical = TestData.Chemical(ChemicalType.Other, "há 1 mês") };

        var result = await hybrid.ProfileAsync(assessment);

        handler.Calls.ShouldBe(1);
        result.Source.ShouldBe(ProfileSource.Jev);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Does_not_call_jev_for_an_empty_goal(string goal)
    {
        var (hybrid, handler) = Create();

        (await hybrid.ProfileAsync(WithGoal(goal))).Source.ShouldBe(ProfileSource.Rules);
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Uses_rules_when_jev_is_disabled_even_for_ambiguous_input()
    {
        var hybrid = Create(jev: null);
        var assessment = WithGoal(JevTestData.AmbiguousGoal) with { Chemical = TestData.Chemical(ChemicalType.Other, "há 1 mês") };

        (await hybrid.ProfileAsync(assessment)).Source.ShouldBe(ProfileSource.Rules);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, """{ "error": "boom" }""")]
    [InlineData(HttpStatusCode.TooManyRequests, """{ "error": "rate limited" }""")]
    [InlineData(HttpStatusCode.OK, "null")]
    public async Task Falls_back_to_rules_when_jev_fails(HttpStatusCode status, string response)
    {
        var (hybrid, handler) = Create(response, status);
        var assessment = WithGoal(JevTestData.AmbiguousGoal);

        var result = await hybrid.ProfileAsync(assessment);

        handler.Calls.ShouldBe(1);
        result.Source.ShouldBe(ProfileSource.Rules);
        result.Priorities.ShouldBe(TestData.Rules.Profile(assessment).Priorities);
    }

    [Fact]
    public async Task Falls_back_to_rules_when_jev_times_out()
    {
        var hybrid = Create(JevTestData.Create(new ThrowingHttpHandler(new TaskCanceledException("JEV timeout"))));

        var result = await hybrid.ProfileAsync(WithGoal(JevTestData.AmbiguousGoal));

        result.Source.ShouldBe(ProfileSource.Rules);
    }

    [Fact]
    public async Task Propagates_a_cancellation_requested_by_the_caller()
    {
        var (hybrid, _) = Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => hybrid.ProfileAsync(WithGoal(JevTestData.AmbiguousGoal), cts.Token));
    }
}
