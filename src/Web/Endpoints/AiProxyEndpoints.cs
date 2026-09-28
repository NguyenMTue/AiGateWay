using System.Diagnostics;
using System.Text.Json.Serialization;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.Common.Models.AiProxy;
using AiGateway.Application.Common.Models.VirtualKeys;
using AiGateway.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Web.Endpoints;

public class AiProxyEndpoints : IEndpointGroup
{
    public static string? RoutePrefix => "/v1";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost("/chat/completions", CreateChatCompletion);
        groupBuilder.MapGet("/models", GetModels);
    }

    public static async Task<Results<Ok<ChatCompletionResponse>, UnauthorizedHttpResult, ProblemHttpResult>> CreateChatCompletion(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromHeader(Name = "x-api-key")] string? xApiKey,
        [FromBody] ChatCompletionRequest request,
        IVirtualKeyService virtualKeyService,
        IRateLimitService rateLimitService,
        IIntelligentRouter router,
        IAiProviderAdapterFactory adapterFactory,
        IUsageMeteringChannel meteringChannel,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // 1. Extract raw virtual key from Authorization or x-api-key header
        var rawKey = ExtractVirtualKey(authorization, xApiKey);
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return TypedResults.Unauthorized();
        }

        // 2. Validate Virtual Key
        var virtualKey = await virtualKeyService.ValidateKeyAsync(rawKey, cancellationToken);
        if (virtualKey == null)
        {
            return TypedResults.Unauthorized();
        }

        // 3. Rate Limit Check (RPM & TPM)
        var estimatedTokens = request.MaxTokens ?? 500;
        var rateLimitResult = await rateLimitService.CheckAndRecordAsync(
            virtualKey.Id,
            virtualKey.RateLimitRpm,
            virtualKey.RateLimitTpm,
            estimatedTokens,
            cancellationToken);

        if (!rateLimitResult.IsAllowed)
        {
            return TypedResults.Problem(
                detail: rateLimitResult.Reason,
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Rate Limit Exceeded");
        }

        // 4. Resolve Target Provider, Model & API Key via Intelligent Router
        RouteExecutionTarget target;
        try
        {
            target = await router.ResolveTargetAsync(request.Model, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "No Available AI Provider");
        }

        // 5. Execute Provider Adapter
        var sw = Stopwatch.StartNew();
        try
        {
            var adapter = adapterFactory.GetAdapter(target.Provider.ProviderType);
            var response = await adapter.ExecuteChatCompletionAsync(
                target.Provider,
                target.Model,
                target.DecryptedApiKey,
                request,
                cancellationToken);

            sw.Stop();

            // 6. Queue Async Usage Metering Log (Non-blocking)
            var promptTokens = response.Usage?.PromptTokens ?? 0;
            var completionTokens = response.Usage?.CompletionTokens ?? 0;
            var costUsd = (promptTokens * target.Model.PromptTokenCostPer1K / 1000m)
                          + (completionTokens * target.Model.CompletionTokenCostPer1K / 1000m);

            await meteringChannel.QueueUsageLogAsync(new UsageLogItem(
                VirtualKeyId: virtualKey.Id,
                ProviderId: target.Provider.Id,
                ModelId: target.Model.Id,
                RequestedModelAlias: request.Model,
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                CalculatedCostUsd: costUsd,
                LatencyMs: sw.ElapsedMilliseconds,
                HttpStatusCode: StatusCodes.Status200OK,
                IsSuccess: true,
                ErrorMessage: null,
                ClientIp: httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
                RequestedAt: DateTimeOffset.UtcNow
            ), cancellationToken);

            return TypedResults.Ok(response);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            var statusCode = (int)(ex.StatusCode ?? System.Net.HttpStatusCode.InternalServerError);

            // Handle Failover / Cooldown for the failed key
            await router.HandleProviderFailureAsync(target.ApiKey.Id, statusCode, cancellationToken);

            // Queue failure log asynchronously
            await meteringChannel.QueueUsageLogAsync(new UsageLogItem(
                VirtualKeyId: virtualKey.Id,
                ProviderId: target.Provider.Id,
                ModelId: target.Model.Id,
                RequestedModelAlias: request.Model,
                PromptTokens: 0,
                CompletionTokens: 0,
                CalculatedCostUsd: 0,
                LatencyMs: sw.ElapsedMilliseconds,
                HttpStatusCode: statusCode,
                IsSuccess: false,
                ErrorMessage: ex.Message,
                ClientIp: httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
                RequestedAt: DateTimeOffset.UtcNow
            ), cancellationToken);

            return TypedResults.Problem(
                detail: $"Upstream AI Provider Error: {ex.Message}",
                statusCode: statusCode,
                title: "Provider Execution Failure");
        }
    }

    public static async Task<Ok<OpenAiModelsListResponse>> GetModels(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var activeModels = await context.AiModels
            .Where(m => m.IsActive && m.Provider.IsActive)
            .Select(m => m.Alias)
            .Distinct()
            .ToListAsync(cancellationToken);

        var modelItems = activeModels.Select(alias => new OpenAiModelItem
        {
            Id = alias,
            Object = "model",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            OwnedBy = "AiGateway"
        }).ToList();

        return TypedResults.Ok(new OpenAiModelsListResponse
        {
            Object = "list",
            Data = modelItems
        });
    }

    private static string? ExtractVirtualKey(string? authorization, string? xApiKey)
    {
        if (!string.IsNullOrWhiteSpace(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(xApiKey))
        {
            return xApiKey.Trim();
        }

        return null;
    }

    public class OpenAiModelsListResponse
    {
        [JsonPropertyName("object")]
        public string Object { get; set; } = "list";

        [JsonPropertyName("data")]
        public List<OpenAiModelItem> Data { get; set; } = new();
    }

    public class OpenAiModelItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("object")]
        public string Object { get; set; } = "model";

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("owned_by")]
        public string OwnedBy { get; set; } = "AiGateway";
    }
}
