using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.Common.Models.VirtualKeys;
using Microsoft.Extensions.Caching.Memory;

namespace AiGateway.Infrastructure.Services;

public class RateLimitService : IRateLimitService
{
    private readonly IMemoryCache _cache;

    public RateLimitService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<RateLimitCheckResult> CheckAndRecordAsync(
        int virtualKeyId,
        int? limitRpm,
        int? limitTpm,
        int estimatedTokens,
        CancellationToken cancellationToken)
    {
        var currentWindowMinute = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmm");
        var rpmCacheKey = $"rl:rpm:{virtualKeyId}:{currentWindowMinute}";
        var tpmCacheKey = $"rl:tpm:{virtualKeyId}:{currentWindowMinute}";

        var currentRpm = _cache.GetOrCreate(rpmCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return 0;
        });

        var currentTpm = _cache.GetOrCreate(tpmCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return 0;
        });

        if (limitRpm.HasValue && currentRpm >= limitRpm.Value)
        {
            return Task.FromResult(new RateLimitCheckResult(
                IsAllowed: false,
                Reason: $"Rate limit exceeded (RPM limit: {limitRpm.Value}).",
                CurrentRpm: currentRpm,
                MaxRpm: limitRpm,
                CurrentTpm: currentTpm,
                MaxTpm: limitTpm
            ));
        }

        if (limitTpm.HasValue && currentTpm + estimatedTokens > limitTpm.Value)
        {
            return Task.FromResult(new RateLimitCheckResult(
                IsAllowed: false,
                Reason: $"Token rate limit exceeded (TPM limit: {limitTpm.Value}).",
                CurrentRpm: currentRpm,
                MaxRpm: limitRpm,
                CurrentTpm: currentTpm,
                MaxTpm: limitTpm
            ));
        }

        // Record request and tokens into sliding window cache
        _cache.Set(rpmCacheKey, currentRpm + 1, TimeSpan.FromMinutes(2));
        _cache.Set(tpmCacheKey, currentTpm + estimatedTokens, TimeSpan.FromMinutes(2));

        return Task.FromResult(new RateLimitCheckResult(
            IsAllowed: true,
            Reason: null,
            CurrentRpm: currentRpm + 1,
            MaxRpm: limitRpm,
            CurrentTpm: currentTpm + estimatedTokens,
            MaxTpm: limitTpm
        ));
    }
}
