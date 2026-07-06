using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Chat.Requests;
using SmartRentalPlatform.Contracts.Chat.Responses;
using SmartRentalPlatform.Domain.Entities.Chat;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Chat;
using SmartRentalPlatform.Domain.Enums.Notifications;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Users;

namespace SmartRentalPlatform.Application.Chat;

public sealed class ChatService : IChatService
{
    private const int MaxTextLength = 2000;
    private const int MaxIconLength = 20;
    private const int MaxMessagesLimit = 100;

    private readonly IAppDbContext context;
    private readonly INotificationService notificationService;
    private readonly IChatPresenceTracker presenceTracker;

    public ChatService(
        IAppDbContext context,
        INotificationService notificationService,
        IChatPresenceTracker presenceTracker)
    {
        this.context = context;
        this.notificationService = notificationService;
        this.presenceTracker = presenceTracker;
    }

    public async Task<List<ConversationResponse>> GetConversationsAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var conversations = await BaseConversationQuery()
            .Where(x => x.Participants.Any(p => p.UserId == currentUserId && p.LeftAt == null))
            .OrderByDescending(x => x.LastMessageAt ?? x.UpdatedAt)
            .ToListAsync(cancellationToken);

        return conversations.Select(x => MapConversation(x, currentUserId)).ToList();
    }

    public async Task<ConversationResponse> CreateDirectConversationAsync(Guid currentUserId, CreateDirectConversationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OtherUserId == Guid.Empty)
            throw new BadRequestException("CHAT_INVALID_USER", "Vui lòng chọn người nhận.");

        if (request.OtherUserId == currentUserId)
            throw new BadRequestException("CHAT_SELF_CHAT", "Không thể tạo cuộc trò chuyện với chính mình.");

        var users = await context.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Where(x => (x.Id == currentUserId || x.Id == request.OtherUserId) && x.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var currentUser = users.FirstOrDefault(x => x.Id == currentUserId) ?? throw new NotFoundException("CHAT_USER_NOT_FOUND", "Không tìm thấy người dùng hiện tại.");
        var otherUser = users.FirstOrDefault(x => x.Id == request.OtherUserId) ?? throw new NotFoundException("CHAT_USER_NOT_FOUND", "Không tìm thấy người dùng cần chat.");

        EnsureActiveUser(currentUser);
        EnsureActiveUser(otherUser);

        var (userAId, userBId) = NormalizePair(currentUserId, request.OtherUserId);
        var existing = await BaseConversationQuery()
            .FirstOrDefaultAsync(x => x.Type == ConversationType.Direct && x.DirectUserAId == userAId && x.DirectUserBId == userBId, cancellationToken);

        if (existing is not null)
        {
            ReactivateParticipant(existing, currentUserId, currentUserId);
            ReactivateParticipant(existing, request.OtherUserId, currentUserId);
            await context.SaveChangesAsync(cancellationToken);
            return MapConversation(existing, currentUserId);
        }

        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            DirectUserAId = userAId,
            DirectUserBId = userBId,
            CreatedByUserId = currentUserId,
            CreatedAt = now,
            UpdatedAt = now,
            Participants =
            {
                CreateParticipant(currentUserId, ConversationParticipantRole.Member, ConversationParticipantSource.Manual, currentUserId, now),
                CreateParticipant(request.OtherUserId, ConversationParticipantRole.Member, ConversationParticipantSource.Manual, currentUserId, now)
            }
        };

        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadConversationAsync(conversation.Id, cancellationToken);
        return MapConversation(saved, currentUserId);
    }

    public async Task<ConversationResponse> CreateGroupConversationAsync(Guid currentUserId, CreateGroupConversationRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureLandlordAsync(currentUserId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Group,
            Title = NormalizeTitle(request.Title, "Nhóm trò chuyện"),
            CreatedByUserId = currentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        conversation.Participants.Add(CreateParticipant(currentUserId, ConversationParticipantRole.Owner, ConversationParticipantSource.Manual, currentUserId, now));

        foreach (var userId in request.ParticipantUserIds.Where(x => x != Guid.Empty && x != currentUserId).Distinct())
        {
            await EnsureUserCanBeAddedByLandlordAsync(currentUserId, userId, cancellationToken);
            conversation.Participants.Add(CreateParticipant(userId, ConversationParticipantRole.Member, ConversationParticipantSource.RoomQuickPick, currentUserId, now));
        }

        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadConversationAsync(conversation.Id, cancellationToken);
        return MapConversation(saved, currentUserId);
    }

    public async Task<ConversationResponse> UpdateConversationAsync(Guid currentUserId, Guid conversationId, UpdateConversationRequest request, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureOwner(currentUserId, conversation);
        EnsureGroup(conversation);

        conversation.Title = NormalizeTitle(request.Title, conversation.Title ?? "Nhóm trò chuyện");
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return MapConversation(conversation, currentUserId);
    }

    public async Task<List<ChatUserResponse>> GetLandlordQuickContactsAsync(Guid landlordUserId, CancellationToken cancellationToken = default)
    {
        await EnsureLandlordAsync(landlordUserId, cancellationToken);

        var contracts = await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room).ThenInclude(x => x.RoomingHouse)
            .Include(x => x.MainTenantUser).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.Occupants).ThenInclude(x => x.User).ThenInclude(x => x!.UserRoles).ThenInclude(x => x.Role)
            .Where(x => x.DeletedAt == null && x.Room.RoomingHouse.LandlordUserId == landlordUserId)
            .ToListAsync(cancellationToken);

        var users = new Dictionary<Guid, ChatUserResponse>();
        foreach (var contract in contracts)
        {
            AddQuickContact(users, contract.MainTenantUser, $"Phòng {contract.Room.RoomNumber}");
            foreach (var occupant in contract.Occupants.Where(x => x.User is not null))
            {
                AddQuickContact(users, occupant.User!, $"Phòng {contract.Room.RoomNumber}");
            }
        }

        return users.Values.OrderBy(x => x.DisplayName).ToList();
    }

    public async Task<List<ChatUserResponse>> SearchUsersByEmailAsync(Guid currentUserId, string email, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        if (normalized.Length < 3)
            return new List<ChatUserResponse>();

        var currentUser = await context.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == currentUserId && x.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("CHAT_USER_NOT_FOUND", "Không tìm thấy người dùng hiện tại.");

        if (HasRole(currentUser, RoleName.Landlord))
        {
            var quickContacts = await GetLandlordQuickContactsAsync(currentUserId, cancellationToken);
            return quickContacts
                .Where(x => x.UserId != currentUserId && x.Email.ToUpperInvariant().Contains(normalized, StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .ToList();
        }

        var landlords = await context.RoomingHouses
            .AsNoTracking()
            .Include(x => x.Landlord).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role)
            .Where(x => x.Landlord.Email.ToUpper().Contains(normalized) &&
                x.LandlordUserId != currentUserId &&
                x.Landlord.DeletedAt == null &&
                x.Landlord.Status == UserStatus.Active)
            .Select(x => x.Landlord)
            .Distinct()
            .Take(10)
            .ToListAsync(cancellationToken);

        return landlords.Select(x => MapChatUser(x, "Chủ trọ")).ToList();
    }

    public async Task<ConversationResponse> AddParticipantsAsync(Guid currentUserId, Guid conversationId, AddConversationParticipantsRequest request, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureOwner(currentUserId, conversation);
        EnsureGroup(conversation);

        var now = DateTimeOffset.UtcNow;
        foreach (var userId in request.UserIds.Where(x => x != Guid.Empty && x != currentUserId).Distinct())
        {
            await EnsureUserCanBeAddedByLandlordAsync(currentUserId, userId, cancellationToken);
            var participant = conversation.Participants.FirstOrDefault(x => x.UserId == userId);
            if (participant is null)
            {
                conversation.Participants.Add(CreateParticipant(userId, ConversationParticipantRole.Member, ConversationParticipantSource.ContractEmail, currentUserId, now));
            }
            else
            {
                participant.LeftAt = null;
                participant.JoinedAt = now;
                participant.AddedByUserId = currentUserId;
                participant.Source = ConversationParticipantSource.ContractEmail;
            }
        }

        conversation.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        return MapConversation(conversation, currentUserId);
    }

    public async Task<ConversationResponse> RemoveParticipantAsync(Guid currentUserId, Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureOwner(currentUserId, conversation);
        EnsureGroup(conversation);

        var participant = conversation.Participants.FirstOrDefault(x => x.UserId == userId && x.LeftAt == null)
            ?? throw new NotFoundException("CHAT_PARTICIPANT_NOT_FOUND", "Không tìm thấy thành viên trong nhóm.");

        if (participant.Role == ConversationParticipantRole.Owner)
            throw new BadRequestException("CHAT_OWNER_REMOVE_INVALID", "Không thể xóa chủ nhóm.");

        participant.LeftAt = DateTimeOffset.UtcNow;
        participant.UnreadCount = 0;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return MapConversation(conversation, currentUserId);
    }

    public async Task<ConversationResponse> LeaveConversationAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureGroup(conversation);

        var participant = EnsureActiveParticipant(currentUserId, conversation);
        if (participant.Role == ConversationParticipantRole.Owner)
            throw new BadRequestException("CHAT_OWNER_LEAVE_INVALID", "Chủ nhóm không thể rời nhóm trong giai đoạn này. Vui lòng đóng nhóm nếu cần.");

        participant.LeftAt = DateTimeOffset.UtcNow;
        participant.UnreadCount = 0;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return MapConversation(conversation, currentUserId);
    }

    public async Task<ConversationResponse> CloseConversationAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureOwner(currentUserId, conversation);
        EnsureGroup(conversation);

        conversation.IsClosed = true;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return MapConversation(conversation, currentUserId);
    }

    public async Task<List<ChatMessageResponse>> GetMessagesAsync(Guid currentUserId, Guid conversationId, DateTimeOffset? before, int limit = 30, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureActiveParticipant(currentUserId, conversation);

        limit = Math.Clamp(limit, 1, MaxMessagesLimit);
        var query = context.ChatMessages
            .AsNoTracking()
            .Include(x => x.Sender)
            .Where(x => x.ConversationId == conversationId);

        if (before.HasValue)
            query = query.Where(x => x.CreatedAt < before.Value);

        var messages = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return messages.OrderBy(x => x.CreatedAt).Select(x => MapMessage(x)).ToList();
    }

    public async Task<SendChatMessageResponse> SendMessageAsync(Guid currentUserId, Guid conversationId, SendChatMessageRequest request, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        if (conversation.IsClosed)
            throw new BadRequestException("CHAT_CONVERSATION_CLOSED", "Cuộc trò chuyện đã đóng.");

        EnsureActiveParticipant(currentUserId, conversation);
        var messageType = ParseMessageType(request.MessageType);
        ValidateMessage(messageType, request);

        var now = DateTimeOffset.UtcNow;
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderId = currentUserId,
            MessageType = messageType,
            Content = NormalizeOptional(request.Content),
            ImageUrl = NormalizeOptional(request.ImageUrl),
            CreatedAt = now
        };

        context.ChatMessages.Add(message);
        conversation.LastMessageAt = now;
        conversation.LastMessagePreview = BuildPreview(messageType, message.Content);
        conversation.UpdatedAt = now;

        var recipientIds = new List<Guid>();
        foreach (var participant in conversation.Participants.Where(x => x.LeftAt == null && x.UserId != currentUserId))
        {
            participant.UnreadCount += 1;
            recipientIds.Add(participant.UserId);
        }

        await context.SaveChangesAsync(cancellationToken);

        var savedMessage = await context.ChatMessages
            .AsNoTracking()
            .Include(x => x.Sender)
            .FirstAsync(x => x.Id == message.Id, cancellationToken);

        foreach (var recipientId in recipientIds)
        {
            if (!presenceTracker.IsUserViewingConversation(conversationId, recipientId))
            {
                await notificationService.CreateAsync(
                    recipientId,
                    NotificationType.NewChatMessage,
                    "Tin nhắn mới",
                    BuildNotificationBody(savedMessage),
                    conversationId.ToString(),
                    "Conversation",
                    cancellationToken);
            }
        }

        var refreshed = await LoadConversationAsync(conversationId, cancellationToken);
        var response = MapMessage(savedMessage);
        response.ClientMessageId = request.ClientMessageId;

        return new SendChatMessageResponse
        {
            Message = response,
            Conversation = MapConversation(refreshed, currentUserId),
            RecipientUserIds = recipientIds
        };
    }

    public async Task<ConversationResponse> MarkAsReadAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        var participant = EnsureActiveParticipant(currentUserId, conversation);
        participant.UnreadCount = 0;
        participant.LastReadAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return MapConversation(conversation, currentUserId);
    }

    public async Task EnsureCanJoinConversationAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var canJoin = await context.ConversationParticipants
            .AnyAsync(x => x.ConversationId == conversationId && x.UserId == currentUserId && x.LeftAt == null && !x.Conversation.IsClosed, cancellationToken);

        if (!canJoin)
            throw new ForbiddenException("CHAT_FORBIDDEN", "Bạn không có quyền tham gia cuộc trò chuyện này.");
    }

    private IQueryable<Conversation> BaseConversationQuery()
    {
        return context.Conversations
            .Include(x => x.Participants).ThenInclude(x => x.User)
            .Include(x => x.Participants).ThenInclude(x => x.User).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role);
    }

    private async Task<Conversation> LoadConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        return await BaseConversationQuery()
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("CHAT_CONVERSATION_NOT_FOUND", "Không tìm thấy cuộc trò chuyện.");
    }

    private static ConversationParticipant CreateParticipant(Guid userId, ConversationParticipantRole role, ConversationParticipantSource source, Guid addedByUserId, DateTimeOffset now)
    {
        return new ConversationParticipant
        {
            UserId = userId,
            Role = role,
            Source = source,
            AddedByUserId = addedByUserId,
            JoinedAt = now
        };
    }

    private static void ReactivateParticipant(Conversation conversation, Guid userId, Guid addedByUserId)
    {
        var participant = conversation.Participants.FirstOrDefault(x => x.UserId == userId);
        if (participant is not null)
        {
            participant.LeftAt = null;
            participant.AddedByUserId = addedByUserId;
        }
    }

    private static (Guid UserAId, Guid UserBId) NormalizePair(Guid left, Guid right)
    {
        return left.CompareTo(right) <= 0 ? (left, right) : (right, left);
    }

    private async Task EnsureLandlordAsync(Guid userId, CancellationToken cancellationToken)
    {
        var isLandlord = await context.UserRoles.AnyAsync(x => x.UserId == userId && x.Role.Name == RoleName.Landlord, cancellationToken);
        if (!isLandlord)
            throw new ForbiddenException("CHAT_LANDLORD_REQUIRED", "Chỉ chủ trọ mới có quyền thực hiện thao tác này.");
    }

    private async Task EnsureUserCanBeAddedByLandlordAsync(Guid landlordUserId, Guid userId, CancellationToken cancellationToken)
    {
        var isAllowed = await context.RentalContracts
            .AsNoTracking()
            .AnyAsync(x => x.DeletedAt == null &&
                x.Room.RoomingHouse.LandlordUserId == landlordUserId &&
                (x.MainTenantUserId == userId || x.Occupants.Any(o => o.UserId == userId)), cancellationToken);

        if (!isAllowed)
            throw new ForbiddenException("CHAT_MEMBER_OUT_OF_SCOPE", "Người dùng này không thuộc khu trọ/hợp đồng của bạn.");

        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId && x.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("CHAT_USER_NOT_FOUND", "Không tìm thấy người dùng.");
        EnsureActiveUser(user);
    }

    private static void EnsureActiveUser(User user)
    {
        if (user.Status != UserStatus.Active || user.DeletedAt != null)
            throw new ForbiddenException("CHAT_USER_INACTIVE", "Tài khoản không còn hoạt động.");
    }

    private static bool HasRole(User user, RoleName role)
    {
        return user.UserRoles.Any(x => x.Role.Name == role);
    }

    private static void EnsureGroup(Conversation conversation)
    {
        if (conversation.Type != ConversationType.Group)
            throw new BadRequestException("CHAT_GROUP_REQUIRED", "Thao tác này chỉ áp dụng cho nhóm chat.");
    }

    private static void EnsureOwner(Guid currentUserId, Conversation conversation)
    {
        var participant = conversation.Participants.FirstOrDefault(x => x.UserId == currentUserId && x.LeftAt == null);
        if (participant?.Role != ConversationParticipantRole.Owner)
            throw new ForbiddenException("CHAT_OWNER_REQUIRED", "Chỉ chủ nhóm mới có quyền thực hiện thao tác này.");
    }

    private static ConversationParticipant EnsureActiveParticipant(Guid currentUserId, Conversation conversation)
    {
        return conversation.Participants.FirstOrDefault(x => x.UserId == currentUserId && x.LeftAt == null)
            ?? throw new ForbiddenException("CHAT_FORBIDDEN", "Bạn không có quyền truy cập cuộc trò chuyện này.");
    }

    private static ChatMessageType ParseMessageType(string value)
    {
        if (!Enum.TryParse<ChatMessageType>(value, true, out var type))
            throw new BadRequestException("CHAT_MESSAGE_TYPE_INVALID", "Loại tin nhắn không hợp lệ.");

        return type;
    }

    private static void ValidateMessage(ChatMessageType type, SendChatMessageRequest request)
    {
        var content = request.Content?.Trim();
        var imageUrl = request.ImageUrl?.Trim();

        if (type == ChatMessageType.Text && string.IsNullOrWhiteSpace(content))
            throw new BadRequestException("CHAT_TEXT_REQUIRED", "Nội dung tin nhắn không được để trống.");
        if (type == ChatMessageType.Text && content!.Length > MaxTextLength)
            throw new BadRequestException("CHAT_TEXT_TOO_LONG", "Tin nhắn tối đa 2000 ký tự.");
        if (type == ChatMessageType.Icon && string.IsNullOrWhiteSpace(content))
            throw new BadRequestException("CHAT_ICON_REQUIRED", "Vui lòng chọn biểu tượng.");
        if (type == ChatMessageType.Icon && content!.Length > MaxIconLength)
            throw new BadRequestException("CHAT_ICON_TOO_LONG", "Biểu tượng không hợp lệ.");
        if (type == ChatMessageType.Image && string.IsNullOrWhiteSpace(imageUrl))
            throw new BadRequestException("CHAT_IMAGE_REQUIRED", "Vui lòng tải ảnh trước khi gửi.");
    }

    private static string BuildPreview(ChatMessageType type, string? content)
    {
        return type switch
        {
            ChatMessageType.Image => "Đã gửi một hình ảnh",
            ChatMessageType.Icon => content ?? "Đã gửi một biểu tượng",
            ChatMessageType.System => content ?? "Cập nhật nhóm",
            _ => content is { Length: > 120 } ? content[..120] + "..." : content ?? string.Empty
        };
    }

    private static string BuildNotificationBody(ChatMessage message)
    {
        return BuildPreview(message.MessageType, message.Content);
    }

    private ConversationResponse MapConversation(Conversation conversation, Guid currentUserId)
    {
        var currentParticipant = conversation.Participants.FirstOrDefault(x => x.UserId == currentUserId);
        return new ConversationResponse
        {
            Id = conversation.Id,
            Type = conversation.Type.ToString(),
            Title = ResolveTitle(conversation, currentUserId),
            CreatedByUserId = conversation.CreatedByUserId,
            LastMessageAt = conversation.LastMessageAt,
            LastMessagePreview = conversation.LastMessagePreview,
            UnreadCount = currentParticipant?.UnreadCount ?? 0,
            IsClosed = conversation.IsClosed,
            IsCurrentUserOwner = currentParticipant?.Role == ConversationParticipantRole.Owner,
            HasCurrentUserLeft = currentParticipant?.LeftAt is not null,
            Participants = conversation.Participants
                .OrderBy(x => x.Role == ConversationParticipantRole.Owner ? 0 : 1)
                .ThenBy(x => x.User.DisplayName)
                .Select(MapParticipant)
                .ToList()
        };
    }

    private static string ResolveTitle(Conversation conversation, Guid currentUserId)
    {
        if (conversation.Type == ConversationType.Group)
            return string.IsNullOrWhiteSpace(conversation.Title) ? "Nhóm trò chuyện" : conversation.Title!;

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
            AvatarUrl = participant.User.AvatarUrl,
            Role = participant.Role.ToString(),
            Source = participant.Source.ToString(),
            JoinedAt = participant.JoinedAt,
            LeftAt = participant.LeftAt
        };
    }

    private static ChatMessageResponse MapMessage(ChatMessage message)
    {
        return new ChatMessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = message.Sender.DisplayName,
            MessageType = message.MessageType.ToString(),
            Content = message.Content,
            ImageUrl = message.ImageUrl,
            CreatedAt = message.CreatedAt,
            DeletedAt = message.DeletedAt
        };
    }

    private static ChatUserResponse MapChatUser(User user, string? contextLabel)
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

    private static void AddQuickContact(Dictionary<Guid, ChatUserResponse> users, User user, string contextLabel)
    {
        if (user.Status != UserStatus.Active || user.DeletedAt != null)
            return;

        if (!users.ContainsKey(user.Id))
            users[user.Id] = MapChatUser(user, contextLabel);
    }

    private static string NormalizeEmail(string email)
    {
        return (email ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static string NormalizeTitle(string? title, string fallback)
    {
        var normalized = title?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized.Length > 200 ? normalized[..200] : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
