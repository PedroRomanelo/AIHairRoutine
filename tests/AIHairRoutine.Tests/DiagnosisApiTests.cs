using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AIHairRoutine.Tests;

public sealed class DiagnosisApiTests(DiagnosisApiTests.Factory factory) : IClassFixture<DiagnosisApiTests.Factory>
{
    private static object ValidPayload(object? chemical = null, string[]? allergies = null) => new
    {
        hairType = "wavy",
        thickness = "fine",
        tone = "brown",
        conditions = new[] { "dry", "frizzy" },
        chemical = chemical ?? new { hasChemical = true, type = "coloring", performed = "há 2 meses", touchUpFrequency = "quarterly" },
        mainGoal = "reduzir o frizz e ganhar brilho",
        allergies = allergies ?? ["fragrance"],
        locale = "pt-BR",
    };

    [Fact]
    public async Task Returns_a_four_week_schedule_built_by_rules_and_written_by_the_template()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/diagnoses", ValidPayload());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var profile = root.GetProperty("profile");
        profile.GetProperty("conditions").EnumerateArray().Select(e => e.GetString()).ShouldBe(["dry", "frizzy"]);
        profile.GetProperty("chemical").GetProperty("isRecent").GetBoolean().ShouldBeTrue();
        root.GetProperty("priorities")[0].GetString().ShouldBe("frizz_control");

        var schedule = root.GetProperty("schedule");
        schedule.GetProperty("weeks").GetArrayLength().ShouldBe(4);
        schedule.GetProperty("washDays").EnumerateArray().Select(e => e.GetString()).ShouldBe(["monday", "thursday"]);
        schedule.GetProperty("overview").GetArrayLength().ShouldBeGreaterThan(0);
        schedule.GetProperty("summary").GetString().ShouldStartWith("Cronograma de 4 semanas");

        var steps = schedule.GetProperty("weeks").EnumerateArray()
            .SelectMany(w => w.GetProperty("days").EnumerateArray())
            .SelectMany(d => d.GetProperty("steps").EnumerateArray())
            .ToList();
        steps.ShouldAllBe(s => !string.IsNullOrEmpty(s.GetProperty("how").GetString()));

        // Fragrance was ticked: the scented shampoo is reported as excluded, never scheduled.
        var excluded = root.GetProperty("excludedProducts").EnumerateArray()
            .Single(e => e.GetProperty("id").GetGuid() == TestCatalog.ShampooWithFragrance.Id);
        excluded.GetProperty("reason").GetString().ShouldBe("allergy");
        var scheduledIds = steps
            .Select(s => s.GetProperty("productId"))
            .Where(id => id.ValueKind == JsonValueKind.String)
            .Select(id => id.GetGuid())
            .ToList();
        scheduledIds.ShouldNotContain(TestCatalog.ShampooWithFragrance.Id);

        root.GetProperty("recommendedProducts").GetArrayLength().ShouldBeGreaterThan(0);
        root.GetProperty("meta").GetProperty("profileSource").GetString().ShouldBe("rules");
        root.GetProperty("meta").GetProperty("models").GetProperty("generator").GetString().ShouldBe("template");
    }

    [Fact]
    public async Task Rejects_chemistry_without_a_date_with_a_validation_problem()
    {
        var payload = ValidPayload(chemical: new { hasChemical = true, type = "bleaching", touchUpFrequency = "monthly" });

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/diagnoses", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Chemical.Performed");
    }

    [Fact]
    public async Task Rejects_an_allergy_outside_the_closed_list()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/diagnoses", ValidPayload(allergies: ["pollen"]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Health_endpoint_is_up()
    {
        var response = await factory.CreateClient().GetAsync("/health");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Boots the API with external dependencies disabled and an in-memory catalog.</summary>
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

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IProductCatalog>();
                services.AddSingleton<IProductCatalog>(new InMemoryCatalog(TestCatalog.All));
            });
    }

    private sealed class InMemoryCatalog(IReadOnlyList<Product> products) : IProductCatalog
    {
        public Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken ct = default) => Task.FromResult(products);
    }
}
