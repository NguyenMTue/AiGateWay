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

        List<AiModel> candidateModels = new();
        RoutingStrategy strategy = RoutingStrategy.Priority;
        RouteRule? matchedRule = null;

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
            matchedRule = rule;
            strategy = rule.RoutingStrategy;

            if (rule.PrimaryModel != null)
                candidateModels.Add(rule.PrimaryModel);

            if (rule.FallbackModel != null)
                candidateModels.Add(rule.FallbackModel);
        }
        else
        {
            // 2. Fallback: Search AiModels directly by Alias or ModelId
            var directModels = await _context.AiModels
                .Include(m => m.Provider)
                    .ThenInclude(p => p.ApiKeys)
                .Where(m => m.IsActive && m.Provider.IsActive &&
                           (m.Alias.ToLower() == requestedModelAlias.ToLower() ||
                            m.ModelId.ToLower() == requestedModelAlias.ToLower()))
                .ToListAsync(cancellationToken);

            candidateModels.AddRange(directModels);
        }

        if (candidateModels.Count == 0)
        {
            throw new InvalidOperationException($"No active provider or API key available for requested model alias '{requestedModelAlias}'.");
        }

        // Apply advanced routing strategy sorting
        if (strategy == RoutingStrategy.LowestCost)
        {
            candidateModels = candidateModels
                .OrderBy(m => (m.PromptTokenCostPer1K + m.CompletionTokenCostPer1K))
                .ToList();
        }
        else if (strategy == RoutingStrategy.LowestLatency)
        {
            var modelIds = candidateModels.Select(m => m.Id).ToList();
            var windowStart = DateTimeOffset.UtcNow.AddHours(-24);

            var avgLatencies = await _context.RequestLogs
                .AsNoTracking()
                .Where(r => r.IsSuccess && r.RequestedAt >= windowStart && r.AiModelId.HasValue && modelIds.Contains(r.AiModelId.Value))
                .GroupBy(r => r.AiModelId!.Value)
                .Select(g => new { ModelId = g.Key, AvgLatency = g.Average(x => (double)x.LatencyMs) })
                .ToDictionaryAsync(x => x.ModelId, x => x.AvgLatency, cancellationToken);

            candidateModels = candidateModels
                .OrderBy(m => avgLatencies.TryGetValue(m.Id, out var lat) ? lat : 0.0)
                .ToList();
        }

        foreach (var model in candidateModels)
        {
            var target = PickKeyAndBuildTarget(model, strategy, matchedRule, excludedSet);
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
