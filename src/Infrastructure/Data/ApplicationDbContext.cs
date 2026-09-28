using System.Reflection;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Entities;
using AiGateway.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<AiProvider> AiProviders => Set<AiProvider>();

    public DbSet<AiModel> AiModels => Set<AiModel>();

    public DbSet<ProviderApiKey> ProviderApiKeys => Set<ProviderApiKey>();

    public DbSet<VirtualKey> VirtualKeys => Set<VirtualKey>();

    public DbSet<RouteRule> RouteRules => Set<RouteRule>();

    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
