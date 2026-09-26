namespace AIHairRoutine.Infrastructure.Generation.Providers;

/// <summary>Provider-agnostic prompt: a system instruction plus a single user message.</summary>
public sealed record ChatPrompt(string System, string User);
