using AiGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<AiProvider> AiProviders { get; }

    DbSet<AiModel> AiModels { get; }

    DbSet<ProviderApiKey> ProviderApiKeys { get; }

    DbSet<VirtualKey> VirtualKeys { get; }

    DbSet<RouteRule> RouteRules { get; }

    DbSet<RequestLog> RequestLogs { get; }

    DbSet<ChatHistory> ChatHistories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
