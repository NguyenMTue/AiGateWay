using AiGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiGateway.Infrastructure.Data.Configurations;

public class RouteRuleConfiguration : IEntityTypeConfiguration<RouteRule>
{
    public void Configure(EntityTypeBuilder<RouteRule> builder)
    {
        builder.ToTable("RouteRules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.TargetModelAlias)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(r => r.TargetModelAlias);

        builder.HasOne(r => r.PrimaryModel)
            .WithMany()
            .HasForeignKey(r => r.PrimaryModelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.FallbackModel)
            .WithMany()
            .HasForeignKey(r => r.FallbackModelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
