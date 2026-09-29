using AiGateway.Application.RouteRules;
using AiGateway.Application.RouteRules.Commands;
using AiGateway.Application.RouteRules.Queries;
using AiGateway.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AiGateway.Web.Endpoints;

public class RouteRuleEndpoints : IEndpointGroup
{
    public static string? RoutePrefix => "/api/route-rules";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet("/all", GetRouteRules);
        groupBuilder.MapGet("/id/{id:int}", GetRouteRuleById);
        groupBuilder.MapPost("/create", CreateRouteRule);
        groupBuilder.MapPut("/update", UpdateRouteRule);
        groupBuilder.MapDelete("/delete/{id:int}", DeleteRouteRule);
    }

    public static async Task<Ok<List<RouteRuleDto>>> GetRouteRules(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRouteRulesQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }

    public static async Task<Results<Ok<RouteRuleDto>, NotFound>> GetRouteRuleById(
        int id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRouteRuleByIdQuery(id), cancellationToken);
        return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    public static async Task<Ok<int>> CreateRouteRule(
        [FromBody] CreateRouteRuleCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return TypedResults.Ok(id);
    }

    public static async Task<Results<NoContent, NotFound>> UpdateRouteRule(
        [FromBody] UpdateRouteRuleCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var success = await sender.Send(command, cancellationToken);
        return success ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    public static async Task<Results<NoContent, NotFound>> DeleteRouteRule(
        int id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var success = await sender.Send(new DeleteRouteRuleCommand(id), cancellationToken);
        return success ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
