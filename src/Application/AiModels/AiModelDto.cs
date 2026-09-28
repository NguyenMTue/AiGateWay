using AiGateway.Domain.Enums;

namespace AiGateway.Application.AiModels;

public class AiModelDto
{
    public int Id { get; set; }
    public int AiProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public ModelType ModelType { get; set; }
    public decimal PromptTokenCostPer1K { get; set; }
    public decimal CompletionTokenCostPer1K { get; set; }
    public int? ContextWindowTokens { get; set; }
    public int? MaxOutputTokens { get; set; }
    public bool SupportsStreaming { get; set; }
    public bool SupportsVision { get; set; }
    public bool SupportsFunctionCalling { get; set; }
    public bool IsActive { get; set; }
}
