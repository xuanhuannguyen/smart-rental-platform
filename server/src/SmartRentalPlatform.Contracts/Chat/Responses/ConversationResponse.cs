namespace SmartRentalPlatform.Contracts.Chat.Responses;

public sealed class ConversationResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public Guid? RoomingHouseId { get; set; }
    public string? RoomingHouseName { get; set; }
    public string? RoomingHouseAddress { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
    public int UnreadCount { get; set; }
    public bool IsClosed { get; set; }
    public bool IsCurrentUserOwner { get; set; }
    public bool HasCurrentUserLeft { get; set; }
    public bool RequiresJoinApproval { get; set; }
    public bool IsCurrentUserAdmin { get; set; }
    public bool CanManageMembers { get; set; }
    public string InboxStatus { get; set; } = "Main";
    public string? AvatarUrl { get; set; }
    public List<ChatParticipantResponse> Participants { get; set; } = new();
}

