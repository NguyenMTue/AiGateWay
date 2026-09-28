using AiGateway.Domain.Enums;

namespace AiGateway.Domain.Entities;

public class AiProvider : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public ProviderType ProviderType { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 60;

    public int MaxRetries { get; set; } = 3;

    public ICollection<AiModel> Models { get; set; } = new List<AiModel>();

    public ICollection<ProviderApiKey> ApiKeys { get; set; } = new List<ProviderApiKey>();
}
