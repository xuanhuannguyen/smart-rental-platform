namespace SmartRentalPlatform.Contracts.Chat.Responses;

public sealed class ChatCountsResponse
{
    public int MainUnreadCount { get; set; }
    public int PendingCount { get; set; }
}
