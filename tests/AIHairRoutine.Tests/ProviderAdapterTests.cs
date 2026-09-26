using System.Net;
using System.Text.Json;
using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation.Providers;
using AIHairRoutine.Infrastructure.Generation.Providers.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AIHairRoutine.Tests;

/// <summary>
/// Each adapter is built through its provider factory, exactly as in production, so these tests also
/// pin the adapter + API-key strategy pairing. The fake handler checks route, headers and body, and
/// feeds back a sample response in the provider's own wire format.
/// </summary>
public sealed class ProviderAdapterTests
{
    private const string BaseUrl = "https://provider.test/";
    private const int MaxTokens = 700;
    private static readonly ChatPrompt Prompt = new("SYSTEM PROMPT", "USER PROMPT");

    private static (IChatModelClient Client, FakeHttpHandler Handler) Create(
        AiProvider provider,
        string responseJson,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new FakeHttpHandler(status, responseJson);
        var httpFactory = new FakeHttpClientFactory(handler, BaseUrl);
        var options = Options.Create(new GenerationOptions
        {
            Provider = provider,
            MaxTokens = MaxTokens,
            Providers =
            {
                [provider.ToString()] = new ProviderOptions
                {
                    BaseUrl = BaseUrl,
                    ApiKey = $"key-{provider}",
                    Model = $"model-{provider.ToString().ToLowerInvariant()}",
                },
            },
        });

        IChatModelClientFactory[] factories =
        [
            new AnthropicClientFactory(httpFactory, options, NullLoggerFactory.Instance),
            new OpenAiClientFactory(httpFactory, options, NullLoggerFactory.Instance),
            new GeminiClientFactory(httpFactory, options, NullLoggerFactory.Instance),
            new DeepSeekClientFactory(httpFactory, options, NullLoggerFactory.Instance),
        ];

        return (new ChatModelClientFactory(factories, options).CreateActive(), handler);
    }

    private static JsonElement Body(FakeHttpHandler handler) => JsonDocument.Parse(handler.Body!).RootElement;

    // --- Anthropic ---

    [Fact]
    public async Task Anthropic_posts_to_messages_with_its_auth_and_body()
    {
        var (client, handler) = Create(AiProvider.Anthropic, """{ "content": [ { "type": "text", "text": "ok" } ] }""");

        await client.CompleteAsync(Prompt);

        client.Provider.ShouldBe(AiProvider.Anthropic);
        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Uri.ShouldBe(new Uri("https://provider.test/v1/messages"));
        handler.Headers["x-api-key"].ShouldBe("key-Anthropic");
        handler.Headers["anthropic-version"].ShouldBe("2023-06-01");

        var body = Body(handler);
        body.GetProperty("model").GetString().ShouldBe("model-anthropic");
        body.GetProperty("max_tokens").GetInt32().ShouldBe(MaxTokens);
        body.GetProperty("system").GetString().ShouldBe("SYSTEM PROMPT");
        var message = body.GetProperty("messages").EnumerateArray().ShouldHaveSingleItem();
        message.GetProperty("role").GetString().ShouldBe("user");
        message.GetProperty("content").GetString().ShouldBe("USER PROMPT");
    }

    [Fact]
    public async Task Anthropic_concatenates_text_blocks_and_ignores_other_block_types()
    {
        var (client, _) = Create(AiProvider.Anthropic, """
            { "content": [
                { "type": "text", "text": "Olá, " },
                { "type": "tool_use", "id": "toolu_1", "name": "x", "input": {} },
                { "type": "text", "text": "mundo" } ],
              "stop_reason": "end_turn" }
            """);

        (await client.CompleteAsync(Prompt)).ShouldBe("Olá, mundo");
    }

    // --- OpenAI / DeepSeek (same Chat Completions wire format) ---

    [Theory]
    [InlineData(AiProvider.OpenAI, "key-OpenAI", "model-openai")]
    [InlineData(AiProvider.DeepSeek, "key-DeepSeek", "model-deepseek")]
    public async Task OpenAI_compatible_providers_post_to_chat_completions_with_a_bearer_token(
        AiProvider provider, string expectedKey, string expectedModel)
    {
        var (client, handler) = Create(provider, """{ "choices": [ { "message": { "role": "assistant", "content": "ok" } } ] }""");

        await client.CompleteAsync(Prompt);

        client.Provider.ShouldBe(provider);
        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Uri.ShouldBe(new Uri("https://provider.test/v1/chat/completions"));
        handler.Headers["Authorization"].ShouldBe($"Bearer {expectedKey}");
        handler.Headers.ShouldNotContainKey("x-api-key");

        var body = Body(handler);
        body.GetProperty("model").GetString().ShouldBe(expectedModel);
        body.GetProperty("max_tokens").GetInt32().ShouldBe(MaxTokens);
        var messages = body.GetProperty("messages").EnumerateArray().ToList();
        messages.Select(m => m.GetProperty("role").GetString()).ShouldBe(["system", "user"]);
        messages.Select(m => m.GetProperty("content").GetString()).ShouldBe(["SYSTEM PROMPT", "USER PROMPT"]);
    }

