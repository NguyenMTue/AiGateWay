namespace AiGateway.Domain.Entities;

public class RequestLog : BaseEntity
{
    public int? VirtualKeyId { get; set; }

    public VirtualKey? VirtualKey { get; set; }

    public int? AiProviderId { get; set; }

    public AiProvider? AiProvider { get; set; }

    public int? AiModelId { get; set; }

    public AiModel? AiModel { get; set; }

    public string RequestedModelAlias { get; set; } = string.Empty;

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public decimal CalculatedCostUsd { get; set; }

    public long LatencyMs { get; set; }

    public int HttpStatusCode { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ClientIp { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
}
