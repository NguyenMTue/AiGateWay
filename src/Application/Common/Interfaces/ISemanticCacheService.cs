using AiGateway.Application.Common.Models.AiProxy;

namespace AiGateway.Application.Common.Interfaces;

public interface ISemanticCacheService
{
    /// <summary>
    /// Generates a normalized semantic cache key from model alias and chat messages.
    /// </summary>
    string GenerateSemanticKey(string modelAlias, IEnumerable<ChatMessageDto> messages);

    /// <summary>
    /// Attempts to retrieve a cached ChatCompletionResponse for the given semantic key.
    /// </summary>
    Task<ChatCompletionResponse?> GetCachedResponseAsync(string semanticKey, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a ChatCompletionResponse in the semantic cache with a specified TTL.
    /// </summary>
    Task SetCachedResponseAsync(string semanticKey, ChatCompletionResponse response, TimeSpan ttl, CancellationToken cancellationToken);
}
