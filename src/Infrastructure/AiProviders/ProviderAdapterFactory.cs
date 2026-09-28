using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Enums;

namespace AiGateway.Infrastructure.AiProviders;

public class ProviderAdapterFactory : IAiProviderAdapterFactory
{
    private readonly IEnumerable<IAiProviderAdapter> _adapters;

    public ProviderAdapterFactory(IEnumerable<IAiProviderAdapter> adapters)
    {
        _adapters = adapters;
    }

    public IAiProviderAdapter GetAdapter(ProviderType providerType)
    {
        var adapter = _adapters.FirstOrDefault(a => a.ProviderType == providerType);

        return adapter ?? throw new NotSupportedException($"No provider adapter registered for ProviderType '{providerType}'.");
    }
}
