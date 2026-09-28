using AiGateway.Application.Common.Models.VirtualKeys;

namespace AiGateway.Application.Common.Interfaces;

public interface IUsageMeteringChannel
{
    ValueTask QueueUsageLogAsync(UsageLogItem item, CancellationToken cancellationToken = default);
}
