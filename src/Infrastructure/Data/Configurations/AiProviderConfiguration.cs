using AiGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiGateway.Infrastructure.Data.Configurations;

public class AiProviderConfiguration : IEntityTypeConfiguration<AiProvider>
{
    public void Configure(EntityTypeBuilder<AiProvider> builder)
    {
        builder.ToTable("AiProviders");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Slug)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(p => p.Slug)
            .IsUnique();

        builder.Property(p => p.BaseUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.TimeoutSeconds)
            .HasDefaultValue(60);

        builder.Property(p => p.MaxRetries)
            .HasDefaultValue(3);

        builder.HasMany(p => p.Models)
            .WithOne(m => m.Provider)
            .HasForeignKey(m => m.AiProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ApiKeys)
            .WithOne(k => k.Provider)
            .HasForeignKey(k => k.AiProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
