using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.Common.Models.AiProxy;
using AiGateway.Domain.Entities;
using AiGateway.Domain.Enums;

namespace AiGateway.Infrastructure.AiProviders;

public class AnthropicProviderAdapter : IAiProviderAdapter
{
    private readonly HttpClient _httpClient;

    public AnthropicProviderAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public ProviderType ProviderType => ProviderType.AnthropicClaude;

    public async Task<ChatCompletionResponse> ExecuteChatCompletionAsync(
        AiProvider provider,
        AiModel model,
        string apiKey,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var anthropicReq = MapToAnthropicRequest(model.ModelId, request);

        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var endpointUrl = $"{baseUrl}/v1/messages";

        var jsonBody = JsonSerializer.Serialize(anthropicReq);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Anthropic provider error ({(int)response.StatusCode}): {responseContent}",
                null,
                response.StatusCode);
        }

        var anthropicRes = JsonSerializer.Deserialize<AnthropicResponse>(responseContent);
        return MapToOpenAiResponse(anthropicRes, request.Model);
    }

    private static AnthropicRequest MapToAnthropicRequest(string modelId, ChatCompletionRequest request)
    {
        var messages = new List<AnthropicMessage>();
        string? systemPrompt = null;

        foreach (var msg in request.Messages)
        {
            if (msg.Role.ToLower() == "system")
            {
                systemPrompt = msg.Content?.ToString();
            }
            else
            {
                messages.Add(new AnthropicMessage
                {
                    Role = msg.Role.ToLower() == "assistant" ? "assistant" : "user",
                    Content = msg.Content?.ToString() ?? string.Empty
                });
            }
        }

        return new AnthropicRequest
        {
            Model = modelId,
            Messages = messages,
            System = systemPrompt,
            MaxTokens = request.MaxTokens ?? 4096,
            Temperature = request.Temperature
        };
    }

    private static ChatCompletionResponse MapToOpenAiResponse(AnthropicResponse? res, string requestedModel)
    {
        var choiceText = res?.Content?.FirstOrDefault()?.Text ?? string.Empty;

        var promptTokens = res?.Usage?.InputTokens ?? 0;
        var completionTokens = res?.Usage?.OutputTokens ?? 0;

        return new ChatCompletionResponse
        {
            Id = res?.Id ?? $"chatcmpl-anthropic-{Guid.NewGuid():N}",
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
                    FinishReason = res?.StopReason ?? "stop"
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

    // Anthropic DTOs
    private class AnthropicRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<AnthropicMessage> Messages { get; set; } = new();

        [JsonPropertyName("system")]
        public string? System { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 4096;

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }
    }

    private class AnthropicMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class AnthropicResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("content")]
        public List<AnthropicContentBlock>? Content { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }

        [JsonPropertyName("usage")]
        public AnthropicUsage? Usage { get; set; }
    }

    private class AnthropicContentBlock
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }
}
