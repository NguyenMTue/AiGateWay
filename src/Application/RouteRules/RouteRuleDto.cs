using AiGateway.Domain.Enums;

namespace AiGateway.Application.RouteRules;

public class RouteRuleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TargetModelAlias { get; set; } = string.Empty;
    public RoutingStrategy RoutingStrategy { get; set; }
    public int PrimaryModelId { get; set; }
    public string? PrimaryModelName { get; set; }
    public int? FallbackModelId { get; set; }
    public string? FallbackModelName { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
}
