using AiGateway.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Application.Providers.Queries;

public record GetProvidersQuery : IRequest<List<ProviderDto>>;

public class GetProvidersQueryHandler : IRequestHandler<GetProvidersQuery, List<ProviderDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProvidersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProviderDto>> Handle(GetProvidersQuery request, CancellationToken cancellationToken)
    {
        return await _context.AiProviders
            .AsNoTracking()
            .Select(p => new ProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                ProviderType = p.ProviderType,
                BaseUrl = p.BaseUrl,
                IsActive = p.IsActive,
                TimeoutSeconds = p.TimeoutSeconds,
                MaxRetries = p.MaxRetries
            })
            .ToListAsync(cancellationToken);
    }
}

public record GetProviderByIdQuery(int Id) : IRequest<ProviderDto?>;

public class GetProviderByIdQueryHandler : IRequestHandler<GetProviderByIdQuery, ProviderDto?>
{
    private readonly IApplicationDbContext _context;

    public GetProviderByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProviderDto?> Handle(GetProviderByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.AiProviders
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(p => new ProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                ProviderType = p.ProviderType,
                BaseUrl = p.BaseUrl,
                IsActive = p.IsActive,
                TimeoutSeconds = p.TimeoutSeconds,
                MaxRetries = p.MaxRetries
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
