using System.Net;
using System.Text.Json;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Jev;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AIHairRoutine.Tests;

internal static class JevTestData
{
    public const string AmbiguousGoal = "quero ficar bonita para o casamento";

    public static JevProfiler Create(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://jev.test/") };
        return new JevProfiler(
            new JevClient(http, NullLogger<JevClient>.Instance),
            TestData.Rules,
            Options.Create(new JevOptions { ApiKey = "jev-key", Model = "jev-test" }),
            NullLogger<JevProfiler>.Instance);
    }

    /// <summary>A JEV answer with one noul probability per "needs_*" question.</summary>
    public static string Response(string? model, params (string Question, double Noul)[] answers) =>
        JsonSerializer.Serialize(new
        {
            model,
            answers = answers.ToDictionary(a => a.Question, a => new { noul = a.Noul }),
        });
}

/// <summary>
/// JEV only reorders the priorities: the typed profile (conditions, chemistry, H/N/R needs, allergies)
/// is already explicit in the questionnaire and must keep coming from the rules.
/// </summary>
public sealed class JevProfilerTests
{
    private static readonly HairAssessment Assessment =
        TestData.Assessment(HairType.Curly, HairCondition.Dry, HairCondition.Frizzy) with
        {
            MainGoal = JevTestData.AmbiguousGoal,
            Chemical = TestData.Chemical(ChemicalType.Coloring, "há 2 meses"),
            Allergies = [Allergen.Sulfate],
        };

    private static (JevProfiler Jev, FakeHttpHandler Handler) Create(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new FakeHttpHandler(status, responseJson);
        return (JevTestData.Create(handler), handler);
    }

    [Fact]
    public async Task Sends_the_profile_state_and_one_noul_question_per_priority()
    {
        var (jev, handler) = Create(JevTestData.Response("jev-1", ("needs_hydration", 0.9)));

        await jev.ProfileAsync(Assessment);

        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Uri.ShouldBe(new Uri("https://jev.test/v1/systemone"));

        var body = JsonDocument.Parse(handler.Body!).RootElement;
        body.GetProperty("model").GetString().ShouldBe("jev-test");

        var state = body.GetProperty("state");
        state.GetProperty("hairType").GetString().ShouldBe("curly");
        state.GetProperty("conditions").EnumerateArray().Select(c => c.GetString()).ShouldBe(["dry", "frizzy"]);
        state.GetProperty("chemical").GetString().ShouldBe("coloring");
        state.GetProperty("chemicalDaysAgo").GetInt32().ShouldBe(60);
        state.GetProperty("mainGoal").GetString().ShouldBe(JevTestData.AmbiguousGoal);

        var questions = body.GetProperty("questions").EnumerateObject().ToList();
        questions.Select(q => q.Name).ShouldBe(
        [
            "needs_hydration", "needs_nutrition", "needs_reconstruction", "needs_frizz_control",
            "needs_oil_control", "needs_shine", "needs_hairloss_control", "needs_volume",
        ], ignoreOrder: true);
        questions.ShouldAllBe(q => q.Value.GetProperty("type").GetString() == "noul");
    }

    [Fact]
    public async Task Ranks_priorities_by_probability_keeping_the_confident_ones_up_to_four()
    {
        var (jev, _) = Create(JevTestData.Response("jev-1",
            ("needs_hydration", 0.55),
            ("needs_hairloss_control", 0.95),
            ("needs_volume", 0.40),
            ("needs_shine", 0.80),
            ("needs_nutrition", 0.70),
            ("needs_frizz_control", 0.60),
            ("needs_oil_control", 0.50)));

        var result = await jev.ProfileAsync(Assessment);

        result.Priorities.ShouldBe(
        [
            HairPriority.HairLossControl, HairPriority.Shine, HairPriority.Nutrition, HairPriority.FrizzControl,
        ]);
    }

    [Fact]
    public async Task A_probability_of_exactly_one_half_is_not_confident()
    {
        var (jev, _) = Create(JevTestData.Response("jev-1",
            ("needs_hydration", 0.90),
            ("needs_shine", 0.80),
            ("needs_volume", 0.50)));

        var result = await jev.ProfileAsync(Assessment);

        result.Priorities.ShouldBe([HairPriority.Hydration, HairPriority.Shine]);
    }

    [Fact]
    public async Task Keeps_the_two_strongest_signals_when_fewer_than_two_are_confident()
    {
        var (jev, _) = Create(JevTestData.Response("jev-1",
            ("needs_hydration", 0.30),
            ("needs_volume", 0.45),
            ("needs_shine", 0.10)));

        var result = await jev.ProfileAsync(Assessment);

        result.Priorities.ShouldBe([HairPriority.Volume, HairPriority.Hydration]);
    }

    [Fact]
    public async Task Keeps_the_rule_based_priorities_when_jev_returns_no_signal()
    {
        var (jev, _) = Create("""{ "model": "jev-1", "answers": { "needs_hydration": { "noul": "alto" }, "unrelated": { "noul": 0.9 } } }""");

        var result = await jev.ProfileAsync(Assessment);

        result.Priorities.ShouldBe(TestData.Rules.Profile(Assessment).Priorities);
        result.Source.ShouldBe(ProfileSource.Jev);
    }

    [Fact]
    public async Task Keeps_the_typed_profile_from_the_rules()
    {
        var (jev, _) = Create(JevTestData.Response("jev-1", ("needs_volume", 0.9), ("needs_shine", 0.8)));

        var result = await jev.ProfileAsync(Assessment);
        var rules = TestData.Rules.Profile(Assessment).Profile;

        result.Profile.HairType.ShouldBe(rules.HairType);
        result.Profile.Conditions.ShouldBe(rules.Conditions);
        result.Profile.Chemical.ShouldBe(rules.Chemical);
        result.Profile.TreatmentNeeds.ShouldBe(rules.TreatmentNeeds);
        result.Profile.Allergies.ShouldBe([Allergen.Sulfate]);
    }

    [Fact]
    public async Task Reports_jev_as_the_source_with_its_model_and_average_confidence()
    {
        var (jev, _) = Create("""
            { "model": "jev-1.13.0", "answers": {
                "needs_hydration": { "noul": 0.9, "confidence": 0.8 },
                "needs_shine": { "noul": 0.7, "confidence": 0.65 } } }
            """);

        var result = await jev.ProfileAsync(Assessment);

        result.Source.ShouldBe(ProfileSource.Jev);
        result.Model.ShouldBe("jev-1.13.0");
        result.Confidence.ShouldBe(0.73); // average of 0.8 and 0.65, rounded
    }

    [Fact]
    public async Task Falls_back_to_default_model_name_and_confidence_when_jev_omits_them()
    {
        var (jev, _) = Create(JevTestData.Response(null, ("needs_hydration", 0.9), ("needs_shine", 0.8)));

        var result = await jev.ProfileAsync(Assessment);

        result.Model.ShouldBe("jev");
        result.Confidence.ShouldBe(0.7);
    }

    [Fact]
    public async Task Throws_on_an_http_error_or_an_empty_answer_so_the_hybrid_can_fall_back()
    {
        var (failing, _) = Create("""{ "error": "unavailable" }""", HttpStatusCode.ServiceUnavailable);
        var (empty, _) = Create("null");

        await Should.ThrowAsync<HttpRequestException>(() => failing.ProfileAsync(Assessment));
        await Should.ThrowAsync<InvalidOperationException>(() => empty.ProfileAsync(Assessment));
    }
}
