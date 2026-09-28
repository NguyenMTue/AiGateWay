using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Constants;
using AiGateway.Domain.Entities;
using AiGateway.Domain.Enums;
using AiGateway.Infrastructure.Identity;
using AiGateway.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiGateway.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
    }
}

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IEncryptionService _encryptionService;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IEncryptionService encryptionService)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _encryptionService = encryptionService;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            if (_context.Database.IsNpgsql())
            {
                await _context.Database.MigrateAsync();
            }
            else
            {
                await _context.Database.EnsureCreatedAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        // 1. Default roles
        var administratorRole = new IdentityRole(Roles.Administrator);

        if (_roleManager.Roles.All(r => r.Name != administratorRole.Name))
        {
            await _roleManager.CreateAsync(administratorRole);
        }

        // 2. Default users
        var administrator = new ApplicationUser { UserName = "administrator@localhost", Email = "administrator@localhost" };

        if (_userManager.Users.All(u => u.UserName != administrator.UserName))
        {
            await _userManager.CreateAsync(administrator, "Administrator1!");
            if (!string.IsNullOrWhiteSpace(administratorRole.Name))
            {
                await _userManager.AddToRolesAsync(administrator, new[] { administratorRole.Name });
            }
        }

        // 3. Seed AI Providers & Models & API Keys
        if (!await _context.AiProviders.AnyAsync())
        {
            var openAiProvider = new AiProvider
            {
                Name = "OpenAI Official",
                Slug = "openai",
                ProviderType = ProviderType.OpenAI,
                BaseUrl = "https://api.openai.com/v1",
                IsActive = true,
                TimeoutSeconds = 60,
                MaxRetries = 3,
                Models = new List<AiModel>
                {
                    new()
                    {
                        Name = "GPT-4o",
                        ModelId = "gpt-4o",
                        Alias = "gpt-4o",
                        ModelType = ModelType.ChatCompletion,
                        PromptTokenCostPer1K = 0.0025m,
                        CompletionTokenCostPer1K = 0.0100m,
                        ContextWindowTokens = 128000,
                        MaxOutputTokens = 4096,
                        SupportsStreaming = true,
                        SupportsVision = true,
                        SupportsFunctionCalling = true
                    },
                    new()
                    {
                        Name = "GPT-4o Mini",
                        ModelId = "gpt-4o-mini",
                        Alias = "gpt-4o-mini",
                        ModelType = ModelType.ChatCompletion,
                        PromptTokenCostPer1K = 0.00015m,
                        CompletionTokenCostPer1K = 0.00060m,
                        ContextWindowTokens = 128000,
                        MaxOutputTokens = 4096,
                        SupportsStreaming = true,
                        SupportsVision = true,
                        SupportsFunctionCalling = true
                    }
                },
                ApiKeys = new List<ProviderApiKey>
                {
                    new()
                    {
                        Name = "OpenAI Primary Key",
                        EncryptedApiKey = _encryptionService.Encrypt("sk-proj-dummy-openai-key-for-dev"),
                        KeyMask = "sk-proj-...dev1",
                        Weight = 1,
                        Priority = 1,
                        IsActive = true
                    }
                }
            };

            var geminiProvider = new AiProvider
            {
                Name = "Google Gemini",
                Slug = "google-gemini",
                ProviderType = ProviderType.GoogleGemini,
                BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
                IsActive = true,
                TimeoutSeconds = 60,
                MaxRetries = 3,
                Models = new List<AiModel>
                {
                    new()
                    {
                        Name = "Gemini 1.5 Pro",
                        ModelId = "gemini-1.5-pro",
                        Alias = "gemini-1.5-pro",
                        ModelType = ModelType.ChatCompletion,
                        PromptTokenCostPer1K = 0.00125m,
                        CompletionTokenCostPer1K = 0.00500m,
                        ContextWindowTokens = 2000000,
                        MaxOutputTokens = 8192,
                        SupportsStreaming = true,
                        SupportsVision = true,
                        SupportsFunctionCalling = true
                    },
                    new()
                    {
                        Name = "Gemini 1.5 Flash",
                        ModelId = "gemini-1.5-flash",
                        Alias = "gemini-1.5-flash",
                        ModelType = ModelType.ChatCompletion,
                        PromptTokenCostPer1K = 0.000075m,
                        CompletionTokenCostPer1K = 0.000300m,
                        ContextWindowTokens = 1000000,
                        MaxOutputTokens = 8192,
                        SupportsStreaming = true,
                        SupportsVision = true,
                        SupportsFunctionCalling = true
                    }
                },
                ApiKeys = new List<ProviderApiKey>
                {
                    new()
                    {
                        Name = "Gemini Primary Key",
                        EncryptedApiKey = _encryptionService.Encrypt("AIzaSyDummyGeminiKeyForDev"),
                        KeyMask = "AIza...dev1",
                        Weight = 1,
                        Priority = 1,
                        IsActive = true
                    }
                }
            };

            _context.AiProviders.AddRange(openAiProvider, geminiProvider);
            await _context.SaveChangesAsync();
        }

        // 4. Seed Route Rules
        if (!await _context.RouteRules.AnyAsync())
        {
            var gpt4o = await _context.AiModels.FirstOrDefaultAsync(m => m.Alias == "gpt-4o");
            var geminiPro = await _context.AiModels.FirstOrDefaultAsync(m => m.Alias == "gemini-1.5-pro");
            var gpt4oMini = await _context.AiModels.FirstOrDefaultAsync(m => m.Alias == "gpt-4o-mini");
            var geminiFlash = await _context.AiModels.FirstOrDefaultAsync(m => m.Alias == "gemini-1.5-flash");

            if (gpt4o != null && geminiPro != null)
            {
                _context.RouteRules.Add(new RouteRule
                {
                    Name = "Smart Model Routing Rule",
                    TargetModelAlias = "smart-model",
                    RoutingStrategy = RoutingStrategy.Priority,
                    PrimaryModelId = gpt4o.Id,
                    FallbackModelId = geminiPro.Id,
                    Priority = 1,
                    IsActive = true
                });
            }

            if (gpt4oMini != null && geminiFlash != null)
            {
                _context.RouteRules.Add(new RouteRule
                {
                    Name = "Cheap Model Routing Rule",
                    TargetModelAlias = "cheap-model",
                    RoutingStrategy = RoutingStrategy.Priority,
                    PrimaryModelId = gpt4oMini.Id,
                    FallbackModelId = geminiFlash.Id,
                    Priority = 1,
                    IsActive = true
                });
            }

            await _context.SaveChangesAsync();
        }

        // 5. Seed Dev Virtual Key for Testing
        if (!await _context.VirtualKeys.AnyAsync())
        {
            var devRawKey = "gw-live-devtestkey1234567890abcdef";
            var keyHash = VirtualKeyService.HashKey(devRawKey);

            _context.VirtualKeys.Add(new VirtualKey
            {
                Name = "Development Local Test Key",
                KeyHash = keyHash,
                KeyPrefix = "gw-live-",
                KeyMask = "gw-live-...cdef",
                RateLimitRpm = 60,
                RateLimitTpm = 100000,
                MaxBudgetUsd = 100.00m,
                CurrentUsageUsd = 0.00m,
                IsActive = true
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("=========================================================================");
            _logger.LogInformation("CREATED SAMPLE DEV VIRTUAL KEY FOR TESTING:");
            _logger.LogInformation("  Raw Virtual Key: {RawKey}", devRawKey);
            _logger.LogInformation("  Header Usage:    Authorization: Bearer {RawKey}", devRawKey);
            _logger.LogInformation("=========================================================================");
        }
    }
}
