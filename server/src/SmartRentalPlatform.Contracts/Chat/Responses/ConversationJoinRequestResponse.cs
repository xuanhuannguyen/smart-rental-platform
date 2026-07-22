namespace SmartRentalPlatform.Contracts.Chat.Responses;

public sealed class ConversationJoinRequestResponse
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid RequesterUserId { get; set; }
    public string RequesterDisplayName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string? RequesterAvatarUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedByDisplayName { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}
