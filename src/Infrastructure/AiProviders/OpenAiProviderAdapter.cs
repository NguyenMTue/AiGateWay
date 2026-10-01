using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.Common.Models.AiProxy;
using AiGateway.Domain.Entities;
using AiGateway.Domain.Enums;

namespace AiGateway.Infrastructure.AiProviders;

public class OpenAiProviderAdapter : IAiProviderAdapter
{
    private readonly HttpClient _httpClient;

    public OpenAiProviderAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public ProviderType ProviderType => ProviderType.OpenAI;

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
                Id = $"chatcmpl-mock-{Guid.NewGuid():N}",
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

        var targetModelRequest = new ChatCompletionRequest
        {
            Model = model.ModelId,
            Messages = request.Messages,
            Temperature = request.Temperature,
            TopP = request.TopP,
            N = request.N,
            Stream = false,
            MaxTokens = request.MaxTokens,
            PresencePenalty = request.PresencePenalty,
            FrequencyPenalty = request.FrequencyPenalty,
            User = request.User
        };

        var endpointUrl = $"{provider.BaseUrl.TrimEnd('/')}/chat/completions";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var jsonBody = JsonSerializer.Serialize(targetModelRequest);
        httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenAI provider error ({(int)response.StatusCode}): {responseContent}",
                null,
                response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<ChatCompletionResponse>(responseContent)
                     ?? throw new InvalidOperationException("Failed to deserialize response from OpenAI.");

        result.Model = request.Model; // Preserve requested alias in response
        return result;
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
            yield return "data: {\"id\":\"chatcmpl-mock-stream\",\"object\":\"chat.completion.chunk\",\"created\":1700000000,\"model\":\"" + request.Model + "\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Guard patrolling castle courtyard. All clear.\"},\"finish_reason\":null}]}\n\n";
            yield return "data: {\"id\":\"chatcmpl-mock-stream\",\"object\":\"chat.completion.chunk\",\"created\":1700000000,\"model\":\"" + request.Model + "\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n";
            yield return "data: [DONE]\n\n";
            yield break;
        }
        var targetModelRequest = new ChatCompletionRequest
        {
            Model = model.ModelId,
            Messages = request.Messages,
            Temperature = request.Temperature,
            TopP = request.TopP,
            N = request.N,
            Stream = true,
            MaxTokens = request.MaxTokens,
            PresencePenalty = request.PresencePenalty,
            FrequencyPenalty = request.FrequencyPenalty,
            User = request.User
        };

        var endpointUrl = $"{provider.BaseUrl.TrimEnd('/')}/chat/completions";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var jsonBody = JsonSerializer.Serialize(targetModelRequest);
        httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"OpenAI provider error ({(int)response.StatusCode}): {responseContent}",
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
}
