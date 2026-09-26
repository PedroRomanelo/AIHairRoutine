using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using FluentValidation;

namespace AIHairRoutine.Api.Endpoints;

public static class DiagnosisEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Diagnosis");

        group.MapPost("/diagnoses", async (
                HairAssessment assessment,
                IValidator<HairAssessment> validator,
                IDiagnosisService service,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(assessment, ct);
                if (!validation.IsValid)
                    return Results.ValidationProblem(validation.ToDictionary());

                var result = await service.DiagnoseAsync(assessment, ct);
                return Results.Ok(result);
            })
            .WithName("CreateDiagnosis")
            .WithSummary("Gera perfil capilar, prioridades, produtos recomendados e o cronograma capilar de 4 semanas (H/N/R).")
            .Produces<DiagnosisResult>()
            .ProducesValidationProblem()
            .RequireRateLimiting("diagnoses");

        return app;
    }
}
