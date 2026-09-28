using System.Text.Json.Serialization;

namespace AiGateway.Application.Common.Models.AiProxy;

public class ChatMessageDto
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public object? Content { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
