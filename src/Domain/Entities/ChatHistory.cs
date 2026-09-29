using AiGateway.Domain.Common;

namespace AiGateway.Domain.Entities;

public class ChatHistory : BaseEntity
{
    public string? UserId { get; set; }

    public int? VirtualKeyId { get; set; }

    public VirtualKey? VirtualKey { get; set; }

    public string ConversationId { get; set; } = string.Empty;

    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    public string? ModelAlias { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
