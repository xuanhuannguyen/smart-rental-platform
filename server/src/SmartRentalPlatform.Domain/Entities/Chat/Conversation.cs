using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Chat;

namespace SmartRentalPlatform.Domain.Entities.Chat;

public class Conversation
{
    public Guid Id { get; set; }
    public ConversationType Type { get; set; }
    public string? Title { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? DirectUserAId { get; set; }
    public Guid? DirectUserBId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsClosed { get; set; }

    public Room? Room { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
