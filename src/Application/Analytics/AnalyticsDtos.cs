namespace AiGateway.Application.Analytics;

public class PeriodUsageSummaryDto
{
    public string Period { get; set; } = string.Empty;
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens { get; set; }
    public decimal CostUsd { get; set; }
    public int RequestCount { get; set; }
}

public class CostAndTokenUsageReportDto
{
    public long TotalPromptTokens { get; set; }
    public long TotalCompletionTokens { get; set; }
    public long TotalTokens { get; set; }
    public decimal TotalCostUsd { get; set; }
    public int TotalRequests { get; set; }
    public List<PeriodUsageSummaryDto> TimeSeries { get; set; } = new();
}

public class VirtualKeyUsageStatsDto
{
    public int? VirtualKeyId { get; set; }
    public string KeyName { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens { get; set; }
    public decimal TotalCostUsd { get; set; }
    public int RequestCount { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
}

public class ProviderHealthStatsDto
{
    public int? AiProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public int Error429Count { get; set; }
    public int Error5xxCount { get; set; }
    public double ErrorRatePercent { get; set; }
    public double AverageLatencyMs { get; set; }
}
