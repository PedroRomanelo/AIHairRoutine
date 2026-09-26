using AIHairRoutine.Infrastructure.Config;

namespace AIHairRoutine.Infrastructure.Generation.Providers;

/// <summary>
/// Adapter that exposes a single foreign LLM chat API behind a uniform contract: take a
/// system+user prompt, return the model's raw text. Each provider's endpoint, request/response
/// shape and auth are hidden behind this interface, so the routine generator stays provider-agnostic.
/// </summary>
public interface IChatModelClient
{
    AiProvider Provider { get; }

    Task<string> CompleteAsync(ChatPrompt prompt, CancellationToken ct = default);
}
