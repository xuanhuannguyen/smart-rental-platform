namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IChatPresenceTracker
{
    void JoinConversation(Guid conversationId, Guid userId, string connectionId);
    void LeaveConversation(Guid conversationId, Guid userId, string connectionId);
    void RemoveConnection(string connectionId);
    bool IsUserViewingConversation(Guid conversationId, Guid userId);
    IReadOnlyCollection<string> GetConversationConnectionIds(Guid conversationId, Guid userId);
}
