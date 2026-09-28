using AiGateway.Domain.Enums;

namespace AiGateway.Application.Common.Interfaces;

public interface IAiProviderAdapterFactory
{
    IAiProviderAdapter GetAdapter(ProviderType providerType);
}
