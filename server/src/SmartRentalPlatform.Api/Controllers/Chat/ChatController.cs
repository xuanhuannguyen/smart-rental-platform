using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Api.Hubs;
using SmartRentalPlatform.Application.Chat;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Contracts.Chat.Requests;
using SmartRentalPlatform.Contracts.Chat.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Files;

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
    public async Task<ActionResult<ApiResponse<List<ConversationResponse>>>> GetConversations(CancellationToken cancellationToken)
    {
        var result = await chatService.GetConversationsAsync(GetCurrentUserId(), cancellationToken);
        return Ok(Success(result, "Tải danh sách tin nhắn thành công."));
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
    public async Task<ActionResult<ApiResponse<List<ChatUserResponse>>>> SearchUsers([FromQuery] string email, CancellationToken cancellationToken)
    {
        var result = await chatService.SearchUsersByEmailAsync(GetCurrentUserId(), email, cancellationToken);
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
        var result = await chatService.SendMessageAsync(GetCurrentUserId(), id, request, cancellationToken);
        await hubContext.Clients.Group(ChatHubGroups.Conversation(id)).SendAsync("ReceiveMessage", result.Message, cancellationToken);
        await BroadcastConversationAsync(result.Conversation);
        foreach (var recipientId in result.RecipientUserIds)
        {
            await hubContext.Clients.Group(ChatHubGroups.User(recipientId)).SendAsync("UnreadCountUpdated", new { conversationId = id }, cancellationToken);
        }
        return Ok(Success(result.Message, "Gửi tin nhắn thành công."));
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
            ObjectKey = uploaded.ObjectKey,
            Url = uploaded.Url
        }, "Tải ảnh chat thành công."));
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
