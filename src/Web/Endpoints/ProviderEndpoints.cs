using AiGateway.Application.Providers;
using AiGateway.Application.Providers.Commands;
using AiGateway.Application.Providers.Queries;
using AiGateway.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AiGateway.Web.Endpoints;

public class ProviderEndpoints : IEndpointGroup
{
    public static string? RoutePrefix => "/api/providers";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet("/all", GetProviders);
        groupBuilder.MapGet("/id/{id:int}", GetProviderById);
        groupBuilder.MapPost("/create", CreateProvider);
        groupBuilder.MapPut("/update", UpdateProvider);
        groupBuilder.MapDelete("/delete/{id:int}", DeleteProvider);
    }

    public static async Task<Ok<List<ProviderDto>>> GetProviders(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProvidersQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }

    public static async Task<Results<Ok<ProviderDto>, NotFound>> GetProviderById(
        int id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProviderByIdQuery(id), cancellationToken);
        return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    public static async Task<Ok<int>> CreateProvider(
        [FromBody] CreateProviderCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return TypedResults.Ok(id);
    }

    public static async Task<Results<NoContent, NotFound>> UpdateProvider(
        [FromBody] UpdateProviderCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var success = await sender.Send(command, cancellationToken);
        return success ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    public static async Task<Results<NoContent, NotFound>> DeleteProvider(
        int id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var success = await sender.Send(new DeleteProviderCommand(id), cancellationToken);
        return success ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
