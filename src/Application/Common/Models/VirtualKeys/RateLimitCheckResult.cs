namespace AiGateway.Application.Common.Models.VirtualKeys;

public record RateLimitCheckResult(
    bool IsAllowed,
    string? Reason,
    int CurrentRpm,
    int? MaxRpm,
    int CurrentTpm,
    int? MaxTpm
);
