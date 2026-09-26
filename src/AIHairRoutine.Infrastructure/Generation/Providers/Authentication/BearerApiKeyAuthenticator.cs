using System.Net.Http.Headers;
using AIHairRoutine.Infrastructure.Config;

namespace AIHairRoutine.Infrastructure.Generation.Providers.Authentication;

/// <summary>
/// Bearer-token auth: <c>Authorization: Bearer &lt;key&gt;</c>. Shared by OpenAI and DeepSeek
/// (DeepSeek is OpenAI API-compatible), so the same strategy serves both providers.
/// </summary>
public sealed class BearerApiKeyAuthenticator : IApiKeyAuthenticator
{
    public void Apply(HttpRequestMessage request, ProviderOptions options) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
}
