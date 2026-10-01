using AiGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiGateway.Infrastructure.Data.Configurations;

public class ChatHistoryConfiguration : IEntityTypeConfiguration<ChatHistory>
{
    public void Configure(EntityTypeBuilder<ChatHistory> builder)
    {
        builder.ToTable("ChatHistories");

        builder.HasKey(ch => ch.Id);

        builder.Property(ch => ch.ConversationId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(ch => ch.Role)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ch => ch.Content)
            .IsRequired();

        builder.Property(ch => ch.ModelAlias)
            .HasMaxLength(100);

        builder.HasIndex(ch => ch.ConversationId);
        builder.HasIndex(ch => ch.UserId);

        builder.HasOne(ch => ch.VirtualKey)
            .WithMany()
            .HasForeignKey(ch => ch.VirtualKeyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
