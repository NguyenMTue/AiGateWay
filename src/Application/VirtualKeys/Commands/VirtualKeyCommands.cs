using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Application.VirtualKeys.Commands;

public record CreateVirtualKeyCommand(
    string Name,
    int? RateLimitRpm,
    int? RateLimitTpm,
    decimal? MaxBudgetUsd,
    DateTimeOffset? ExpiresAt,
    string KeyPrefix = "gw-live-"
) : IRequest<CreatedVirtualKeyResponseDto>;

public class CreateVirtualKeyCommandHandler : IRequestHandler<CreateVirtualKeyCommand, CreatedVirtualKeyResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IVirtualKeyService _virtualKeyService;

    public CreateVirtualKeyCommandHandler(IApplicationDbContext context, IVirtualKeyService virtualKeyService)
    {
        _context = context;
        _virtualKeyService = virtualKeyService;
    }

    public async Task<CreatedVirtualKeyResponseDto> Handle(CreateVirtualKeyCommand request, CancellationToken cancellationToken)
    {
        var (rawKey, keyHash, prefix, mask) = _virtualKeyService.GenerateNewKey(request.KeyPrefix);

        var entity = new VirtualKey
        {
            Name = request.Name,
            KeyHash = keyHash,
            KeyPrefix = prefix,
            KeyMask = mask,
            RateLimitRpm = request.RateLimitRpm,
            RateLimitTpm = request.RateLimitTpm,
            MaxBudgetUsd = request.MaxBudgetUsd,
            CurrentUsageUsd = 0.00m,
            ExpiresAt = request.ExpiresAt,
            IsActive = true
        };

        _context.VirtualKeys.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreatedVirtualKeyResponseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            RawVirtualKey = rawKey,
            KeyMask = mask
        };
    }
}

public record UpdateVirtualKeyCommand(
    int Id,
    string Name,
    int? RateLimitRpm,
    int? RateLimitTpm,
    decimal? MaxBudgetUsd,
    DateTimeOffset? ExpiresAt,
    bool IsActive
) : IRequest<bool>;

public class UpdateVirtualKeyCommandHandler : IRequestHandler<UpdateVirtualKeyCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateVirtualKeyCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateVirtualKeyCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.VirtualKeys
            .FirstOrDefaultAsync(vk => vk.Id == request.Id, cancellationToken);

        if (entity == null) return false;

        entity.Name = request.Name;
        entity.RateLimitRpm = request.RateLimitRpm;
        entity.RateLimitTpm = request.RateLimitTpm;
        entity.MaxBudgetUsd = request.MaxBudgetUsd;
        entity.ExpiresAt = request.ExpiresAt;
        entity.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public record DeleteVirtualKeyCommand(int Id) : IRequest<bool>;

public class DeleteVirtualKeyCommandHandler : IRequestHandler<DeleteVirtualKeyCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteVirtualKeyCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteVirtualKeyCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.VirtualKeys
            .FirstOrDefaultAsync(vk => vk.Id == request.Id, cancellationToken);

        if (entity == null) return false;

        _context.VirtualKeys.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
