using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.Common.Models.VirtualKeys;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AiGateway.Infrastructure.Services;

public class RateLimitService : IRateLimitService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RateLimitService> _logger;

    public RateLimitService(
        IDistributedCache distributedCache,
        IMemoryCache memoryCache,
        ILogger<RateLimitService> logger)
    {
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<RateLimitCheckResult> CheckAndRecordAsync(
        int virtualKeyId,
        int? limitRpm,
        int? limitTpm,
        int estimatedTokens,
        CancellationToken cancellationToken)
    {
        var currentWindowMinute = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmm");
        var rpmCacheKey = $"rl:rpm:{virtualKeyId}:{currentWindowMinute}";
        var tpmCacheKey = $"rl:tpm:{virtualKeyId}:{currentWindowMinute}";

        var currentRpm = await GetCounterAsync(rpmCacheKey, cancellationToken);
        var currentTpm = await GetCounterAsync(tpmCacheKey, cancellationToken);

        if (limitRpm.HasValue && currentRpm >= limitRpm.Value)
        {
            return new RateLimitCheckResult(
                IsAllowed: false,
                Reason: $"Rate limit exceeded (RPM limit: {limitRpm.Value}).",
                CurrentRpm: currentRpm,
                MaxRpm: limitRpm,
                CurrentTpm: currentTpm,
                MaxTpm: limitTpm
            );
        }

        if (limitTpm.HasValue && currentTpm + estimatedTokens > limitTpm.Value)
        {
            return new RateLimitCheckResult(
                IsAllowed: false,
                Reason: $"Token rate limit exceeded (TPM limit: {limitTpm.Value}).",
                CurrentRpm: currentRpm,
                MaxRpm: limitRpm,
                CurrentTpm: currentTpm,
                MaxTpm: limitTpm
            );
        }

        var newRpm = currentRpm + 1;
        var newTpm = currentTpm + estimatedTokens;

        await SetCounterAsync(rpmCacheKey, newRpm, TimeSpan.FromMinutes(2), cancellationToken);
        await SetCounterAsync(tpmCacheKey, newTpm, TimeSpan.FromMinutes(2), cancellationToken);

        return new RateLimitCheckResult(
            IsAllowed: true,
            Reason: null,
            CurrentRpm: newRpm,
            MaxRpm: limitRpm,
            CurrentTpm: newTpm,
            MaxTpm: limitTpm
        );
    }

    private async Task<int> GetCounterAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var str = await _distributedCache.GetStringAsync(key, cancellationToken);
            if (str != null && int.TryParse(str, out var val))
            {
                return val;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read key {Key} from distributed cache. Falling back to memory cache.", key);
        }

        if (_memoryCache.TryGetValue(key, out int memoryVal))
        {
            return memoryVal;
        }

        return 0;
    }

    private async Task SetCounterAsync(string key, int value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        _memoryCache.Set(key, value, ttl);

        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };
            await _distributedCache.SetStringAsync(key, value.ToString(), options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write key {Key} to distributed cache. Falling back to local memory cache.", key);
        }
    }
}
