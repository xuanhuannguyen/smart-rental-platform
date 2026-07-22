using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Api.Hubs;
using SmartRentalPlatform.Application.Chat;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Contracts.Chat.Requests;
using SmartRentalPlatform.Contracts.Chat.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Files;
using SmartRentalPlatform.Domain.Enums.Chat;

namespace SmartRentalPlatform.Api.Controllers.Chat;

[ApiController]
[Authorize]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private const long MaxImageBytes = 5 * 1024 * 1024;

    private readonly IChatService chatService;
    private readonly ICurrentUserService currentUserService;
    private readonly IFileStorageService fileStorageService;
    private readonly IChatPresenceTracker presenceTracker;
    private readonly IHubContext<ChatHub> hubContext;

    public ChatController(
        IChatService chatService,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        IChatPresenceTracker presenceTracker,
        IHubContext<ChatHub> hubContext)
    {
        this.chatService = chatService;
        this.currentUserService = currentUserService;
        this.fileStorageService = fileStorageService;
        this.presenceTracker = presenceTracker;
        this.hubContext = hubContext;
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<ApiResponse<List<ConversationResponse>>>> GetConversations([FromQuery] string? box, CancellationToken cancellationToken)
    {
        var result = await chatService.GetConversationsAsync(GetCurrentUserId(), box, cancellationToken);
        return Ok(Success(result, "Tải danh sách tin nhắn thành công."));
    }

    [HttpGet("conversations/recent")]
    public async Task<ActionResult<ApiResponse<List<ConversationResponse>>>> GetRecentConversations(
        [FromQuery] string? box,
        [FromQuery] int take = 5,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        var result = await chatService.GetRecentConversationsAsync(GetCurrentUserId(), box, take, skip, cancellationToken);
        return Ok(Success(result, "Tải cuộc trò chuyện gần đây thành công."));
    }

    [HttpPost("conversations/direct")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> CreateDirect(CreateDirectConversationRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.CreateDirectConversationAsync(GetCurrentUserId(), request, cancellationToken);
        return Ok(Success(result, "Tạo cuộc trò chuyện thành công."));
    }

    [HttpPost("conversations/groups")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> CreateGroup(CreateGroupConversationRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.CreateGroupConversationAsync(GetCurrentUserId(), request, cancellationToken);
        await BroadcastConversationAsync(result);
        return Ok(Success(result, "Tạo nhóm chat thành công."));
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> GetConversation(Guid id, CancellationToken cancellationToken)
    {
        var result = await chatService.GetConversationResponseForUserAsync(id, GetCurrentUserId(), cancellationToken);
        return Ok(Success(result, "Tải thông tin hội thoại thành công."));
    }

    [HttpPatch("conversations/{id:guid}")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> UpdateConversation(Guid id, UpdateConversationRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.UpdateConversationAsync(GetCurrentUserId(), id, request, cancellationToken);
        await BroadcastConversationAsync(result);
        return Ok(Success(result, "Cập nhật nhóm chat thành công."));
    }

    [HttpGet("landlord/quick-contacts")]
    public async Task<ActionResult<ApiResponse<List<ChatUserResponse>>>> GetQuickContacts(CancellationToken cancellationToken)
    {
        var result = await chatService.GetLandlordQuickContactsAsync(GetCurrentUserId(), cancellationToken);
        return Ok(Success(result, "Tải danh sách người thuê thành công."));
    }

    [HttpGet("users/search")]
    public async Task<ActionResult<ApiResponse<List<ChatUserResponse>>>> SearchUsers([FromQuery] string email, [FromQuery] Guid? roomingHouseId, CancellationToken cancellationToken)
    {
        var result = await chatService.SearchUsersByEmailAsync(GetCurrentUserId(), email, roomingHouseId, cancellationToken);
        return Ok(Success(result, "Tìm người dùng thành công."));
    }

    [HttpPost("conversations/{id:guid}/participants")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> AddParticipants(Guid id, AddConversationParticipantsRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.AddParticipantsAsync(GetCurrentUserId(), id, request, cancellationToken);
        await BroadcastConversationAsync(result);
        await hubContext.Clients.Group(ChatHubGroups.Conversation(id)).SendAsync("ParticipantAdded", result, cancellationToken);
        return Ok(Success(result, "Thêm thành viên thành công."));
    }

    [HttpDelete("conversations/{id:guid}/participants/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> RemoveParticipant(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var result = await chatService.RemoveParticipantAsync(GetCurrentUserId(), id, userId, cancellationToken);
        await RemoveUserConnectionsFromConversationAsync(id, userId, cancellationToken);
        await BroadcastConversationAsync(result);
        await hubContext.Clients.Group(ChatHubGroups.User(userId)).SendAsync("ParticipantRemoved", new { conversationId = id }, cancellationToken);
        await hubContext.Clients.Group(ChatHubGroups.Conversation(id)).SendAsync("ParticipantRemoved", new { conversationId = id, userId }, cancellationToken);
        return Ok(Success(result, "Đã xóa thành viên khỏi nhóm."));
    }

    [HttpPatch("conversations/{id:guid}/participants/{userId:guid}/role")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> UpdateParticipantRole(Guid id, Guid userId, [FromBody] UpdateParticipantRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.UpdateParticipantRoleAsync(GetCurrentUserId(), id, userId, request.Role, cancellationToken);
        await BroadcastConversationAsync(result);
        return Ok(Success(result, "Cập nhật vai trò thành viên thành công."));
    }

    [HttpPost("conversations/{id:guid}/leave")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> LeaveConversation(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await chatService.LeaveConversationAsync(userId, id, cancellationToken);
        await RemoveUserConnectionsFromConversationAsync(id, userId, cancellationToken);
        await BroadcastConversationAsync(result);
        await hubContext.Clients.Group(ChatHubGroups.Conversation(id)).SendAsync("ParticipantLeft", new { conversationId = id, userId }, cancellationToken);
        return Ok(Success(result, "Bạn đã rời nhóm."));
    }

    [HttpPost("conversations/{id:guid}/close")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> CloseConversation(Guid id, CancellationToken cancellationToken)
    {
        var result = await chatService.CloseConversationAsync(GetCurrentUserId(), id, cancellationToken);
        await BroadcastConversationAsync(result);
        await hubContext.Clients.Group(ChatHubGroups.Conversation(id)).SendAsync("ConversationClosed", result, cancellationToken);
        return Ok(Success(result, "Đã đóng nhóm chat."));
    }

    [HttpPost("conversations/{id:guid}/clear")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> ClearConversation(Guid id, CancellationToken cancellationToken)
    {
        var result = await chatService.ClearConversationHistoryAsync(GetCurrentUserId(), id, cancellationToken);
        return Ok(Success(result, "Xóa lịch sử trò chuyện thành công."));
    }

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<ActionResult<ApiResponse<List<ChatMessageResponse>>>> GetMessages(
        Guid id,
        [FromQuery] DateTimeOffset? before,
        [FromQuery] int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await chatService.GetMessagesAsync(GetCurrentUserId(), id, before, limit, cancellationToken);
        return Ok(Success(result, "Tải lịch sử tin nhắn thành công."));
    }

    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<ActionResult<ApiResponse<ChatMessageResponse>>> SendMessage(Guid id, SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        var senderUserId = GetCurrentUserId();
        var result = await chatService.SendMessageAsync(senderUserId, id, request, cancellationToken);

        var payload = new { message = result.Message, conversation = result.Conversation };

        // Broadcast MessageCreated to all participants' user groups
        await hubContext.Clients.Group(ChatHubGroups.User(senderUserId)).SendAsync("MessageCreated", payload, cancellationToken);
        foreach (var recipientId in result.RecipientUserIds)
        {
            await hubContext.Clients.Group(ChatHubGroups.User(recipientId)).SendAsync("MessageCreated", payload, cancellationToken);
            await hubContext.Clients.Group(ChatHubGroups.User(recipientId)).SendAsync("UnreadCountUpdated", new { conversationId = id, lastMessageAt = result.Conversation.LastMessageAt }, cancellationToken);
        }

        // Backward compatibility
        await hubContext.Clients.Group(ChatHubGroups.Conversation(id)).SendAsync("ReceiveMessage", result.Message, cancellationToken);
        await BroadcastConversationAsync(result.Conversation);

        return Ok(Success(result.Message, "Gửi tin nhắn thành công."));
    }

    [HttpDelete("conversations/{conversationId:guid}/messages/{messageId:guid}")]
    public async Task<ActionResult<ApiResponse<ChatMessageResponse>>> DeleteMessage(Guid conversationId, Guid messageId, CancellationToken cancellationToken)
    {
        var result = await chatService.DeleteMessageAsync(GetCurrentUserId(), conversationId, messageId, cancellationToken);
        await hubContext.Clients.Group(ChatHubGroups.Conversation(conversationId)).SendAsync("MessageDeleted", result.Message, cancellationToken);
        await BroadcastConversationAsync(result.Conversation);
        return Ok(Success(result.Message, "Xóa tin nhắn thành công."));
    }

    [HttpPost("conversations/{conversationId:guid}/messages/{messageId:guid}/unsend")]
    public async Task<ActionResult<ApiResponse<ChatMessageResponse>>> UnsendMessage(Guid conversationId, Guid messageId, CancellationToken cancellationToken)
    {
        var result = await chatService.UnsendMessageAsync(GetCurrentUserId(), conversationId, messageId, cancellationToken);
        await hubContext.Clients.Group(ChatHubGroups.Conversation(conversationId)).SendAsync("MessageDeleted", result, cancellationToken);

        var conversation = await chatService.GetConversationResponseForUserAsync(conversationId, GetCurrentUserId(), cancellationToken);
        await BroadcastConversationAsync(conversation);
        return Ok(Success(result, "Gỡ tin nhắn thành công."));
    }

    [HttpGet("conversations/{conversationId:guid}/messages/{messageId:guid}/file")]
    public async Task<IActionResult> DownloadFile(Guid conversationId, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await chatService.GetFileMessageAsync(GetCurrentUserId(), conversationId, messageId, cancellationToken);
        if (!message.MediaAssetId.HasValue)
            throw new BadRequestException("CHAT_FILE_NOT_FOUND", "Không tìm thấy tệp tin.");

        return Redirect(PrivateMediaPathBuilder.Build(message.MediaAssetId.Value, forceDownload: true));
    }

    [HttpPatch("conversations/{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await chatService.MarkAsReadAsync(userId, id, cancellationToken);
        await hubContext.Clients.Group(ChatHubGroups.Conversation(id)).SendAsync("MessageRead", new { conversationId = id, userId }, cancellationToken);
        await hubContext.Clients.Group(ChatHubGroups.User(userId)).SendAsync("UnreadCountUpdated", new { conversationId = id, unreadCount = 0 }, cancellationToken);
        return Ok(Success(result, "Đã đánh dấu đã đọc."));
    }

    [HttpGet("conversation-counts")]
    public async Task<ActionResult<ApiResponse<ChatCountsResponse>>> GetConversationCounts(CancellationToken cancellationToken)
    {
        var result = await chatService.GetConversationCountsAsync(GetCurrentUserId(), cancellationToken);
        return Ok(Success(result, "Tải thống kê hộp thư thành công."));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var result = await chatService.GetUnreadMessageCountAsync(GetCurrentUserId(), cancellationToken);
        return Ok(Success(result, "Tải số tin chưa đọc thành công."));
    }

    [HttpPost("conversations/{id:guid}/accept-contact-request")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> AcceptContactRequest(Guid id, CancellationToken cancellationToken)
    {
        var result = await chatService.AcceptContactRequestAsync(GetCurrentUserId(), id, cancellationToken);
        await BroadcastConversationAsync(result.Conversation);
        if (result.SystemMessage is not null)
        {
            var payload = new { message = result.SystemMessage, conversation = result.Conversation };
            foreach (var participant in result.Conversation.Participants.Where(p => p.LeftAt is null))
            {
                await hubContext.Clients.Group(ChatHubGroups.User(participant.UserId)).SendAsync("MessageCreated", payload, cancellationToken);
            }
            await hubContext.Clients.Group(ChatHubGroups.Conversation(id)).SendAsync("ReceiveMessage", result.SystemMessage, cancellationToken);
        }

        return Ok(Success(result.Conversation, "Đã chấp nhận yêu cầu nhắn tin."));
    }

    [HttpPost("conversations/{id:guid}/reject-contact-request")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> RejectContactRequest(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await chatService.RejectContactRequestAsync(userId, id, cancellationToken);
        await hubContext.Clients.Group(ChatHubGroups.User(userId)).SendAsync("ConversationRejected", new { conversationId = id }, cancellationToken);
        await BroadcastConversationAsync(result);

        return Ok(Success(result, "Đã từ chối yêu cầu nhắn tin."));
    }

    [HttpPost("direct/rooming-houses/{roomingHouseId:guid}")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> ContactLandlord(Guid roomingHouseId, [FromBody] ContactLandlordRequest request, CancellationToken cancellationToken)
    {
        var conversation = await chatService.ContactLandlordAsync(GetCurrentUserId(), roomingHouseId, request.InitialMessage, cancellationToken);
        await BroadcastConversationAsync(conversation);
        foreach (var participant in conversation.Participants)
        {
            await hubContext.Clients.Group(ChatHubGroups.User(participant.UserId)).SendAsync("UnreadCountUpdated", new { conversationId = conversation.Id }, cancellationToken);
        }
        return Ok(Success(conversation, "Gửi tin nhắn liên hệ chủ trọ thành công."));
    }

    [HttpPost("direct/rooming-houses/{roomingHouseId:guid}/open")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> OpenDirectConversation(Guid roomingHouseId, CancellationToken cancellationToken)
    {
        var conversation = await chatService.CreateDirectConversationByRoomingHouseAsync(GetCurrentUserId(), roomingHouseId, cancellationToken);
        await BroadcastConversationAsync(conversation);
        foreach (var participant in conversation.Participants)
        {
            await hubContext.Clients.Group(ChatHubGroups.User(participant.UserId)).SendAsync("UnreadCountUpdated", new { conversationId = conversation.Id }, cancellationToken);
        }
        return Ok(Success(conversation, "Mở cuộc trò chuyện trực tiếp thành công."));
    }

    [HttpGet("landlord/rooming-houses/{roomingHouseId:guid}/tenants")]
    public async Task<ActionResult<ApiResponse<List<ChatUserResponse>>>> GetActiveTenantsByRoomingHouse(Guid roomingHouseId, CancellationToken cancellationToken)
    {
        var result = await chatService.GetActiveTenantsByRoomingHouseAsync(GetCurrentUserId(), roomingHouseId, cancellationToken);
        return Ok(Success(result, "Tải danh sách người thuê thành công."));
    }

    [HttpGet("conversations/{id:guid}/eligible-members")]
    public async Task<ActionResult<ApiResponse<List<ChatUserResponse>>>> GetEligibleMembers(Guid id, [FromQuery] Guid? roomingHouseId, CancellationToken cancellationToken)
    {
        var result = await chatService.GetEligibleMembersAsync(GetCurrentUserId(), id, roomingHouseId, cancellationToken);
        return Ok(Success(result, "Tải danh sách thành viên hợp lệ thành công."));
    }

    [HttpGet("filters/rooming-houses")]
    public async Task<ActionResult<ApiResponse<List<ChatRoomingHouseFilterResponse>>>> GetFilterRoomingHouses(CancellationToken cancellationToken)
    {
        var result = await chatService.GetFilterRoomingHousesAsync(GetCurrentUserId(), cancellationToken);
        return Ok(Success(result, "Tải bộ lọc nhà trọ thành công."));
    }

    [HttpGet("conversations/{id:guid}/join-requests")]
    public async Task<ActionResult<ApiResponse<List<ConversationJoinRequestResponse>>>> GetJoinRequests(Guid id, CancellationToken cancellationToken)
    {
        var result = await chatService.GetJoinRequestsAsync(GetCurrentUserId(), id, cancellationToken);
        return Ok(Success(result, "Tải danh sách yêu cầu tham gia thành công."));
    }

    [HttpPost("conversations/{id:guid}/join-requests")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> CreateJoinRequest(Guid id, [FromBody] CreateJoinRequestRequest? request, CancellationToken cancellationToken)
    {
        var result = await chatService.CreateJoinRequestAsync(GetCurrentUserId(), id, request?.TargetUserId, cancellationToken);
        await BroadcastConversationAsync(result);
        return Ok(Success(result, "Gửi yêu cầu tham gia thành công."));
    }

    [HttpPost("conversations/{id:guid}/join-requests/{requestId:guid}/approve")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> ApproveJoinRequest(Guid id, Guid requestId, CancellationToken cancellationToken)
    {
        var result = await chatService.ApproveJoinRequestAsync(GetCurrentUserId(), id, requestId, cancellationToken);
        await BroadcastConversationAsync(result);
        return Ok(Success(result, "Đã duyệt yêu cầu tham gia."));
    }

    [HttpPost("conversations/{id:guid}/join-requests/{requestId:guid}/reject")]
    public async Task<ActionResult<ApiResponse<object>>> RejectJoinRequest(Guid id, Guid requestId, CancellationToken cancellationToken)
    {
        await chatService.RejectJoinRequestAsync(GetCurrentUserId(), id, requestId, cancellationToken);
        return Ok(Success<object>(new { }, "Đã từ chối yêu cầu tham gia."));
    }

    [HttpPatch("conversations/{id:guid}/approval-settings")]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> UpdateApprovalSettings(Guid id, [FromBody] UpdateApprovalSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.UpdateApprovalSettingsAsync(GetCurrentUserId(), id, request.RequiresJoinApproval, cancellationToken);
        await BroadcastConversationAsync(result);
        return Ok(Success(result, "Cập nhật cài đặt phê duyệt thành công."));
    }

    [HttpPost("images")]
    [RequestSizeLimit(MaxImageBytes)]
    public async Task<ActionResult<ApiResponse<ChatImageUploadResponse>>> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new BadRequestException("CHAT_IMAGE_REQUIRED", "Vui lòng chọn ảnh.");

        if (file.Length > MaxImageBytes)
            throw new BadRequestException("CHAT_IMAGE_TOO_LARGE", "Ảnh chat tối đa 5MB.");

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedImageExtensions.Contains(extension))
            throw new BadRequestException("CHAT_IMAGE_TYPE_INVALID", "Ảnh chat chỉ hỗ trợ jpg, jpeg, png, webp hoặc gif.");

        await using var stream = file.OpenReadStream();
        var uploaded = await fileStorageService.UploadImageAsync(
            new ImageUploadFile
            {
                Content = stream,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Length = file.Length
            },
            FileUploadScope.ChatImage,
            cancellationToken);

        return Ok(Success(new ChatImageUploadResponse
        {
            MediaAssetId = uploaded.MediaAssetId,
            Url = uploaded.Url
        }, "Tải ảnh chat thành công."));
    }

    [HttpPost("avatars")]
    [RequestSizeLimit(MaxImageBytes)]
    public async Task<ActionResult<ApiResponse<ChatImageUploadResponse>>> UploadAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new BadRequestException("CHAT_AVATAR_REQUIRED", "Vui lòng chọn ảnh đại diện nhóm.");

        if (file.Length > MaxImageBytes)
            throw new BadRequestException("CHAT_AVATAR_TOO_LARGE", "Ảnh đại diện nhóm tối đa 5MB.");

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedImageExtensions.Contains(extension))
            throw new BadRequestException("CHAT_AVATAR_TYPE_INVALID", "Ảnh đại diện chỉ hỗ trợ jpg, jpeg, png, webp hoặc gif.");

        await using var stream = file.OpenReadStream();
        var uploaded = await fileStorageService.UploadImageAsync(
            new ImageUploadFile
            {
                Content = stream,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Length = file.Length
            },
            FileUploadScope.Avatar,
            cancellationToken);

        return Ok(Success(new ChatImageUploadResponse
        {
            MediaAssetId = uploaded.MediaAssetId,
            Url = uploaded.Url
        }, "Tải ảnh đại diện nhóm thành công."));
    }

    [HttpPost("files")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ChatFileUploadResponse>>> UploadFile(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new BadRequestException("CHAT_FILE_REQUIRED", "Vui lòng chọn tệp.");

        await using var stream = file.OpenReadStream();
        var uploaded = await fileStorageService.UploadFileAsync(
            stream,
            file.FileName,
            file.ContentType,
            FileUploadScope.ChatFile,
            cancellationToken);

        return Ok(Success(new ChatFileUploadResponse
        {
            MediaAssetId = uploaded.MediaAssetId,
            Url = uploaded.Url,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Size = file.Length
        }, "Tải tệp thành công."));
    }

    private Guid GetCurrentUserId()
    {
        return currentUserService.GetRequiredUserId("Bạn cần đăng nhập để sử dụng chat.");
    }

    private async Task BroadcastConversationAsync(ConversationResponse conversation)
    {
        await hubContext.Clients.Group(ChatHubGroups.Conversation(conversation.Id)).SendAsync("ConversationUpdated", conversation);
        foreach (var participant in conversation.Participants.Where(x => x.LeftAt is null))
        {
            await hubContext.Clients.Group(ChatHubGroups.User(participant.UserId)).SendAsync("ConversationUpdated", conversation);
        }
    }

    private async Task RemoveUserConnectionsFromConversationAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        foreach (var connectionId in presenceTracker.GetConversationConnectionIds(conversationId, userId))
        {
            await hubContext.Groups.RemoveFromGroupAsync(connectionId, ChatHubGroups.Conversation(conversationId), cancellationToken);
            presenceTracker.LeaveConversation(conversationId, userId, connectionId);
        }
    }

    private static ApiResponse<T> Success<T>(T data, string message)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }
}

public sealed class UpdateParticipantRoleRequest
{
    public ConversationParticipantRole Role { get; set; }
}

public sealed class CreateJoinRequestRequest
{
    public Guid? TargetUserId { get; set; }
}

public sealed class UpdateApprovalSettingsRequest
{
    public bool RequiresJoinApproval { get; set; }
}

public sealed class ContactLandlordRequest
{
    public string InitialMessage { get; set; } = string.Empty;
}
