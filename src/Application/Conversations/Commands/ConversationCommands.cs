using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Entities;
using MediatR;

namespace AiGateway.Application.Conversations.Commands;

public record AddChatMessageCommand(
    string? UserId,
    string? ConversationId,
    string Role,
    string Content,
    string? ModelAlias = null,
    int? VirtualKeyId = null
) : IRequest<int>;

public class AddChatMessageCommandHandler : IRequestHandler<AddChatMessageCommand, int>
{
    private readonly IApplicationDbContext _context;

    public AddChatMessageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(AddChatMessageCommand request, CancellationToken cancellationToken)
    {
        var entity = new ChatHistory
        {
            UserId = request.UserId,
            ConversationId = !string.IsNullOrWhiteSpace(request.ConversationId) 
                ? request.ConversationId 
                : (request.UserId ?? "default-session"),
            Role = request.Role.ToLowerInvariant(),
            Content = request.Content,
            ModelAlias = request.ModelAlias,
            VirtualKeyId = request.VirtualKeyId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.ChatHistories.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
