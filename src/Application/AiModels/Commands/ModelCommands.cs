using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Entities;
using AiGateway.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Application.AiModels.Commands;

public record CreateModelCommand(
    int AiProviderId,
    string Name,
    string ModelId,
    string Alias,
    ModelType ModelType,
    decimal PromptTokenCostPer1K,
    decimal CompletionTokenCostPer1K,
    int? ContextWindowTokens,
    int? MaxOutputTokens,
    bool SupportsStreaming = true,
    bool SupportsVision = false,
    bool SupportsFunctionCalling = true
) : IRequest<int>;

public class CreateModelCommandHandler : IRequestHandler<CreateModelCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateModelCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateModelCommand request, CancellationToken cancellationToken)
    {
        var entity = new AiModel
        {
            AiProviderId = request.AiProviderId,
            Name = request.Name,
            ModelId = request.ModelId,
            Alias = request.Alias.ToLowerInvariant(),
            ModelType = request.ModelType,
            PromptTokenCostPer1K = request.PromptTokenCostPer1K,
            CompletionTokenCostPer1K = request.CompletionTokenCostPer1K,
            ContextWindowTokens = request.ContextWindowTokens,
            MaxOutputTokens = request.MaxOutputTokens,
            SupportsStreaming = request.SupportsStreaming,
            SupportsVision = request.SupportsVision,
            SupportsFunctionCalling = request.SupportsFunctionCalling,
            IsActive = true
        };

        _context.AiModels.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}

public record UpdateModelCommand(
    int Id,
    int AiProviderId,
    string Name,
    string ModelId,
    string Alias,
    ModelType ModelType,
    decimal PromptTokenCostPer1K,
    decimal CompletionTokenCostPer1K,
    int? ContextWindowTokens,
    int? MaxOutputTokens,
    bool SupportsStreaming,
    bool SupportsVision,
    bool SupportsFunctionCalling,
    bool IsActive
) : IRequest<bool>;

public class UpdateModelCommandHandler : IRequestHandler<UpdateModelCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateModelCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateModelCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.AiModels
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        if (entity == null) return false;

        entity.AiProviderId = request.AiProviderId;
        entity.Name = request.Name;
        entity.ModelId = request.ModelId;
        entity.Alias = request.Alias.ToLowerInvariant();
        entity.ModelType = request.ModelType;
        entity.PromptTokenCostPer1K = request.PromptTokenCostPer1K;
        entity.CompletionTokenCostPer1K = request.CompletionTokenCostPer1K;
        entity.ContextWindowTokens = request.ContextWindowTokens;
        entity.MaxOutputTokens = request.MaxOutputTokens;
        entity.SupportsStreaming = request.SupportsStreaming;
        entity.SupportsVision = request.SupportsVision;
        entity.SupportsFunctionCalling = request.SupportsFunctionCalling;
        entity.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public record DeleteModelCommand(int Id) : IRequest<bool>;

public class DeleteModelCommandHandler : IRequestHandler<DeleteModelCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteModelCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteModelCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.AiModels
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        if (entity == null) return false;

        _context.AiModels.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
