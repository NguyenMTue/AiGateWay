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

    public static async Task<IResult> CreateChatCompletion(
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

        // 4. Resolve & Execute Target Provider with Automatic Failover
        var failedKeyIds = new HashSet<int>();
        const int maxFailoverAttempts = 5;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxFailoverAttempts; attempt++)
        {
            RouteExecutionTarget target;
            try
            {
                target = await router.ResolveTargetAsync(request.Model, cancellationToken, failedKeyIds);
            }
            catch (InvalidOperationException ex)
            {
                if (lastException != null)
                {
                    return TypedResults.Problem(
                        detail: $"All available AI providers failed. Last provider error: {lastException.Message}",
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "All Providers Unavailable");
                }

                return TypedResults.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "No Available AI Provider");
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var adapter = adapterFactory.GetAdapter(target.Provider.ProviderType);

                if (request.Stream)
                {
                    httpContext.Response.ContentType = "text/event-stream";
                    httpContext.Response.Headers.CacheControl = "no-cache";
                    httpContext.Response.Headers.Connection = "keep-alive";

                    var stream = adapter.ExecuteChatCompletionStreamAsync(
                        target.Provider,
                        target.Model,
                        target.DecryptedApiKey,
                        request,
                        cancellationToken);

                    var streamedChunkCount = 0;
                    await foreach (var line in stream.WithCancellation(cancellationToken))
                    {
                        var sseChunk = line.StartsWith("data:") ? line : $"data: {line}";
                        await httpContext.Response.WriteAsync($"{sseChunk}\n\n", cancellationToken);
                        await httpContext.Response.Body.FlushAsync(cancellationToken);
                        streamedChunkCount++;
                    }

                    sw.Stop();

                    // Queue Async Usage Metering Log
                    var promptTokensStream = estimatedTokens;
                    var completionTokensStream = Math.Max(1, streamedChunkCount);
                    var costUsdStream = (promptTokensStream * target.Model.PromptTokenCostPer1K / 1000m)
                                        + (completionTokensStream * target.Model.CompletionTokenCostPer1K / 1000m);

                    await meteringChannel.QueueUsageLogAsync(new UsageLogItem(
                        VirtualKeyId: virtualKey.Id,
                        ProviderId: target.Provider.Id,
                        ModelId: target.Model.Id,
                        RequestedModelAlias: request.Model,
                        PromptTokens: promptTokensStream,
                        CompletionTokens: completionTokensStream,
                        CalculatedCostUsd: costUsdStream,
                        LatencyMs: sw.ElapsedMilliseconds,
                        HttpStatusCode: StatusCodes.Status200OK,
                        IsSuccess: true,
                        ErrorMessage: null,
                        ClientIp: httpContext.Connection.RemoteIpAddress?.ToString(),
                        UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
                        RequestedAt: DateTimeOffset.UtcNow
                    ), cancellationToken);

                    return TypedResults.Empty;
                }

                var response = await adapter.ExecuteChatCompletionAsync(
                    target.Provider,
                    target.Model,
                    target.DecryptedApiKey,
                    request,
                    cancellationToken);

                sw.Stop();

                // Queue Async Usage Metering Log (Non-blocking)
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
                lastException = ex;

                // Handle Failover / Cooldown for the failed key
                await router.HandleProviderFailureAsync(target.ApiKey.Id, statusCode, cancellationToken);
                failedKeyIds.Add(target.ApiKey.Id);

                // Queue failure log asynchronously for this attempt
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

                // Failover loop will continue to try the next available provider/key (e.g. FallbackModel)
            }
            catch (Exception ex)
            {
                sw.Stop();
                var statusCode = StatusCodes.Status500InternalServerError;
                lastException = ex;

                await router.HandleProviderFailureAsync(target.ApiKey.Id, statusCode, cancellationToken);
                failedKeyIds.Add(target.ApiKey.Id);

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

                // Failover loop will continue to try the next available provider/key
            }
        }

        return TypedResults.Problem(
            detail: $"Exceeded maximum failover attempts ({maxFailoverAttempts}). Last error: {lastException?.Message}",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Provider Execution Failure");
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
