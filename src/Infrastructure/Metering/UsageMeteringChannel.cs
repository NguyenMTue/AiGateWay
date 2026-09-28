using System.Threading.Channels;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.Common.Models.VirtualKeys;

namespace AiGateway.Infrastructure.Metering;

public class UsageMeteringChannel : IUsageMeteringChannel
{
    private readonly Channel<UsageLogItem> _channel;

    public UsageMeteringChannel()
    {
        _channel = Channel.CreateUnbounded<UsageLogItem>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
    }

    public ValueTask QueueUsageLogAsync(UsageLogItem item, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public ChannelReader<UsageLogItem> Reader => _channel.Reader;
}
