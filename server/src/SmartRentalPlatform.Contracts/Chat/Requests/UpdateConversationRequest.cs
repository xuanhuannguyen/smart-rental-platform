namespace SmartRentalPlatform.Contracts.Chat.Requests;

public sealed class UpdateConversationRequest
{
    public string? Title { get; set; }
    public string? AvatarUrl { get; set; }
    public Guid? AvatarMediaAssetId { get; set; }
    public bool ClearAvatar { get; set; }
}
