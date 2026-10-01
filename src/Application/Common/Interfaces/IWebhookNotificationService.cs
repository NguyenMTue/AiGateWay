namespace AiGateway.Application.Common.Interfaces;

public interface IWebhookNotificationService
{
    Task SendVirtualKeyBudgetAlertAsync(
        int virtualKeyId,
        string keyName,
        string keyMask,
        decimal currentUsageUsd,
        decimal maxBudgetUsd,
        CancellationToken cancellationToken = default);

    Task SendProviderHealthAlertAsync(
        int providerId,
        string providerName,
        double errorRatePercent,
        int totalRequests,
        int failedRequests,
        int error429Count,
        int error5xxCount,
        CancellationToken cancellationToken = default);

    Task SendCircuitBreakerAlertAsync(
        int providerApiKeyId,
        string providerName,
        int consecutiveFailures,
        TimeSpan breakDuration,
        CancellationToken cancellationToken = default);
}
