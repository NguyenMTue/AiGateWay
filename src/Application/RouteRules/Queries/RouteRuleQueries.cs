using AiGateway.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Application.RouteRules.Queries;

public record GetRouteRulesQuery : IRequest<List<RouteRuleDto>>;

public class GetRouteRulesQueryHandler : IRequestHandler<GetRouteRulesQuery, List<RouteRuleDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRouteRulesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<RouteRuleDto>> Handle(GetRouteRulesQuery request, CancellationToken cancellationToken)
    {
        return await _context.RouteRules
            .AsNoTracking()
            .Include(r => r.PrimaryModel)
            .Include(r => r.FallbackModel)
            .OrderByDescending(r => r.Priority)
            .Select(r => new RouteRuleDto
            {
                Id = r.Id,
                Name = r.Name,
                TargetModelAlias = r.TargetModelAlias,
                RoutingStrategy = r.RoutingStrategy,
                PrimaryModelId = r.PrimaryModelId,
                PrimaryModelName = r.PrimaryModel != null ? r.PrimaryModel.Name : null,
                FallbackModelId = r.FallbackModelId,
                FallbackModelName = r.FallbackModel != null ? r.FallbackModel.Name : null,
                IsActive = r.IsActive,
                Priority = r.Priority
            })
            .ToListAsync(cancellationToken);
    }
}

public record GetRouteRuleByIdQuery(int Id) : IRequest<RouteRuleDto?>;

public class GetRouteRuleByIdQueryHandler : IRequestHandler<GetRouteRuleByIdQuery, RouteRuleDto?>
{
    private readonly IApplicationDbContext _context;

    public GetRouteRuleByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RouteRuleDto?> Handle(GetRouteRuleByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.RouteRules
            .AsNoTracking()
            .Include(r => r.PrimaryModel)
            .Include(r => r.FallbackModel)
            .Where(r => r.Id == request.Id)
            .Select(r => new RouteRuleDto
            {
                Id = r.Id,
                Name = r.Name,
                TargetModelAlias = r.TargetModelAlias,
                RoutingStrategy = r.RoutingStrategy,
                PrimaryModelId = r.PrimaryModelId,
                PrimaryModelName = r.PrimaryModel != null ? r.PrimaryModel.Name : null,
                FallbackModelId = r.FallbackModelId,
                FallbackModelName = r.FallbackModel != null ? r.FallbackModel.Name : null,
                IsActive = r.IsActive,
                Priority = r.Priority
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
