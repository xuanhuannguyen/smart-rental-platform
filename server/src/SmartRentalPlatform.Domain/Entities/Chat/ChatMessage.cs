using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.Chat;

namespace SmartRentalPlatform.Domain.Entities.Chat;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public Guid? MediaAssetId { get; set; }
    public ChatMessageType MessageType { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileContentType { get; set; }
    public long? FileSize { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public MediaAsset? MediaAsset { get; set; }
}
