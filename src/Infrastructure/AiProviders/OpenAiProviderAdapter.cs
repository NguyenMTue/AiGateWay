using System.Net.Http.Headers;
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
        var targetModelRequest = new ChatCompletionRequest
        {
            Model = model.ModelId,
            Messages = request.Messages,
            Temperature = request.Temperature,
            TopP = request.TopP,
            N = request.N,
            Stream = request.Stream,
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
}
