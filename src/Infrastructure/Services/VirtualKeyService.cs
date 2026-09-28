using System.Security.Cryptography;
using System.Text;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Infrastructure.Services;

public class VirtualKeyService : IVirtualKeyService
{
    private readonly IApplicationDbContext _context;

    public VirtualKeyService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VirtualKey?> ValidateKeyAsync(string rawVirtualKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawVirtualKey))
            return null;

        var keyHash = HashKey(rawVirtualKey);

        var key = await _context.VirtualKeys
            .FirstOrDefaultAsync(vk => vk.KeyHash == keyHash, cancellationToken);

        if (key == null || !key.IsActive)
            return null;

        if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return null;

        if (key.MaxBudgetUsd.HasValue && key.CurrentUsageUsd >= key.MaxBudgetUsd.Value)
            return null;

        return key;
    }

    public (string RawKey, string KeyHash, string KeyPrefix, string KeyMask) GenerateNewKey(string prefix = "gw-live-")
    {
        var randomBytes = new byte[24];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var randomStr = Convert.ToHexString(randomBytes).ToLowerInvariant();
        var rawKey = $"{prefix}{randomStr}";
        var keyHash = HashKey(rawKey);

        var mask = $"{prefix}...{rawKey[^4..]}";

        return (rawKey, keyHash, prefix, mask);
    }

    public static string HashKey(string rawKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
