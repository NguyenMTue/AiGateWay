using AiGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiGateway.Infrastructure.Data.Configurations;

public class VirtualKeyConfiguration : IEntityTypeConfiguration<VirtualKey>
{
    public void Configure(EntityTypeBuilder<VirtualKey> builder)
    {
        builder.ToTable("VirtualKeys");

        builder.HasKey(vk => vk.Id);

        builder.Property(vk => vk.Name)
            .HasMaxLength(100)
            .IsRequired();

        // SHA-256 hash of the virtual key for secure fast lookup
        builder.Property(vk => vk.KeyHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(vk => vk.KeyHash)
            .IsUnique();

        builder.Property(vk => vk.KeyPrefix)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(vk => vk.KeyMask)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(vk => vk.MaxBudgetUsd)
            .HasPrecision(18, 4);

        builder.Property(vk => vk.CurrentUsageUsd)
            .HasPrecision(18, 4);

        builder.HasMany(vk => vk.RequestLogs)
            .WithOne(rl => rl.VirtualKey)
            .HasForeignKey(rl => rl.VirtualKeyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
