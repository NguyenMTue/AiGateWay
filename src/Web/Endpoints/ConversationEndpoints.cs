using AiGateway.Application.Conversations;
using AiGateway.Application.Conversations.Commands;
using AiGateway.Application.Conversations.Queries;
using AiGateway.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AiGateway.Web.Endpoints;

public class ConversationEndpoints : IEndpointGroup
{
    public static string? RoutePrefix => "/conversations";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet("/", GetConversations);
        groupBuilder.MapPost("/add", AddMessage);
    }

    public static async Task<Ok<List<ChatHistoryDto>>> GetConversations(
        [FromQuery] string? userId,
        [FromQuery] string? conversationId,
        [FromQuery] int? top,
        [FromQuery] int? pageSize,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetConversationsQuery(
            userId,
            conversationId,
            top ?? 20,
            1,
            pageSize ?? 20
        );

        var result = await sender.Send(query, cancellationToken);
        return TypedResults.Ok(result);
    }

    public static async Task<Ok<int>> AddMessage(
        [FromBody] AddChatMessageCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return TypedResults.Ok(id);
    }
}

public class ApiConversationEndpoints : IEndpointGroup
{
    public static string? RoutePrefix => "/api/conversations";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet("/", ConversationEndpoints.GetConversations);
        groupBuilder.MapPost("/add", ConversationEndpoints.AddMessage);
    }
}
