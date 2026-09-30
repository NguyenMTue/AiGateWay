using AiGateway.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Application.Analytics.Queries;

public record GetCostAndTokenUsageQuery(
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    string GroupBy = "day",
    int? VirtualKeyId = null,
    int? AiProviderId = null
) : IRequest<CostAndTokenUsageReportDto>;

public class GetCostAndTokenUsageQueryHandler : IRequestHandler<GetCostAndTokenUsageQuery, CostAndTokenUsageReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetCostAndTokenUsageQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CostAndTokenUsageReportDto> Handle(GetCostAndTokenUsageQuery request, CancellationToken cancellationToken)
    {
        var start = request.StartDate ?? DateTimeOffset.UtcNow.AddDays(-30);
        var end = request.EndDate ?? DateTimeOffset.UtcNow;

        var query = _context.RequestLogs
            .AsNoTracking()
            .Where(r => r.RequestedAt >= start && r.RequestedAt <= end);

        if (request.VirtualKeyId.HasValue)
        {
            query = query.Where(r => r.VirtualKeyId == request.VirtualKeyId.Value);
        }

        if (request.AiProviderId.HasValue)
        {
            query = query.Where(r => r.AiProviderId == request.AiProviderId.Value);
        }

        var totalPromptTokens = await query.SumAsync(x => (long)x.PromptTokens, cancellationToken);
        var totalCompletionTokens = await query.SumAsync(x => (long)x.CompletionTokens, cancellationToken);
        var totalTokens = await query.SumAsync(x => (long)x.TotalTokens, cancellationToken);
        var totalCostUsd = await query.SumAsync(x => x.CalculatedCostUsd, cancellationToken);
        var totalRequests = await query.CountAsync(cancellationToken);

        var isMonth = string.Equals(request.GroupBy, "month", StringComparison.OrdinalIgnoreCase);

        var logs = await query
            .Select(x => new 
            { 
                x.PromptTokens, 
                x.CompletionTokens, 
                x.TotalTokens, 
                x.CalculatedCostUsd, 
                x.RequestedAt 
            })
            .ToListAsync(cancellationToken);

        var groupedSeries = logs
            .GroupBy(l => isMonth 
                ? l.RequestedAt.ToString("yyyy-MM") 
                : l.RequestedAt.ToString("yyyy-MM-dd"))
            .Select(g => new PeriodUsageSummaryDto
            {
                Period = g.Key,
                PromptTokens = g.Sum(x => (long)x.PromptTokens),
                CompletionTokens = g.Sum(x => (long)x.CompletionTokens),
                TotalTokens = g.Sum(x => (long)x.TotalTokens),
                CostUsd = g.Sum(x => x.CalculatedCostUsd),
                RequestCount = g.Count()
            })
            .OrderBy(g => g.Period)
            .ToList();

        return new CostAndTokenUsageReportDto
        {
            TotalPromptTokens = totalPromptTokens,
            TotalCompletionTokens = totalCompletionTokens,
            TotalTokens = totalTokens,
            TotalCostUsd = totalCostUsd,
            TotalRequests = totalRequests,
            TimeSeries = groupedSeries
        };
    }
}

public record GetVirtualKeyUsageStatsQuery(
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    int Top = 10
) : IRequest<List<VirtualKeyUsageStatsDto>>;

public class GetVirtualKeyUsageStatsQueryHandler : IRequestHandler<GetVirtualKeyUsageStatsQuery, List<VirtualKeyUsageStatsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetVirtualKeyUsageStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<VirtualKeyUsageStatsDto>> Handle(GetVirtualKeyUsageStatsQuery request, CancellationToken cancellationToken)
    {
        var start = request.StartDate ?? DateTimeOffset.UtcNow.AddDays(-30);
        var end = request.EndDate ?? DateTimeOffset.UtcNow;

        var logs = await _context.RequestLogs
            .AsNoTracking()
            .Include(r => r.VirtualKey)
            .Where(r => r.RequestedAt >= start && r.RequestedAt <= end)
            .ToListAsync(cancellationToken);

        var result = logs
            .GroupBy(r => new 
            { 
                r.VirtualKeyId, 
                KeyName = r.VirtualKey != null ? r.VirtualKey.Name : "Direct API/Unknown",
                KeyPrefix = r.VirtualKey != null ? r.VirtualKey.KeyPrefix : "" 
            })
            .Select(g => new VirtualKeyUsageStatsDto
            {
                VirtualKeyId = g.Key.VirtualKeyId,
                KeyName = g.Key.KeyName,
                KeyPrefix = g.Key.KeyPrefix,
                PromptTokens = g.Sum(x => (long)x.PromptTokens),
                CompletionTokens = g.Sum(x => (long)x.CompletionTokens),
                TotalTokens = g.Sum(x => (long)x.TotalTokens),
                TotalCostUsd = g.Sum(x => x.CalculatedCostUsd),
                RequestCount = g.Count(),
                SuccessfulRequests = g.Count(x => x.IsSuccess),
                FailedRequests = g.Count(x => !x.IsSuccess)
            })
            .OrderByDescending(x => x.TotalCostUsd)
            .ThenByDescending(x => x.TotalTokens)
            .Take(Math.Max(1, request.Top))
            .ToList();

        return result;
    }
}

public record GetProviderHealthStatsQuery(
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null
) : IRequest<List<ProviderHealthStatsDto>>;

public class GetProviderHealthStatsQueryHandler : IRequestHandler<GetProviderHealthStatsQuery, List<ProviderHealthStatsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProviderHealthStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProviderHealthStatsDto>> Handle(GetProviderHealthStatsQuery request, CancellationToken cancellationToken)
    {
        var start = request.StartDate ?? DateTimeOffset.UtcNow.AddDays(-30);
        var end = request.EndDate ?? DateTimeOffset.UtcNow;

        var logs = await _context.RequestLogs
            .AsNoTracking()
            .Include(r => r.AiProvider)
            .Where(r => r.RequestedAt >= start && r.RequestedAt <= end)
            .ToListAsync(cancellationToken);

        var result = logs
            .GroupBy(r => new 
            { 
                r.AiProviderId, 
                ProviderName = r.AiProvider != null ? r.AiProvider.Name : "Unknown Provider" 
            })
            .Select(g =>
            {
                var total = g.Count();
                var failed = g.Count(x => !x.IsSuccess);
                var avgLatency = total > 0 ? g.Average(x => (double)x.LatencyMs) : 0.0;
                var errRate = total > 0 ? (double)failed / total * 100.0 : 0.0;

                return new ProviderHealthStatsDto
                {
                    AiProviderId = g.Key.AiProviderId,
                    ProviderName = g.Key.ProviderName,
                    TotalRequests = total,
                    SuccessfulRequests = g.Count(x => x.IsSuccess),
                    FailedRequests = failed,
                    Error429Count = g.Count(x => x.HttpStatusCode == 429),
                    Error5xxCount = g.Count(x => x.HttpStatusCode >= 500 && x.HttpStatusCode < 600),
                    ErrorRatePercent = Math.Round(errRate, 2),
                    AverageLatencyMs = Math.Round(avgLatency, 2)
                };
            })
            .OrderByDescending(x => x.TotalRequests)
            .ToList();

        return result;
    }
}
