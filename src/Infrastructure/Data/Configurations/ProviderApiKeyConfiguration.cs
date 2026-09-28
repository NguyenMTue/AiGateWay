using AiGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiGateway.Infrastructure.Data.Configurations;

public class ProviderApiKeyConfiguration : IEntityTypeConfiguration<ProviderApiKey>
{
    public void Configure(EntityTypeBuilder<ProviderApiKey> builder)
    {
        builder.ToTable("ProviderApiKeys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Name)
            .HasMaxLength(100)
            .IsRequired();

        // Encrypted string for provider API key storage
        builder.Property(k => k.EncryptedApiKey)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(k => k.KeyMask)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(k => k.Weight)
            .HasDefaultValue(1);

        builder.Property(k => k.Priority)
            .HasDefaultValue(1);
    }
}
