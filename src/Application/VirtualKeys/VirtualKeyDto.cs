namespace AiGateway.Application.VirtualKeys;

public class VirtualKeyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string KeyMask { get; set; } = string.Empty;
    public int? RateLimitRpm { get; set; }
    public int? RateLimitTpm { get; set; }
    public decimal? MaxBudgetUsd { get; set; }
    public decimal CurrentUsageUsd { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public string? AllowedModelAliases { get; set; }
}

public class CreatedVirtualKeyResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RawVirtualKey { get; set; } = string.Empty;
    public string KeyMask { get; set; } = string.Empty;
    public string? AllowedModelAliases { get; set; }
}
