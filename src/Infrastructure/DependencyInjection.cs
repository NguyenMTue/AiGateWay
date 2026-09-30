using AiGateway.Application.Common.Interfaces;
using AiGateway.Infrastructure.AiProviders;
using AiGateway.Infrastructure.Data;
using AiGateway.Infrastructure.Data.Interceptors;
using AiGateway.Infrastructure.Identity;
using AiGateway.Infrastructure.Metering;
using AiGateway.Infrastructure.Routing;
using AiGateway.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);
        Guard.Against.Null(connectionString, message: $"Connection string '{Services.Database}' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.EnrichNpgsqlDbContext<ApplicationDbContext>();

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddAuthentication()
            .AddBearerToken(IdentityConstants.BearerScheme);

        builder.Services.AddAuthorizationBuilder();

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddApiEndpoints();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();

        // Encryption Service
        builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

        // Memory Cache for Rate Limiting Fallback
        builder.Services.AddMemoryCache();

        // Distributed Cache for Distributed Rate Limiting (Redis or Memory Fallback)
        var redisConnectionString = builder.Configuration.GetConnectionString("redis") 
            ?? builder.Configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "AiGateway:";
            });
        }
        else
        {
            builder.Services.AddDistributedMemoryCache();
        }

        // Virtual Key, Rate Limiting & Circuit Breaker Services
        builder.Services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();
        builder.Services.AddScoped<IVirtualKeyService, VirtualKeyService>();
        builder.Services.AddSingleton<IRateLimitService, RateLimitService>();

        // Async Usage Metering Channel & Background Worker
        builder.Services.AddSingleton<UsageMeteringChannel>();
        builder.Services.AddSingleton<IUsageMeteringChannel>(sp => sp.GetRequiredService<UsageMeteringChannel>());
        builder.Services.AddHostedService<UsageMeteringBackgroundService>();

        // HTTP Client & Provider Adapters
        builder.Services.AddHttpClient();
        builder.Services.AddTransient<IAiProviderAdapter, OpenAiProviderAdapter>();
        builder.Services.AddTransient<IAiProviderAdapter, GeminiProviderAdapter>();
        builder.Services.AddTransient<IAiProviderAdapter, AnthropicProviderAdapter>();
        builder.Services.AddScoped<IAiProviderAdapterFactory, ProviderAdapterFactory>();

        // Intelligent Router
        builder.Services.AddScoped<IIntelligentRouter, IntelligentRouter>();
    }
}
