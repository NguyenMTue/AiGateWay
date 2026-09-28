using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiGateway.Infrastructure.Metering;

public class UsageMeteringBackgroundService : BackgroundService
{
    private readonly UsageMeteringChannel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UsageMeteringBackgroundService> _logger;

    public UsageMeteringBackgroundService(
        UsageMeteringChannel channel,
        IServiceProvider serviceProvider,
        ILogger<UsageMeteringBackgroundService> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Usage Metering Background Service started.");

        while (await _channel.Reader.WaitToReadAsync(stoppingToken))
        {
            while (_channel.Reader.TryRead(out var item))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                    var requestLog = new RequestLog
                    {
                        VirtualKeyId = item.VirtualKeyId,
                        AiProviderId = item.ProviderId,
                        AiModelId = item.ModelId,
                        RequestedModelAlias = item.RequestedModelAlias,
                        PromptTokens = item.PromptTokens,
                        CompletionTokens = item.CompletionTokens,
                        TotalTokens = item.PromptTokens + item.CompletionTokens,
                        CalculatedCostUsd = item.CalculatedCostUsd,
                        LatencyMs = item.LatencyMs,
                        HttpStatusCode = item.HttpStatusCode,
                        IsSuccess = item.IsSuccess,
                        ErrorMessage = item.ErrorMessage,
                        ClientIp = item.ClientIp,
                        UserAgent = item.UserAgent,
                        RequestedAt = item.RequestedAt
                    };

                    dbContext.RequestLogs.Add(requestLog);

                    // Update accumulated spend and last used timestamp for virtual key
                    if (item.VirtualKeyId.HasValue && item.CalculatedCostUsd > 0)
                    {
                        var key = await dbContext.VirtualKeys
                            .FirstOrDefaultAsync(vk => vk.Id == item.VirtualKeyId.Value, stoppingToken);

                        if (key != null)
                        {
                            key.CurrentUsageUsd += item.CalculatedCostUsd;
                            key.LastUsedAt = item.RequestedAt;
                        }
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing background usage metering log for requested model '{RequestedModel}'.", item.RequestedModelAlias);
                }
            }
        }

        _logger.LogInformation("Usage Metering Background Service stopped.");
    }
}
