using AiGateway.Application.VirtualKeys;
using AiGateway.Application.VirtualKeys.Commands;
using AiGateway.Application.VirtualKeys.Queries;
using AiGateway.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AiGateway.Web.Endpoints;

public class VirtualKeyEndpoints : IEndpointGroup
{
    public static string? RoutePrefix => "/api/virtual-keys";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet("/all", GetVirtualKeys);
        groupBuilder.MapGet("/id/{id:int}", GetVirtualKeyById);
        groupBuilder.MapPost("/create", CreateVirtualKey);
        groupBuilder.MapPut("/update", UpdateVirtualKey);
        groupBuilder.MapDelete("/delete/{id:int}", DeleteVirtualKey);
    }

    public static async Task<Ok<List<VirtualKeyDto>>> GetVirtualKeys(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetVirtualKeysQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }

    public static async Task<Results<Ok<VirtualKeyDto>, NotFound>> GetVirtualKeyById(
        int id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetVirtualKeyByIdQuery(id), cancellationToken);
        return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    public static async Task<Ok<CreatedVirtualKeyResponseDto>> CreateVirtualKey(
        [FromBody] CreateVirtualKeyCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return TypedResults.Ok(result);
    }

    public static async Task<Results<NoContent, NotFound>> UpdateVirtualKey(
        [FromBody] UpdateVirtualKeyCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var success = await sender.Send(command, cancellationToken);
        return success ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    public static async Task<Results<NoContent, NotFound>> DeleteVirtualKey(
        int id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var success = await sender.Send(new DeleteVirtualKeyCommand(id), cancellationToken);
        return success ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
