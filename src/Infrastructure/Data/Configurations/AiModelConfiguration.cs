using AiGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiGateway.Infrastructure.Data.Configurations;

public class AiModelConfiguration : IEntityTypeConfiguration<AiModel>
{
    public void Configure(EntityTypeBuilder<AiModel> builder)
    {
        builder.ToTable("AiModels");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.ModelId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.Alias)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(m => m.Alias);

        builder.Property(m => m.PromptTokenCostPer1K)
            .HasPrecision(18, 6);

        builder.Property(m => m.CompletionTokenCostPer1K)
            .HasPrecision(18, 6);
    }
}
