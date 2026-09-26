using AIHairRoutine.Infrastructure.Config;

namespace AIHairRoutine.Infrastructure.Generation.Providers;

/// <summary>
/// Abstract Factory: each implementation creates the chat-client family for one provider,
/// pairing the provider's <see cref="IChatModelClient"/> adapter with the correct API-key
/// authentication strategy over a named HttpClient. The active provider is chosen by
/// configuration and resolved by <see cref="ChatModelClientFactory"/>.
/// </summary>
public interface IChatModelClientFactory
{
    AiProvider Provider { get; }

    IChatModelClient Create();
}
