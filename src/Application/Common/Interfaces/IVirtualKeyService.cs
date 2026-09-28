using AiGateway.Domain.Entities;

namespace AiGateway.Application.Common.Interfaces;

public interface IVirtualKeyService
{
    Task<VirtualKey?> ValidateKeyAsync(string rawVirtualKey, CancellationToken cancellationToken);
    (string RawKey, string KeyHash, string KeyPrefix, string KeyMask) GenerateNewKey(string prefix = "gw-live-");
}
