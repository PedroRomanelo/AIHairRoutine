using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AIHairRoutine.Api;

/// <summary>Converts unhandled exceptions into RFC 9457 ProblemDetails responses.</summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception processing {Path}", ctx.Request.Path);

        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = new ProblemDetails
            {
                Title = "Erro interno",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "Ocorreu um erro ao processar a requisição.",
            },
        });
    }
}
