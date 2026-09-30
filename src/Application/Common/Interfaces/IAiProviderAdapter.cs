using AiGateway.Application.Common.Models.AiProxy;
using AiGateway.Domain.Entities;
using AiGateway.Domain.Enums;

namespace AiGateway.Application.Common.Interfaces;

public interface IAiProviderAdapter
{
    ProviderType ProviderType { get; }

    Task<ChatCompletionResponse> ExecuteChatCompletionAsync(
        AiProvider provider,
        AiModel model,
        string apiKey,
        ChatCompletionRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<string> ExecuteChatCompletionStreamAsync(
        AiProvider provider,
        AiModel model,
        string apiKey,
        ChatCompletionRequest request,
        CancellationToken cancellationToken);
}
