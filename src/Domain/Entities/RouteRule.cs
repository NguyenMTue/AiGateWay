using AiGateway.Domain.Enums;

namespace AiGateway.Domain.Entities;

public class RouteRule : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string TargetModelAlias { get; set; } = string.Empty;

    public RoutingStrategy RoutingStrategy { get; set; } = RoutingStrategy.Priority;

    public int PrimaryModelId { get; set; }

    public AiModel PrimaryModel { get; set; } = null!;

    public int? FallbackModelId { get; set; }

    public AiModel? FallbackModel { get; set; }

    public bool IsActive { get; set; } = true;

    public int Priority { get; set; } = 1;
}
