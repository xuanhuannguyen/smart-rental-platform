using SmartRentalPlatform.Contracts.Chat.Requests;
using SmartRentalPlatform.Contracts.Chat.Responses;

namespace SmartRentalPlatform.Application.Chat;

public interface IChatService
{
    Task<List<ConversationResponse>> GetConversationsAsync(Guid currentUserId, CancellationToken cancellationToken = default);
    Task<ConversationResponse> CreateDirectConversationAsync(Guid currentUserId, CreateDirectConversationRequest request, CancellationToken cancellationToken = default);
    Task<ConversationResponse> CreateGroupConversationAsync(Guid currentUserId, CreateGroupConversationRequest request, CancellationToken cancellationToken = default);
    Task<ConversationResponse> UpdateConversationAsync(Guid currentUserId, Guid conversationId, UpdateConversationRequest request, CancellationToken cancellationToken = default);
    Task<List<ChatUserResponse>> GetLandlordQuickContactsAsync(Guid landlordUserId, CancellationToken cancellationToken = default);
    Task<List<ChatUserResponse>> SearchUsersByEmailAsync(Guid currentUserId, string email, CancellationToken cancellationToken = default);
    Task<ConversationResponse> AddParticipantsAsync(Guid currentUserId, Guid conversationId, AddConversationParticipantsRequest request, CancellationToken cancellationToken = default);
    Task<ConversationResponse> RemoveParticipantAsync(Guid currentUserId, Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
    Task<ConversationResponse> LeaveConversationAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<ConversationResponse> CloseConversationAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<List<ChatMessageResponse>> GetMessagesAsync(Guid currentUserId, Guid conversationId, DateTimeOffset? before, int limit = 30, CancellationToken cancellationToken = default);
    Task<SendChatMessageResponse> SendMessageAsync(Guid currentUserId, Guid conversationId, SendChatMessageRequest request, CancellationToken cancellationToken = default);
    Task<ConversationResponse> MarkAsReadAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default);
    Task EnsureCanJoinConversationAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default);
}