    [Theory]
    [InlineData(AiProvider.OpenAI)]
    [InlineData(AiProvider.DeepSeek)]
    public async Task OpenAI_compatible_providers_read_the_first_choice(AiProvider provider)
    {
        var (client, _) = Create(provider, """
            { "id": "chatcmpl-1", "choices": [
                { "index": 0, "message": { "role": "assistant", "content": "primeira" }, "finish_reason": "stop" },
                { "index": 1, "message": { "role": "assistant", "content": "segunda" }, "finish_reason": "stop" } ] }
            """);

        (await client.CompleteAsync(Prompt)).ShouldBe("primeira");
    }

    [Fact]
    public async Task OpenAI_returns_empty_text_when_there_are_no_choices()
    {
        var (client, _) = Create(AiProvider.OpenAI, """{ "choices": [] }""");

        (await client.CompleteAsync(Prompt)).ShouldBeEmpty();
    }

    // --- Gemini ---

    [Fact]
    public async Task Gemini_posts_to_generate_content_for_the_model_with_its_auth_and_body()
    {
        var (client, handler) = Create(AiProvider.Gemini, """{ "candidates": [ { "content": { "parts": [ { "text": "ok" } ] } } ] }""");

        await client.CompleteAsync(Prompt);

        client.Provider.ShouldBe(AiProvider.Gemini);
        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Uri.ShouldBe(new Uri("https://provider.test/v1beta/models/model-gemini:generateContent"));
        handler.Headers["x-goog-api-key"].ShouldBe("key-Gemini");
        handler.Uri!.Query.ShouldNotContain("key");

        var body = Body(handler);
        body.GetProperty("system_instruction").GetProperty("parts")[0].GetProperty("text").GetString().ShouldBe("SYSTEM PROMPT");
        var content = body.GetProperty("contents").EnumerateArray().ShouldHaveSingleItem();
        content.GetProperty("role").GetString().ShouldBe("user");
        content.GetProperty("parts")[0].GetProperty("text").GetString().ShouldBe("USER PROMPT");
        body.GetProperty("generationConfig").GetProperty("maxOutputTokens").GetInt32().ShouldBe(MaxTokens);
    }

    [Fact]
    public async Task Gemini_concatenates_the_parts_of_the_first_candidate()
    {
        var (client, _) = Create(AiProvider.Gemini, """
            { "candidates": [
                { "content": { "role": "model", "parts": [ { "text": "parte 1, " }, { "text": "parte 2" } ] }, "finishReason": "STOP" },
                { "content": { "role": "model", "parts": [ { "text": "ignorada" } ] } } ] }
            """);

        (await client.CompleteAsync(Prompt)).ShouldBe("parte 1, parte 2");
    }

    [Fact]
    public async Task Gemini_returns_empty_text_when_there_are_no_candidates()
    {
        var (client, _) = Create(AiProvider.Gemini, """{ "candidates": [] }""");

        (await client.CompleteAsync(Prompt)).ShouldBeEmpty();
    }

    // --- All providers ---

    [Theory]
    [InlineData(AiProvider.Anthropic, HttpStatusCode.Unauthorized)]
    [InlineData(AiProvider.OpenAI, HttpStatusCode.TooManyRequests)]
    [InlineData(AiProvider.Gemini, HttpStatusCode.BadRequest)]
    [InlineData(AiProvider.DeepSeek, HttpStatusCode.InternalServerError)]
    public async Task Error_responses_throw_so_the_template_fallback_takes_over(AiProvider provider, HttpStatusCode status)
    {
        var (client, _) = Create(provider, """{ "error": { "message": "nope" } }""", status);

        var ex = await Should.ThrowAsync<HttpRequestException>(() => client.CompleteAsync(Prompt));
        ex.StatusCode.ShouldBe(status);
    }

    [Theory]
    [InlineData(AiProvider.Anthropic)]
    [InlineData(AiProvider.OpenAI)]
    [InlineData(AiProvider.Gemini)]
    [InlineData(AiProvider.DeepSeek)]
    public async Task A_null_response_body_throws(AiProvider provider)
    {
        var (client, _) = Create(provider, "null");

        await Should.ThrowAsync<InvalidOperationException>(() => client.CompleteAsync(Prompt));
    }

    [Fact]
    public void Selecting_a_provider_without_a_registered_factory_fails_clearly()
    {
        var options = Options.Create(new GenerationOptions { Provider = AiProvider.Gemini });
        var httpFactory = new FakeHttpClientFactory(new FakeHttpHandler(HttpStatusCode.OK, "{}"), BaseUrl);
        var selector = new ChatModelClientFactory(
            [new AnthropicClientFactory(httpFactory, options, NullLoggerFactory.Instance)], options);

        Should.Throw<InvalidOperationException>(() => selector.CreateActive()).Message.ShouldContain("Gemini");
    }
}
