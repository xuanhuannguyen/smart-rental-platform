using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Chat;

namespace SmartRentalPlatform.Domain.Entities.Chat;

public class ConversationJoinRequest
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid RequesterUserId { get; set; }
    public ConversationJoinRequestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public User RequesterUser { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
}
