namespace AiGateway.Application.Common.Models.VirtualKeys;

public record UsageLogItem(
    int? VirtualKeyId,
    int? ProviderId,
    int? ModelId,
    string RequestedModelAlias,
    int PromptTokens,
    int CompletionTokens,
    decimal CalculatedCostUsd,
    long LatencyMs,
    int HttpStatusCode,
    bool IsSuccess,
    string? ErrorMessage,
    string? ClientIp,
    string? UserAgent,
    DateTimeOffset RequestedAt
);
