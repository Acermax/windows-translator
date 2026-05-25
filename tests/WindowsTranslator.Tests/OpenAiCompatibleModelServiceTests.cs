using System.Net;
using System.Net.Http.Headers;
using System.Text;
using WindowsTranslator.Core.Settings;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.Tests;

public sealed class OpenAiCompatibleModelServiceTests
{
    [Fact]
    public async Task ListModelsAsync_LoadsModelsFromModelsEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "object": "list",
                      "data": [
                        { "id": "qwen3.6-coder" },
                        { "id": "qwen3.6-translator" }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var service = new OpenAiCompatibleModelService(new HttpClient(handler), new TranslationSettings
        {
            Endpoint = "http://localhost:8001/v1/chat/completions",
            ApiKey = "test-api-key"
        });

        var models = await service.ListModelsAsync(CancellationToken.None);

        Assert.Equal(["qwen3.6-coder", "qwen3.6-translator"], models);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal("http://localhost:8001/v1/models", capturedRequest.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "test-api-key"), capturedRequest.Headers.Authorization);
    }

    [Fact]
    public async Task ListModelsAsync_ThrowsTranslationException_OnEndpointError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"Unauthorized\"}", Encoding.UTF8, "application/json")
        });
        var service = new OpenAiCompatibleModelService(new HttpClient(handler), new TranslationSettings());

        var exception = await Assert.ThrowsAsync<TranslationException>(
            () => service.ListModelsAsync(CancellationToken.None));

        Assert.Contains("401", exception.Message);
        Assert.Contains("Unauthorized", exception.Message);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
