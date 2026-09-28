using AiGateway.Domain.Enums;

namespace AiGateway.Domain.Entities;

public class AiModel : BaseAuditableEntity
{
    public int AiProviderId { get; set; }

    public AiProvider Provider { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    public ModelType ModelType { get; set; } = ModelType.ChatCompletion;

    public decimal PromptTokenCostPer1K { get; set; }

    public decimal CompletionTokenCostPer1K { get; set; }

    public int? ContextWindowTokens { get; set; }

    public int? MaxOutputTokens { get; set; }

    public bool SupportsStreaming { get; set; } = true;

    public bool SupportsVision { get; set; }

    public bool SupportsFunctionCalling { get; set; } = true;

    public bool IsActive { get; set; } = true;
}
