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

    /// <summary>
    /// Comma-separated list of allowed model aliases/IDs (e.g. "gpt-4o-mini,gemini-1.5-flash").
    /// If null or empty, access is unrestricted (all models allowed).
    /// </summary>
    public string? AllowedModelAliases { get; set; }

    public ICollection<RequestLog> RequestLogs { get; set; } = new List<RequestLog>();

    public bool IsModelAllowed(string requestedModelAlias)
    {
        if (string.IsNullOrWhiteSpace(AllowedModelAliases))
        {
            return true; // Unrestricted access
        }

        var allowedList = AllowedModelAliases
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return allowedList.Any(alias => alias.Equals(requestedModelAlias, StringComparison.OrdinalIgnoreCase));
    }
}
