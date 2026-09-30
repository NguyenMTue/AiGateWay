using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.Common.Models.AiProxy;
using AiGateway.Domain.Entities;
using AiGateway.Domain.Enums;

namespace AiGateway.Infrastructure.AiProviders;

public class GeminiProviderAdapter : IAiProviderAdapter
{
    private readonly HttpClient _httpClient;

    public GeminiProviderAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public ProviderType ProviderType => ProviderType.GoogleGemini;

    public async Task<ChatCompletionResponse> ExecuteChatCompletionAsync(
        AiProvider provider,
        AiModel model,
        string apiKey,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var geminiReq = MapToGeminiRequest(request);

        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var endpointUrl = $"{baseUrl}/models/{model.ModelId}:generateContent?key={apiKey}";

        var jsonBody = JsonSerializer.Serialize(geminiReq);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gemini provider error ({(int)response.StatusCode}): {responseContent}",
                null,
                response.StatusCode);
        }

        var geminiRes = JsonSerializer.Deserialize<GeminiResponse>(responseContent);
        return MapToOpenAiResponse(geminiRes, request.Model);
    }

    public async IAsyncEnumerable<string> ExecuteChatCompletionStreamAsync(
        AiProvider provider,
        AiModel model,
        string apiKey,
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var geminiReq = MapToGeminiRequest(request);

        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var endpointUrl = $"{baseUrl}/models/{model.ModelId}:streamGenerateContent?alt=sse&key={apiKey}";

        var jsonBody = JsonSerializer.Serialize(geminiReq);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Gemini provider error ({(int)response.StatusCode}): {responseContent}",
                null,
                response.StatusCode);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }

    private static GeminiRequest MapToGeminiRequest(ChatCompletionRequest request)
    {
        var contents = new List<GeminiContent>();

        foreach (var msg in request.Messages)
        {
            var role = msg.Role.ToLower() == "assistant" ? "model" : "user";
            var text = msg.Content?.ToString() ?? string.Empty;

            contents.Add(new GeminiContent
            {
                Role = role,
                Parts = new List<GeminiPart> { new() { Text = text } }
            });
        }

        return new GeminiRequest
        {
            Contents = contents,
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = request.Temperature,
                TopP = request.TopP,
                MaxOutputTokens = request.MaxTokens
            }
        };
    }

    private static ChatCompletionResponse MapToOpenAiResponse(GeminiResponse? res, string requestedModel)
    {
        var choiceText = res?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
        var finishReason = res?.Candidates?.FirstOrDefault()?.FinishReason ?? "stop";

        var promptTokens = res?.UsageMetadata?.PromptTokenCount ?? 0;
        var completionTokens = res?.UsageMetadata?.CandidatesTokenCount ?? 0;

        return new ChatCompletionResponse
        {
            Id = $"chatcmpl-gemini-{Guid.NewGuid():N}",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = requestedModel,
            Choices = new List<ChatChoiceDto>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessageDto
                    {
                        Role = "assistant",
                        Content = choiceText
                    },
                    FinishReason = finishReason
                }
            },
            Usage = new UsageDto
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens
            }
        };
    }

    // Gemini DTOs
    private class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = new();

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new();
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("topP")]
        public double? TopP { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int? MaxOutputTokens { get; set; }
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }

        [JsonPropertyName("usageMetadata")]
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    private class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }

    private class GeminiUsageMetadata
    {
        [JsonPropertyName("promptTokenCount")]
        public int PromptTokenCount { get; set; }

        [JsonPropertyName("candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }
    }
}
