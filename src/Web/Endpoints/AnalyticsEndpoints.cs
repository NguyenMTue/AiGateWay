using AiGateway.Application.Analytics;
using AiGateway.Application.Analytics.Queries;
using AiGateway.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AiGateway.Web.Endpoints;

public class AnalyticsEndpoints : IEndpointGroup
{
    public static string? RoutePrefix => "/api/analytics";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet("/cost-usage", GetCostAndTokenUsage);
        groupBuilder.MapGet("/virtual-keys", GetVirtualKeyUsageStats);
        groupBuilder.MapGet("/provider-health", GetProviderHealthStats);
    }

    public static async Task<Ok<CostAndTokenUsageReportDto>> GetCostAndTokenUsage(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        [FromQuery] string? groupBy,
        [FromQuery] int? virtualKeyId,
        [FromQuery] int? aiProviderId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetCostAndTokenUsageQuery(
            startDate,
            endDate,
            groupBy ?? "day",
            virtualKeyId,
            aiProviderId
        );

        var result = await sender.Send(query, cancellationToken);
        return TypedResults.Ok(result);
    }

    public static async Task<Ok<List<VirtualKeyUsageStatsDto>>> GetVirtualKeyUsageStats(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        [FromQuery] int? top,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetVirtualKeyUsageStatsQuery(
            startDate,
            endDate,
            top ?? 10
        );

        var result = await sender.Send(query, cancellationToken);
        return TypedResults.Ok(result);
    }

    public static async Task<Ok<List<ProviderHealthStatsDto>>> GetProviderHealthStats(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetProviderHealthStatsQuery(
            startDate,
            endDate
        );

        var result = await sender.Send(query, cancellationToken);
        return TypedResults.Ok(result);
    }
}
