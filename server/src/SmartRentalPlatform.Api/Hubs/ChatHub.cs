using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartRentalPlatform.Application.Chat;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Chat.Requests;
using SmartRentalPlatform.Contracts.Chat.Responses;

namespace SmartRentalPlatform.Api.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IChatService chatService;
    private readonly IChatPresenceTracker presenceTracker;

    public ChatHub(IChatService chatService, IChatPresenceTracker presenceTracker)
    {
        this.chatService = chatService;
        this.presenceTracker = presenceTracker;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, ChatHubGroups.User(userId));
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        presenceTracker.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        await chatService.EnsureCanJoinConversationAsync(userId, conversationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, ChatHubGroups.Conversation(conversationId));
        presenceTracker.JoinConversation(conversationId, userId, Context.ConnectionId);
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ChatHubGroups.Conversation(conversationId));
        presenceTracker.LeaveConversation(conversationId, userId, Context.ConnectionId);
    }

    public async Task<ChatMessageResponse> SendMessage(Guid conversationId, SendChatMessageRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await chatService.SendMessageAsync(userId, conversationId, request);

        await Clients.Group(ChatHubGroups.Conversation(conversationId))
            .SendAsync("ReceiveMessage", result.Message);
        await Clients.Group(ChatHubGroups.Conversation(conversationId))
            .SendAsync("ConversationUpdated", result.Conversation);

        foreach (var recipientId in result.RecipientUserIds)
        {
            await Clients.Group(ChatHubGroups.User(recipientId))
                .SendAsync("UnreadCountUpdated", new { conversationId, result.Conversation.LastMessageAt });
        }

        return result.Message;
    }

    public async Task Typing(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        await chatService.EnsureCanJoinConversationAsync(userId, conversationId);
        await Clients.OthersInGroup(ChatHubGroups.Conversation(conversationId))
            .SendAsync("Typing", new { conversationId, userId });
    }

    private Guid GetCurrentUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var userId))
            return userId;

        throw new UnauthorizedException("UNAUTHORIZED", "Bạn cần đăng nhập để sử dụng chat.");
    }
}
