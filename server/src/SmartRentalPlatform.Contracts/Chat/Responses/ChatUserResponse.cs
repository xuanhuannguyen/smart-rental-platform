namespace SmartRentalPlatform.Contracts.Chat.Responses;

public sealed class ChatUserResponse
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public List<string> Roles { get; set; } = new();
    public string? ContextLabel { get; set; }
}
