namespace SmartRentalPlatform.Api.Hubs;

public static class ChatHubGroups
{
    public static string User(Guid userId) => $"user:{userId}";
    public static string Conversation(Guid conversationId) => $"conversation:{conversationId}";
}
