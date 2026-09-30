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
