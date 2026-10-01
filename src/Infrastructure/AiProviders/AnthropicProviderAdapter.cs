using System.Runtime.CompilerServices;
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
        if (apiKey.Contains("dummy", StringComparison.OrdinalIgnoreCase) || apiKey.StartsWith("sk-mock", StringComparison.OrdinalIgnoreCase) || apiKey.StartsWith("dev-mock", StringComparison.OrdinalIgnoreCase))
        {
            var promptTokens = request.Messages?.Sum(m => m.Content?.ToString()?.Length ?? 0) / 4 ?? 120;
            promptTokens = Math.Max(50, promptTokens);
            var completionTokens = 45;

            return new ChatCompletionResponse
            {
                Id = $"chatcmpl-anthropic-mock-{Guid.NewGuid():N}",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<ChatChoiceDto>
                {
                    new()
                    {
                        Index = 0,
                        Message = new ChatMessageDto
                        {
                            Role = "assistant",
                            Content = "{\"action\": \"patrol\", \"dialogue\": \"Guard patrolling castle courtyard. All clear.\"}"
                        },
                        FinishReason = "stop"
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

    public async IAsyncEnumerable<string> ExecuteChatCompletionStreamAsync(
        AiProvider provider,
        AiModel model,
        string apiKey,
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (apiKey.Contains("dummy", StringComparison.OrdinalIgnoreCase) || apiKey.StartsWith("sk-mock", StringComparison.OrdinalIgnoreCase) || apiKey.StartsWith("dev-mock", StringComparison.OrdinalIgnoreCase))
        {
            yield return "data: {\"id\":\"chatcmpl-anthropic-mock-stream\",\"object\":\"chat.completion.chunk\",\"created\":1700000000,\"model\":\"" + request.Model + "\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Guard patrolling castle courtyard. All clear.\"},\"finish_reason\":null}]}\n\n";
            yield return "data: {\"id\":\"chatcmpl-anthropic-mock-stream\",\"object\":\"chat.completion.chunk\",\"created\":1700000000,\"model\":\"" + request.Model + "\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n";
            yield return "data: [DONE]\n\n";
            yield break;
        }
        var anthropicReq = MapToAnthropicRequest(model.ModelId, request);
        anthropicReq.Stream = true;

        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var endpointUrl = $"{baseUrl}/v1/messages";

        var jsonBody = JsonSerializer.Serialize(anthropicReq);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Anthropic provider error ({(int)response.StatusCode}): {responseContent}",
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

        [JsonPropertyName("stream")]
        public bool? Stream { get; set; }
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
