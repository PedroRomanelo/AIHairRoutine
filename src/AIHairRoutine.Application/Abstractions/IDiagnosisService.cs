using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Abstractions;

/// <summary>Facade orchestrating: profile → match products → generate routine → assemble result.</summary>
public interface IDiagnosisService
{
    Task<DiagnosisResult> DiagnoseAsync(HairAssessment assessment, CancellationToken ct = default);
}
