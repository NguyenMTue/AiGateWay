using AiGateway.Domain.Enums;

namespace AiGateway.Application.Providers;

public class ProviderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int TimeoutSeconds { get; set; }
    public int MaxRetries { get; set; }
}
