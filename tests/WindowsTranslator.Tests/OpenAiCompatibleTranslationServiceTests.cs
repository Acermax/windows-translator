using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using WindowsTranslator.Core.Settings;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.Tests;

public sealed class OpenAiCompatibleTranslationServiceTests
{
    [Fact]
    public async Task TranslateAsync_PostsOpenAiCompatibleChatCompletionRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "model": "served-model",
                      "choices": [
                        { "message": { "role": "assistant", "content": "Hola mundo" } }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var settings = new TranslationSettings
        {
            Endpoint = "http://localhost:8000/v1/chat/completions",
            Model = "served-model",
            ApiKey = "test-api-key",
            TargetLanguage = "Spanish",
            Temperature = 0.6,
            TopP = 0.95,
            TopK = 20,
            MinP = 0.0,
            PresencePenalty = 0.0,
            RepetitionPenalty = 1.0,
            IncludeReasoning = false,
            EnableThinking = false
        };
        var service = new OpenAiCompatibleTranslationService(new HttpClient(handler), settings);

        var result = await service.TranslateAsync(new TranslationRequest("Hello world"), CancellationToken.None);

        Assert.Equal("Hola mundo", result.Text);
        Assert.Equal("served-model", result.Model);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("http://localhost:8000/v1/chat/completions", capturedRequest.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "test-api-key"), capturedRequest.Headers.Authorization);

        var json = JsonNode.Parse(capturedBody!)!;
        Assert.Equal("served-model", json["model"]!.GetValue<string>());
        Assert.Equal("system", json["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Equal("user", json["messages"]![1]!["role"]!.GetValue<string>());
        Assert.Contains("Hello world", json["messages"]![1]!["content"]!.GetValue<string>());
        Assert.Equal(0.6, json["temperature"]!.GetValue<double>());
        Assert.Equal(0.95, json["top_p"]!.GetValue<double>());
        Assert.Equal(20, json["top_k"]!.GetValue<int>());
        Assert.Equal(0.0, json["min_p"]!.GetValue<double>());
        Assert.Equal(0.0, json["presence_penalty"]!.GetValue<double>());
        Assert.Equal(1.0, json["repetition_penalty"]!.GetValue<double>());
        Assert.False(json["include_reasoning"]!.GetValue<bool>());
        Assert.False(json["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>());
    }

    [Fact]
    public async Task TranslateAsync_DoesNotSendAuthorizationHeader_WhenApiKeyIsBlank()
    {
        HttpRequestHeaders? capturedHeaders = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedHeaders = request.Headers;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Hola\"}}]}", Encoding.UTF8, "application/json")
            });
        });

        var settings = new TranslationSettings { ApiKey = " " };
        var service = new OpenAiCompatibleTranslationService(new HttpClient(handler), settings);

        await service.TranslateAsync(new TranslationRequest("Hello"), CancellationToken.None);

        Assert.Null(capturedHeaders!.Authorization);
    }

    [Fact]
    public async Task TranslateAsync_IgnoresReasoning_WhenContentExists()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "Hola mundo",
                        "reasoning": "Internal reasoning to discard"
                      },
                      "finish_reason": "stop"
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json")
        }));
        var service = new OpenAiCompatibleTranslationService(new HttpClient(handler), new TranslationSettings
        {
            IncludeReasoning = true,
            EnableThinking = true
        });

        var result = await service.TranslateAsync(new TranslationRequest("Hello world"), CancellationToken.None);

        Assert.Equal("Hola mundo", result.Text);
    }

    [Fact]
    public async Task TranslateAsync_ThrowsHelpfulError_WhenReasoningExhaustsOutputTokens()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "choices": [
                    {
                      "message": {
                        "content": null,
                        "reasoning": "The model is still thinking"
                      },
                      "finish_reason": "length"
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json")
        }));
        var service = new OpenAiCompatibleTranslationService(new HttpClient(handler), new TranslationSettings
        {
            IncludeReasoning = true,
            EnableThinking = true
        });

        var exception = await Assert.ThrowsAsync<TranslationException>(
            () => service.TranslateAsync(new TranslationRequest("Hello world"), CancellationToken.None));

        Assert.Contains("reasoning but no translated text", exception.Message);
        Assert.Contains("max output tokens", exception.Message);
    }

    [Fact]
    public async Task TranslateAsync_ThrowsTranslationException_OnEndpointError()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("bad request", Encoding.UTF8, "text/plain")
        }));
        var service = new OpenAiCompatibleTranslationService(new HttpClient(handler), new TranslationSettings());

        var exception = await Assert.ThrowsAsync<TranslationException>(
            () => service.TranslateAsync(new TranslationRequest("Hello"), CancellationToken.None));

        Assert.Contains("400", exception.Message);
        Assert.Contains("bad request", exception.Message);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
