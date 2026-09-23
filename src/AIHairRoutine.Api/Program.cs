using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AIHairRoutine.Api;
using Microsoft.AspNetCore.RateLimiting;
using AIHairRoutine.Api.Endpoints;
using AIHairRoutine.Api.Health;
using AIHairRoutine.Application;
using AIHairRoutine.Infrastructure;
using AIHairRoutine.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// JSON: enums as snake_case strings (wavy, frizz_control, moderate), camelCase properties.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});

// Application + Infrastructure (profiling, generation, data, caching, resilience).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Cross-cutting: OpenAPI, ProblemDetails, health, rate limiting.
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddFixedWindowLimiter("diagnoses", fw =>
    {
        fw.PermitLimit = 60;
        fw.Window = TimeSpan.FromMinutes(1);
        fw.QueueLimit = 0;
    });
});

var app = builder.Build();

// Apply DB migrations at startup (best-effort; degrades to empty catalog if DB is down).
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateIfEnabled();
}

app.UseExceptionHandler();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.MapDiagnosisEndpoints();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
