using AiGateway.Application.AiModels;
using AiGateway.Application.AiModels.Commands;
using AiGateway.Application.AiModels.Queries;
using AiGateway.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AiGateway.Web.Endpoints;

public class ModelEndpoints : IEndpointGroup
{
    public static string? RoutePrefix => "/api/models";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet("/all", GetModels);
        groupBuilder.MapGet("/id/{id:int}", GetModelById);
        groupBuilder.MapPost("/create", CreateModel);
        groupBuilder.MapPut("/update", UpdateModel);
        groupBuilder.MapDelete("/delete/{id:int}", DeleteModel);
    }

    public static async Task<Ok<List<AiModelDto>>> GetModels(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetModelsQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }

    public static async Task<Results<Ok<AiModelDto>, NotFound>> GetModelById(
        int id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetModelByIdQuery(id), cancellationToken);
        return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    public static async Task<Ok<int>> CreateModel(
        [FromBody] CreateModelCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return TypedResults.Ok(id);
    }

    public static async Task<Results<NoContent, NotFound>> UpdateModel(
        [FromBody] UpdateModelCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var success = await sender.Send(command, cancellationToken);
        return success ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    public static async Task<Results<NoContent, NotFound>> DeleteModel(
        int id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var success = await sender.Send(new DeleteModelCommand(id), cancellationToken);
        return success ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
