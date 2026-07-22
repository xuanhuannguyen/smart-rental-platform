using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Contracts.Chat.Responses;
using SmartRentalPlatform.Domain.Entities.Chat;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Chat;
using SmartRentalPlatform.Domain.Enums.Users;

namespace SmartRentalPlatform.Application.Chat;

internal static class ChatResponseMapper
{
    public static string BuildPreview(ChatMessageType type, string? content)
    {
        return type switch
        {
            ChatMessageType.Image => "Đã gửi một hình ảnh",
            ChatMessageType.Icon => content ?? "Đã gửi một biểu tượng",
            ChatMessageType.System => content ?? "Cập nhật nhóm",
            ChatMessageType.File => "Đã gửi một tệp đính kèm",
            _ => content is { Length: > 120 } ? content[..120] + "..." : content ?? string.Empty
        };
    }

    public static string BuildNotificationBody(ChatMessage message)
    {
        return BuildPreview(message.MessageType, message.Content);
    }

    public static ConversationResponse MapConversation(Conversation conversation, Guid currentUserId)
    {
        var currentParticipant = conversation.Participants.FirstOrDefault(x => x.UserId == currentUserId);
        var isOwner = currentParticipant?.Role == ConversationParticipantRole.Owner;

        var lastMsgAt = conversation.LastMessageAt;
        var lastMsgPreview = conversation.LastMessagePreview;
        if (currentParticipant is not null && lastMsgAt.HasValue && lastMsgAt.Value < currentParticipant.JoinedAt)
        {
            lastMsgAt = null;
            lastMsgPreview = null;
        }

        if (lastMsgPreview != null && lastMsgPreview.Contains("đã tạo đoạn chat nhóm mới"))
        {
            lastMsgPreview = conversation.CreatedByUserId == currentUserId
                ? "Bạn đã tạo đoạn chat nhóm mới"
                : $"{conversation.CreatedByUser?.DisplayName ?? "Chủ trọ"} đã tạo đoạn chat nhóm mới";
        }

        return new ConversationResponse
        {
            Id = conversation.Id,
            Type = conversation.Type.ToString(),
            Title = ResolveTitle(conversation, currentUserId),
            CreatedByUserId = conversation.CreatedByUserId,
            LastMessageAt = lastMsgAt,
            LastMessagePreview = lastMsgPreview,
            UnreadCount = currentParticipant?.UnreadCount ?? 0,
            IsClosed = conversation.IsClosed,
            IsCurrentUserOwner = isOwner,
            HasCurrentUserLeft = currentParticipant?.LeftAt is not null,
            RequiresJoinApproval = conversation.RequiresJoinApproval,
            IsCurrentUserAdmin = isOwner,
            CanManageMembers = isOwner && conversation.Type == ConversationType.Group,
            RoomingHouseId = conversation.RoomingHouseId,
            RoomingHouseName = conversation.RoomingHouse?.Name,
            InboxStatus = currentParticipant?.InboxStatus.ToString() ?? "Main",
            AvatarUrl = conversation.AvatarMediaAssetId.HasValue
                ? PublicMediaPathBuilder.Build(conversation.AvatarMediaAssetId.Value)
                : conversation.AvatarUrl,
            AvatarMediaAssetId = conversation.AvatarMediaAssetId,
            Participants = conversation.Participants
                .OrderBy(x => x.Role == ConversationParticipantRole.Owner ? 0 : 1)
                .ThenBy(x => x.User?.DisplayName ?? string.Empty)
                .Select(MapParticipant)
                .ToList()
        };
    }

    public static ChatMessageResponse MapMessage(ChatMessage message, Guid currentUserId)
    {
        var isDeleted = message.DeletedAt != null;
        var content = message.Content;

        if (isDeleted)
        {
            content = "Tin nhắn đã bị thu hồi";
        }
        else if (message.MessageType == ChatMessageType.System && content != null && content.Contains("đã tạo đoạn chat nhóm mới"))
        {
            content = message.SenderId == currentUserId
                ? "Bạn đã tạo đoạn chat nhóm mới"
                : $"{message.Sender?.DisplayName ?? "Chủ trọ"} đã tạo đoạn chat nhóm mới";
        }

        return new ChatMessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = message.Sender?.DisplayName ?? "Chủ trọ",
            MessageType = message.MessageType.ToString(),
            Content = content,
            MediaAssetId = isDeleted ? null : message.MediaAssetId,
            ImageUrl = !isDeleted && message.MessageType == ChatMessageType.Image && message.MediaAssetId.HasValue
                ? PrivateMediaPathBuilder.Build(message.MediaAssetId.Value)
                : null,
            FileUrl = !isDeleted && message.MessageType == ChatMessageType.File && message.MediaAssetId.HasValue
                ? PrivateMediaPathBuilder.Build(message.MediaAssetId.Value, forceDownload: true)
                : null,
            FileName = isDeleted ? null : message.FileName,
            FileContentType = isDeleted ? null : message.FileContentType,
            FileSize = isDeleted ? null : message.FileSize,
            CreatedAt = message.CreatedAt,
            DeletedAt = message.DeletedAt
        };
    }

    public static ChatUserResponse MapChatUser(User user, string? contextLabel)
    {
        return new ChatUserResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            ContextLabel = contextLabel,
            Roles = user.UserRoles.Select(x => x.Role.Name.ToString()).ToList()
        };
    }

    public static void AddQuickContact(Dictionary<Guid, ChatUserResponse> users, User user, string contextLabel)
    {
        if (user.Status != UserStatus.Active || user.DeletedAt != null)
        {
            return;
        }

        users.TryAdd(user.Id, MapChatUser(user, contextLabel));
    }

    public static string NormalizeEmail(string email)
    {
        return (email ?? string.Empty).Trim().ToUpperInvariant();
    }

    public static string NormalizeTitle(string? title, string fallback)
    {
        var normalized = title?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? fallback
            : normalized.Length > 200 ? normalized[..200] : normalized;
    }

    public static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string ResolveTitle(Conversation conversation, Guid currentUserId)
    {
        if (conversation.Type == ConversationType.Group)
        {
            return string.IsNullOrWhiteSpace(conversation.Title) ? "Nhóm trò chuyện" : conversation.Title!;
        }

        var other = conversation.Participants.FirstOrDefault(x => x.UserId != currentUserId)?.User;
        return other?.DisplayName ?? "Tin nhắn";
    }

    private static ChatParticipantResponse MapParticipant(ConversationParticipant participant)
    {
        return new ChatParticipantResponse
        {
            UserId = participant.UserId,
            DisplayName = participant.User.DisplayName,
            Email = participant.User.Email,
            AvatarUrl = participant.User.AvatarMediaAssetId.HasValue
                ? PublicMediaPathBuilder.Build(participant.User.AvatarMediaAssetId.Value)
                : participant.User.AvatarUrl,
            Role = participant.Role.ToString(),
            Source = participant.Source.ToString(),
            JoinedAt = participant.JoinedAt,
            LeftAt = participant.LeftAt,
            InboxStatus = participant.InboxStatus.ToString()
        };
    }
}
