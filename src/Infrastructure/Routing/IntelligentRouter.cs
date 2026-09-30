using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Entities;
using AiGateway.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Infrastructure.Routing;

public class IntelligentRouter : IIntelligentRouter
{
    private readonly IApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private static readonly Random _random = new();

    public IntelligentRouter(IApplicationDbContext context, IEncryptionService encryptionService)
    {
        _context = context;
        _encryptionService = encryptionService;
    }

    public async Task<RouteExecutionTarget> ResolveTargetAsync(
        string requestedModelAlias,
        CancellationToken cancellationToken,
        IEnumerable<int>? excludeApiKeyIds = null)
    {
        var excludedSet = excludeApiKeyIds != null 
            ? new HashSet<int>(excludeApiKeyIds) 
            : new HashSet<int>();

        // 1. Check for explicit RouteRule matching alias
        var rule = await _context.RouteRules
            .Include(r => r.PrimaryModel)
                .ThenInclude(m => m.Provider)
                    .ThenInclude(p => p.ApiKeys)
            .Include(r => r.FallbackModel!)
                .ThenInclude(m => m.Provider)
                    .ThenInclude(p => p.ApiKeys)
            .Where(r => r.IsActive && r.TargetModelAlias.ToLower() == requestedModelAlias.ToLower())
            .OrderBy(r => r.Priority)
            .FirstOrDefaultAsync(cancellationToken);

        if (rule != null)
        {
            var primaryTarget = PickKeyAndBuildTarget(rule.PrimaryModel, rule.RoutingStrategy, rule, excludedSet);
            if (primaryTarget != null)
            {
                return primaryTarget;
            }

            // If primary target has no active keys, try fallback model
            if (rule.FallbackModel != null)
            {
                var fallbackTarget = PickKeyAndBuildTarget(rule.FallbackModel, rule.RoutingStrategy, rule, excludedSet);
                if (fallbackTarget != null)
                {
                    return fallbackTarget;
                }
            }
        }

        // 2. Fallback: Search AiModels directly by Alias or ModelId
        var directModels = await _context.AiModels
            .Include(m => m.Provider)
                .ThenInclude(p => p.ApiKeys)
            .Where(m => m.IsActive && m.Provider.IsActive &&
                       (m.Alias.ToLower() == requestedModelAlias.ToLower() ||
                        m.ModelId.ToLower() == requestedModelAlias.ToLower()))
            .ToListAsync(cancellationToken);

        foreach (var model in directModels)
        {
            var target = PickKeyAndBuildTarget(model, RoutingStrategy.Priority, null, excludedSet);
            if (target != null)
            {
                return target;
            }
        }

        throw new InvalidOperationException($"No active provider or API key available for requested model alias '{requestedModelAlias}'.");
    }

    public async Task HandleProviderFailureAsync(
        int providerApiKeyId,
        int httpStatusCode,
        CancellationToken cancellationToken)
    {
        var key = await _context.ProviderApiKeys
            .FirstOrDefaultAsync(k => k.Id == providerApiKeyId, cancellationToken);

        if (key == null) return;

        if (httpStatusCode == 429 || httpStatusCode >= 500) // Rate limit hit or Server Error (500, 502, 503, 504)
        {
            // Place key on 5-minute cooldown
            key.CooldownUntil = DateTimeOffset.UtcNow.AddMinutes(5);
        }
        else if (httpStatusCode == 401 || httpStatusCode == 403) // Invalid API key
        {
            key.IsExhausted = true;
            key.IsActive = false;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private RouteExecutionTarget? PickKeyAndBuildTarget(
        AiModel model,
        RoutingStrategy strategy,
        RouteRule? rule,
        HashSet<int> excludedKeyIds)
    {
        if (!model.IsActive || !model.Provider.IsActive)
            return null;

        var now = DateTimeOffset.UtcNow;
        var validKeys = model.Provider.ApiKeys
            .Where(k => k.IsActive 
                        && !k.IsExhausted 
                        && (k.CooldownUntil == null || k.CooldownUntil <= now)
                        && !excludedKeyIds.Contains(k.Id))
            .ToList();

        if (validKeys.Count == 0)
            return null;

        ProviderApiKey selectedKey;

        switch (strategy)
        {
            case RoutingStrategy.WeightedRandom:
                var totalWeight = validKeys.Sum(k => Math.Max(1, k.Weight));
                var randomRoll = _random.Next(0, totalWeight);
                var cumulative = 0;
                selectedKey = validKeys.First();
                foreach (var k in validKeys)
                {
                    cumulative += Math.Max(1, k.Weight);
                    if (randomRoll < cumulative)
                    {
                        selectedKey = k;
                        break;
                    }
                }
                break;

            case RoutingStrategy.Priority:
            default:
                selectedKey = validKeys
                    .OrderBy(k => k.Priority)
                    .ThenBy(k => k.Id)
                    .First();
                break;
        }

        var decryptedKey = _encryptionService.Decrypt(selectedKey.EncryptedApiKey);

        return new RouteExecutionTarget(
            Provider: model.Provider,
            Model: model,
            ApiKey: selectedKey,
            DecryptedApiKey: decryptedKey,
            MatchingRule: rule
        );
    }
}
