using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.Common.Models.AiProxy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AiGateway.Infrastructure.Services;

public partial class SemanticCacheService : ISemanticCacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SemanticCacheService> _logger;

    public SemanticCacheService(
        IDistributedCache distributedCache,
        IMemoryCache memoryCache,
        ILogger<SemanticCacheService> logger)
    {
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public string GenerateSemanticKey(string modelAlias, IEnumerable<ChatMessageDto> messages)
    {
        var sb = new StringBuilder();
        sb.Append(modelAlias.Trim().ToLowerInvariant()).Append(':');

        foreach (var msg in messages)
        {
            var contentStr = msg.Content?.ToString() ?? string.Empty;
            var normalizedContent = NormalizeSemanticText(contentStr);
            sb.Append(msg.Role.Trim().ToLowerInvariant()).Append('=').Append(normalizedContent).Append(';');
        }

        var rawString = sb.ToString();
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawString));
        var hexHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return $"semcache:{modelAlias.ToLowerInvariant()}:{hexHash}";
    }

    public async Task<ChatCompletionResponse?> GetCachedResponseAsync(string semanticKey, CancellationToken cancellationToken)
    {
        try
        {
            var json = await _distributedCache.GetStringAsync(semanticKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var response = JsonSerializer.Deserialize<ChatCompletionResponse>(json);
                if (response != null)
                {
                    _logger.LogInformation("Semantic Cache HIT (Distributed) for key {Key}", semanticKey);
                    return response;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read semantic cache key {Key} from distributed cache. Checking memory cache.", semanticKey);
        }

        if (_memoryCache.TryGetValue(semanticKey, out ChatCompletionResponse? memoryResponse) && memoryResponse != null)
        {
            _logger.LogInformation("Semantic Cache HIT (Memory) for key {Key}", semanticKey);
            return memoryResponse;
        }

        return null;
    }

    public async Task SetCachedResponseAsync(
        string semanticKey,
        ChatCompletionResponse response,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        // Store clone in local memory cache
        _memoryCache.Set(semanticKey, response, ttl);

        try
        {
            var json = JsonSerializer.Serialize(response);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };
            await _distributedCache.SetStringAsync(semanticKey, json, options, cancellationToken);
            _logger.LogInformation("Semantic Cache SET for key {Key} with TTL {TTL}", semanticKey, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write semantic cache key {Key} to distributed cache.", semanticKey);
        }
    }

    private static string NormalizeSemanticText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // 1. Lowercase
        var normalized = text.ToLowerInvariant();

        // 2. Remove common punctuation marks
        normalized = PunctuationRegex().Replace(normalized, " ");

        // 3. Collapse multiple whitespace into single space
        normalized = ExtraWhitespaceRegex().Replace(normalized, " ").Trim();

        return normalized;
    }

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex ExtraWhitespaceRegex();
}
