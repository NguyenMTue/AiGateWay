using AiGateway.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Application.VirtualKeys.Queries;

public record GetVirtualKeysQuery : IRequest<List<VirtualKeyDto>>;

public class GetVirtualKeysQueryHandler : IRequestHandler<GetVirtualKeysQuery, List<VirtualKeyDto>>
{
    private readonly IApplicationDbContext _context;

    public GetVirtualKeysQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<VirtualKeyDto>> Handle(GetVirtualKeysQuery request, CancellationToken cancellationToken)
    {
        return await _context.VirtualKeys
            .AsNoTracking()
            .Select(vk => new VirtualKeyDto
            {
                Id = vk.Id,
                Name = vk.Name,
                KeyPrefix = vk.KeyPrefix,
                KeyMask = vk.KeyMask,
                RateLimitRpm = vk.RateLimitRpm,
                RateLimitTpm = vk.RateLimitTpm,
                MaxBudgetUsd = vk.MaxBudgetUsd,
                CurrentUsageUsd = vk.CurrentUsageUsd,
                ExpiresAt = vk.ExpiresAt,
                IsActive = vk.IsActive,
                LastUsedAt = vk.LastUsedAt
            })
            .ToListAsync(cancellationToken);
    }
}

public record GetVirtualKeyByIdQuery(int Id) : IRequest<VirtualKeyDto?>;

public class GetVirtualKeyByIdQueryHandler : IRequestHandler<GetVirtualKeyByIdQuery, VirtualKeyDto?>
{
    private readonly IApplicationDbContext _context;

    public GetVirtualKeyByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VirtualKeyDto?> Handle(GetVirtualKeyByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.VirtualKeys
            .AsNoTracking()
            .Where(vk => vk.Id == request.Id)
            .Select(vk => new VirtualKeyDto
            {
                Id = vk.Id,
                Name = vk.Name,
                KeyPrefix = vk.KeyPrefix,
                KeyMask = vk.KeyMask,
                RateLimitRpm = vk.RateLimitRpm,
                RateLimitTpm = vk.RateLimitTpm,
                MaxBudgetUsd = vk.MaxBudgetUsd,
                CurrentUsageUsd = vk.CurrentUsageUsd,
                ExpiresAt = vk.ExpiresAt,
                IsActive = vk.IsActive,
                LastUsedAt = vk.LastUsedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
