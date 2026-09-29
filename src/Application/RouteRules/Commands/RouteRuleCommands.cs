using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Entities;
using AiGateway.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Application.RouteRules.Commands;

public record CreateRouteRuleCommand(
    string Name,
    string TargetModelAlias,
    RoutingStrategy RoutingStrategy,
    int PrimaryModelId,
    int? FallbackModelId = null,
    bool IsActive = true,
    int Priority = 1
) : IRequest<int>;

public class CreateRouteRuleCommandHandler : IRequestHandler<CreateRouteRuleCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateRouteRuleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateRouteRuleCommand request, CancellationToken cancellationToken)
    {
        var entity = new RouteRule
        {
            Name = request.Name,
            TargetModelAlias = request.TargetModelAlias.ToLowerInvariant(),
            RoutingStrategy = request.RoutingStrategy,
            PrimaryModelId = request.PrimaryModelId,
            FallbackModelId = request.FallbackModelId,
            IsActive = request.IsActive,
            Priority = request.Priority
        };

        _context.RouteRules.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}

public record UpdateRouteRuleCommand(
    int Id,
    string Name,
    string TargetModelAlias,
    RoutingStrategy RoutingStrategy,
    int PrimaryModelId,
    int? FallbackModelId,
    bool IsActive,
    int Priority
) : IRequest<bool>;

public class UpdateRouteRuleCommandHandler : IRequestHandler<UpdateRouteRuleCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateRouteRuleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateRouteRuleCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.RouteRules
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (entity == null) return false;

        entity.Name = request.Name;
        entity.TargetModelAlias = request.TargetModelAlias.ToLowerInvariant();
        entity.RoutingStrategy = request.RoutingStrategy;
        entity.PrimaryModelId = request.PrimaryModelId;
        entity.FallbackModelId = request.FallbackModelId;
        entity.IsActive = request.IsActive;
        entity.Priority = request.Priority;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public record DeleteRouteRuleCommand(int Id) : IRequest<bool>;

public class DeleteRouteRuleCommandHandler : IRequestHandler<DeleteRouteRuleCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteRouteRuleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteRouteRuleCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.RouteRules
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (entity == null) return false;

        _context.RouteRules.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
