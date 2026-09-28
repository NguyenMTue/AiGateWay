namespace AiGateway.Domain.Entities;

public class ProviderApiKey : BaseAuditableEntity
{
    public int AiProviderId { get; set; }

    public AiProvider Provider { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string EncryptedApiKey { get; set; } = string.Empty;

    public string KeyMask { get; set; } = string.Empty;

    public int Weight { get; set; } = 1;

    public int Priority { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public bool IsExhausted { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public DateTimeOffset? CooldownUntil { get; set; }
}
