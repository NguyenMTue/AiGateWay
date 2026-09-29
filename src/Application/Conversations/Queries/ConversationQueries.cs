using AiGateway.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Application.Conversations.Queries;

public record GetConversationsQuery(
    string? UserId = null,
    string? ConversationId = null,
    int Top = 20,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<List<ChatHistoryDto>>;

public class GetConversationsQueryHandler : IRequestHandler<GetConversationsQuery, List<ChatHistoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetConversationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ChatHistoryDto>> Handle(GetConversationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ChatHistories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            query = query.Where(c => c.UserId == request.UserId);
        }

        if (!string.IsNullOrWhiteSpace(request.ConversationId))
        {
            query = query.Where(c => c.ConversationId == request.ConversationId);
        }

        var limit = request.Top > 0 ? request.Top : Math.Min(100, Math.Max(1, request.PageSize));

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .Select(c => new ChatHistoryDto
            {
                Id = c.Id,
                UserId = c.UserId,
                VirtualKeyId = c.VirtualKeyId,
                ConversationId = c.ConversationId,
                Role = c.Role,
                Content = c.Content,
                ModelAlias = c.ModelAlias,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
