using AiGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiGateway.Infrastructure.Data.Configurations;

public class RequestLogConfiguration : IEntityTypeConfiguration<RequestLog>
{
    public void Configure(EntityTypeBuilder<RequestLog> builder)
    {
        builder.ToTable("RequestLogs");

        builder.HasKey(rl => rl.Id);

        builder.Property(rl => rl.RequestedModelAlias)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(rl => rl.CalculatedCostUsd)
            .HasPrecision(18, 6);

        builder.Property(rl => rl.ClientIp)
            .HasMaxLength(50);

        builder.Property(rl => rl.UserAgent)
            .HasMaxLength(500);

        builder.HasIndex(rl => rl.RequestedAt);

        builder.HasIndex(rl => rl.VirtualKeyId);

        builder.HasOne(rl => rl.AiProvider)
            .WithMany()
            .HasForeignKey(rl => rl.AiProviderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(rl => rl.AiModel)
            .WithMany()
            .HasForeignKey(rl => rl.AiModelId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
