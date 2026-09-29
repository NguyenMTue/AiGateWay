namespace AiGateway.Application.Conversations;

public class ChatHistoryDto
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public int? VirtualKeyId { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ModelAlias { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
