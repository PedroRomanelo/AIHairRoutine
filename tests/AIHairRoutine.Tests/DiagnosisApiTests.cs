using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AIHairRoutine.Tests;

public sealed class DiagnosisApiTests(DiagnosisApiTests.Factory factory) : IClassFixture<DiagnosisApiTests.Factory>
{
    [Fact]
    public async Task Returns_profile_priorities_and_routine_using_rules_and_template_fallbacks()
    {
        var client = factory.CreateClient();
        var payload = new
        {
            hairType = "wavy",
            chemicalTreatment = "progressive",
            colorTreated = false,
            concerns = new { dryness = 8, frizz = 7, breakage = 4, oiliness = 2, hairLoss = 1 },
            locale = "pt-BR",
        };

        var response = await client.PostAsJsonAsync("/api/v1/diagnoses", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("profile").GetProperty("condition").GetString().ShouldBe("dry");
        root.GetProperty("profile").GetProperty("frizzLevel").GetString().ShouldBe("high");

        var priorities = root.GetProperty("priorities").EnumerateArray().Select(e => e.GetString()).ToList();
        priorities.ShouldContain("hydration");

        root.GetProperty("meta").GetProperty("profileSource").GetString().ShouldBe("rules");
        root.GetProperty("meta").GetProperty("models").GetProperty("generator").GetString().ShouldBe("template");

        root.GetProperty("routine").GetProperty("steps").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Rejects_out_of_range_scores_with_a_validation_problem()
    {
        var client = factory.CreateClient();
        var payload = new { hairType = "wavy", concerns = new { dryness = 99 } };

        var response = await client.PostAsJsonAsync("/api/v1/diagnoses", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Boots the API with all external dependencies disabled (deterministic rules + template).</summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jev:ApiKey"] = string.Empty,
                    ["Generation:Providers:Anthropic:ApiKey"] = string.Empty,
                    ["Database:ConnectionString"] = string.Empty,
                    ["Database:RunMigrationsOnStartup"] = "false",
                });
            });
            return base.CreateHost(builder);
        }
    }
}
