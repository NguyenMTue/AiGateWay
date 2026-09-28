using AiGateway.Application.Common.Models.VirtualKeys;

namespace AiGateway.Application.Common.Interfaces;

public interface IRateLimitService
{
    Task<RateLimitCheckResult> CheckAndRecordAsync(
        int virtualKeyId,
        int? limitRpm,
        int? limitTpm,
        int estimatedTokens,
        CancellationToken cancellationToken);
}
