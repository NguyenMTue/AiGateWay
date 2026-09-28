namespace AiGateway.Domain.Entities;

public class VirtualKey : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string KeyHash { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;

    public string KeyMask { get; set; } = string.Empty;

    public int? RateLimitRpm { get; set; }

    public int? RateLimitTpm { get; set; }

    public decimal? MaxBudgetUsd { get; set; }

    public decimal CurrentUsageUsd { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastUsedAt { get; set; }

    public ICollection<RequestLog> RequestLogs { get; set; } = new List<RequestLog>();
}
