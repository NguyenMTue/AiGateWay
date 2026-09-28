using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Entities;
using AiGateway.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Application.Providers.Commands;

public record CreateProviderCommand(
    string Name,
    string Slug,
    ProviderType ProviderType,
    string BaseUrl,
    int TimeoutSeconds = 60,
    int MaxRetries = 3
) : IRequest<int>;

public class CreateProviderCommandHandler : IRequestHandler<CreateProviderCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateProviderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateProviderCommand request, CancellationToken cancellationToken)
    {
        var entity = new AiProvider
        {
            Name = request.Name,
            Slug = request.Slug.ToLowerInvariant(),
            ProviderType = request.ProviderType,
            BaseUrl = request.BaseUrl,
            TimeoutSeconds = request.TimeoutSeconds,
            MaxRetries = request.MaxRetries,
            IsActive = true
        };

        _context.AiProviders.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}

public record UpdateProviderCommand(
    int Id,
    string Name,
    string Slug,
    ProviderType ProviderType,
    string BaseUrl,
    int TimeoutSeconds,
    int MaxRetries,
    bool IsActive
) : IRequest<bool>;

public class UpdateProviderCommandHandler : IRequestHandler<UpdateProviderCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateProviderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateProviderCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.AiProviders
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (entity == null) return false;

        entity.Name = request.Name;
        entity.Slug = request.Slug.ToLowerInvariant();
        entity.ProviderType = request.ProviderType;
        entity.BaseUrl = request.BaseUrl;
        entity.TimeoutSeconds = request.TimeoutSeconds;
        entity.MaxRetries = request.MaxRetries;
        entity.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public record DeleteProviderCommand(int Id) : IRequest<bool>;

public class DeleteProviderCommandHandler : IRequestHandler<DeleteProviderCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteProviderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteProviderCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.AiProviders
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (entity == null) return false;

        _context.AiProviders.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
