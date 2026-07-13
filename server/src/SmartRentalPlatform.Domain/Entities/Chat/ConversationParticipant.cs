using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Chat;

namespace SmartRentalPlatform.Domain.Entities.Chat;

public class ConversationParticipant
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public ConversationParticipantRole Role { get; set; }
    public ConversationParticipantSource Source { get; set; }
    public Guid? AddedByUserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
    public DateTimeOffset? LastReadAt { get; set; }
    public int UnreadCount { get; set; }
    public bool IsMuted { get; set; }
    public ConversationParticipantInboxStatus InboxStatus { get; set; } = ConversationParticipantInboxStatus.Main;
    public DateTimeOffset? InboxStatusUpdatedAt { get; set; }
    public Guid? InboxStatusUpdatedByUserId { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? AddedByUser { get; set; }
    public User? InboxStatusUpdatedByUser { get; set; }
}
