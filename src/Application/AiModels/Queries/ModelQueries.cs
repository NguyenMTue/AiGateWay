using AiGateway.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Application.AiModels.Queries;

public record GetModelsQuery : IRequest<List<AiModelDto>>;

public class GetModelsQueryHandler : IRequestHandler<GetModelsQuery, List<AiModelDto>>
{
    private readonly IApplicationDbContext _context;

    public GetModelsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AiModelDto>> Handle(GetModelsQuery request, CancellationToken cancellationToken)
    {
        return await _context.AiModels
            .AsNoTracking()
            .Include(m => m.Provider)
            .Select(m => new AiModelDto
            {
                Id = m.Id,
                AiProviderId = m.AiProviderId,
                ProviderName = m.Provider.Name,
                Name = m.Name,
                ModelId = m.ModelId,
                Alias = m.Alias,
                ModelType = m.ModelType,
                PromptTokenCostPer1K = m.PromptTokenCostPer1K,
                CompletionTokenCostPer1K = m.CompletionTokenCostPer1K,
                ContextWindowTokens = m.ContextWindowTokens,
                MaxOutputTokens = m.MaxOutputTokens,
                SupportsStreaming = m.SupportsStreaming,
                SupportsVision = m.SupportsVision,
                SupportsFunctionCalling = m.SupportsFunctionCalling,
                IsActive = m.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}

public record GetModelByIdQuery(int Id) : IRequest<AiModelDto?>;

public class GetModelByIdQueryHandler : IRequestHandler<GetModelByIdQuery, AiModelDto?>
{
    private readonly IApplicationDbContext _context;

    public GetModelByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AiModelDto?> Handle(GetModelByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.AiModels
            .AsNoTracking()
            .Include(m => m.Provider)
            .Where(m => m.Id == request.Id)
            .Select(m => new AiModelDto
            {
                Id = m.Id,
                AiProviderId = m.AiProviderId,
                ProviderName = m.Provider.Name,
                Name = m.Name,
                ModelId = m.ModelId,
                Alias = m.Alias,
                ModelType = m.ModelType,
                PromptTokenCostPer1K = m.PromptTokenCostPer1K,
                CompletionTokenCostPer1K = m.CompletionTokenCostPer1K,
                ContextWindowTokens = m.ContextWindowTokens,
                MaxOutputTokens = m.MaxOutputTokens,
                SupportsStreaming = m.SupportsStreaming,
                SupportsVision = m.SupportsVision,
                SupportsFunctionCalling = m.SupportsFunctionCalling,
                IsActive = m.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
