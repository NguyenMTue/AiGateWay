using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.Common.Models.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiGateway.Infrastructure.Monitoring;

public class ProviderHealthMonitoringBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WebhookSettings _webhookSettings;
    private readonly ILogger<ProviderHealthMonitoringBackgroundService> _logger;

    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan WindowDuration { get; set; } = TimeSpan.FromMinutes(15);
    public int MinRequestsForThresholdCheck { get; set; } = 5;

    public ProviderHealthMonitoringBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<WebhookSettings> webhookSettings,
        ILogger<ProviderHealthMonitoringBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _webhookSettings = webhookSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Provider Health Monitoring Background Service started.");

        using var timer = new PeriodicTimer(MonitoringInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!_webhookSettings.Enabled)
            {
                continue;
            }

            try
            {
                await CheckProviderHealthAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during provider health monitoring background check.");
            }
        }

        _logger.LogInformation("Provider Health Monitoring Background Service stopped.");
    }

    public async Task CheckProviderHealthAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var webhookService = scope.ServiceProvider.GetService<IWebhookNotificationService>();

        if (webhookService == null) return;

        var windowStart = DateTimeOffset.UtcNow.Subtract(WindowDuration);

        var logs = await dbContext.RequestLogs
            .AsNoTracking()
            .Include(r => r.AiProvider)
            .Where(r => r.RequestedAt >= windowStart && r.AiProviderId.HasValue)
            .ToListAsync(cancellationToken);

        var providerGroups = logs.GroupBy(r => new
        {
            ProviderId = r.AiProviderId!.Value,
            ProviderName = r.AiProvider != null ? r.AiProvider.Name : $"Provider #{r.AiProviderId.Value}"
        });

        foreach (var group in providerGroups)
        {
            var totalRequests = group.Count();
            if (totalRequests < MinRequestsForThresholdCheck)
            {
                continue;
            }

            var failedRequests = group.Count(x => !x.IsSuccess);
            var error429Count = group.Count(x => x.HttpStatusCode == 429);
            var error5xxCount = group.Count(x => x.HttpStatusCode >= 500 && x.HttpStatusCode < 600);
            var errorRatePercent = (double)failedRequests / totalRequests * 100.0;

            if (errorRatePercent > _webhookSettings.ProviderErrorRateThresholdPercent)
            {
                _logger.LogWarning("Provider '{ProviderName}' ({ProviderId}) error rate ({ErrorRate:F1}%) exceeded threshold ({Threshold}%).",
                    group.Key.ProviderName, group.Key.ProviderId, errorRatePercent, _webhookSettings.ProviderErrorRateThresholdPercent);

                await webhookService.SendProviderHealthAlertAsync(
                    group.Key.ProviderId,
                    group.Key.ProviderName,
                    errorRatePercent,
                    totalRequests,
                    failedRequests,
                    error429Count,
                    error5xxCount,
                    cancellationToken);
            }
        }
    }
}
