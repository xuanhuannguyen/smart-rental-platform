using SmartRentalPlatform.Contracts.Chat.Requests;
using SmartRentalPlatform.Contracts.Chat.Responses;
using SmartRentalPlatform.Domain.Enums.Chat;

namespace SmartRentalPlatform.Application.Chat;

public interface IChatService
{
    Task<List<ConversationResponse>> GetConversationsAsync(Guid currentUserId, string? box = null, CancellationToken cancellationToken = default);
    Task<List<ConversationResponse>> GetRecentConversationsAsync(Guid currentUserId, string? box, int take, int skip, CancellationToken cancellationToken = default);
    Task<ChatCountsResponse> GetConversationCountsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ConversationResponse> GetConversationResponseForUserAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);

    Task<ConversationResponse> CreateDirectConversationAsync(Guid currentUserId, CreateDirectConversationRequest request, CancellationToken cancellationToken = default);
    Task<ConversationResponse> CreateDirectConversationByRoomingHouseAsync(Guid currentUserId, Guid roomingHouseId, CancellationToken cancellationToken = default);
    Task<ConversationResponse> ContactLandlordAsync(Guid tenantUserId, Guid roomingHouseId, string initialMessage, CancellationToken cancellationToken = default);
    Task<ConversationResponse> CreateGroupConversationAsync(Guid currentUserId, CreateGroupConversationRequest request, CancellationToken cancellationToken = default);
    Task<ConversationResponse> UpdateConversationAsync(Guid currentUserId, Guid conversationId, UpdateConversationRequest request, CancellationToken cancellationToken = default);

    Task<List<ChatUserResponse>> GetLandlordQuickContactsAsync(Guid landlordUserId, CancellationToken cancellationToken = default);
    Task<List<ChatUserResponse>> SearchUsersByEmailAsync(Guid currentUserId, string email, Guid? roomingHouseId = null, CancellationToken cancellationToken = default);
    Task<List<ChatUserResponse>> GetActiveTenantsByRoomingHouseAsync(Guid landlordUserId, Guid roomingHouseId, CancellationToken cancellationToken = default);
    Task<List<ChatUserResponse>> GetEligibleMembersAsync(Guid currentUserId, Guid conversationId, Guid? roomingHouseId, CancellationToken cancellationToken = default);
    Task<List<ChatRoomingHouseFilterResponse>> GetFilterRoomingHousesAsync(Guid currentUserId, CancellationToken cancellationToken = default);

    Task<ConversationResponse> AddParticipantsAsync(Guid currentUserId, Guid conversationId, AddConversationParticipantsRequest request, CancellationToken cancellationToken = default);
    Task<ConversationResponse> RemoveParticipantAsync(Guid currentUserId, Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
    Task<ConversationResponse> UpdateParticipantRoleAsync(Guid currentUserId, Guid conversationId, Guid targetUserId, ConversationParticipantRole role, CancellationToken cancellationToken = default);
    Task<ConversationResponse> LeaveConversationAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<ConversationResponse> CloseConversationAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<ConversationResponse> ClearConversationHistoryAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default);

    Task<List<ConversationJoinRequestResponse>> GetJoinRequestsAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<ConversationResponse> CreateJoinRequestAsync(Guid currentUserId, Guid conversationId, Guid? targetUserId = null, CancellationToken cancellationToken = default);
    Task<ConversationResponse> ApproveJoinRequestAsync(Guid currentUserId, Guid conversationId, Guid requestId, CancellationToken cancellationToken = default);
    Task RejectJoinRequestAsync(Guid currentUserId, Guid conversationId, Guid requestId, CancellationToken cancellationToken = default);
    Task<ConversationResponse> UpdateApprovalSettingsAsync(Guid currentUserId, Guid conversationId, bool requiresApproval, CancellationToken cancellationToken = default);

    Task<(ConversationResponse Conversation, ChatMessageResponse? SystemMessage)> AcceptContactRequestAsync(Guid landlordUserId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<ConversationResponse> RejectContactRequestAsync(Guid landlordUserId, Guid conversationId, CancellationToken cancellationToken = default);

    Task<List<ChatMessageResponse>> GetMessagesAsync(Guid currentUserId, Guid conversationId, DateTimeOffset? before, int limit = 30, CancellationToken cancellationToken = default);
    Task<SendChatMessageResponse> SendMessageAsync(Guid currentUserId, Guid conversationId, SendChatMessageRequest request, CancellationToken cancellationToken = default);
    Task<ConversationResponse> MarkAsReadAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<SendChatMessageResponse> DeleteMessageAsync(Guid currentUserId, Guid conversationId, Guid messageId, CancellationToken cancellationToken = default);
    Task<ChatMessageResponse> UnsendMessageAsync(Guid currentUserId, Guid conversationId, Guid messageId, CancellationToken cancellationToken = default);
    Task<ChatMessageResponse> GetFileMessageAsync(Guid currentUserId, Guid conversationId, Guid messageId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadMessageCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task EnsureCanJoinConversationAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default);
}
