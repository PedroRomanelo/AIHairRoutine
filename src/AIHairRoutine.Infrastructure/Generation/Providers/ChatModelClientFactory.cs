using AIHairRoutine.Infrastructure.Config;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Generation.Providers;

/// <summary>
/// Selects the <see cref="IChatModelClientFactory"/> matching the configured provider and asks it
/// to build the active chat-client adapter. This is the single entry point the generation pipeline
/// uses; adding a provider is a matter of registering one more factory.
/// </summary>
public sealed class ChatModelClientFactory(
    IEnumerable<IChatModelClientFactory> factories,
    IOptions<GenerationOptions> options)
{
    public IChatModelClient CreateActive()
    {
        var provider = options.Value.Provider;
        var factory = factories.FirstOrDefault(f => f.Provider == provider)
            ?? throw new InvalidOperationException(
                $"No AI provider factory is registered for '{provider}'.");

        return factory.Create();
    }
}
