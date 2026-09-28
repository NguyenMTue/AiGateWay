using AiGateway.Domain.Entities;

namespace AiGateway.Application.Common.Interfaces;

public record RouteExecutionTarget(
    AiProvider Provider,
    AiModel Model,
    ProviderApiKey ApiKey,
    string DecryptedApiKey,
    RouteRule? MatchingRule
);

public interface IIntelligentRouter
{
    Task<RouteExecutionTarget> ResolveTargetAsync(
        string requestedModelAlias,
        CancellationToken cancellationToken);

    Task HandleProviderFailureAsync(
        int providerApiKeyId,
        int httpStatusCode,
        CancellationToken cancellationToken);
}
