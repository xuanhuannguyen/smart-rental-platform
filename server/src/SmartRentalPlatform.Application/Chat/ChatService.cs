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

    public async Task<List<ConversationResponse>> GetConversationsAsync(Guid currentUserId, string? box = null, CancellationToken cancellationToken = default)
    {
        var status = box?.ToLower() == "pending"
            ? ConversationParticipantInboxStatus.Pending
            : ConversationParticipantInboxStatus.Main;

        var conversations = await BaseConversationQuery()
            .Where(x => x.Participants.Any(p => p.UserId == currentUserId && p.LeftAt == null && p.InboxStatus == status) && x.LastMessageAt != null)
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
            var hasConversationHistory = await context.ChatMessages
                .AsNoTracking()
                .AnyAsync(x => x.ConversationId == existing.Id, cancellationToken);
            var pMe = existing.Participants.FirstOrDefault(x => x.UserId == currentUserId);
            var pOther = existing.Participants.FirstOrDefault(x => x.UserId == request.OtherUserId);

            if (pMe != null)
            {
                pMe.LeftAt = null;
                pMe.InboxStatus = ConversationParticipantInboxStatus.Main;
                pMe.InboxStatusUpdatedAt = DateTimeOffset.UtcNow;
                pMe.InboxStatusUpdatedByUserId = currentUserId;
                pMe.AddedByUserId = currentUserId;
            }
            if (pOther != null)
            {
                pOther.LeftAt = null;
                if (pOther.InboxStatus != ConversationParticipantInboxStatus.Rejected)
                {
                    pOther.InboxStatus = hasConversationHistory
                        ? ConversationParticipantInboxStatus.Main
                        : ConversationParticipantInboxStatus.Pending;
                    pOther.InboxStatusUpdatedAt = DateTimeOffset.UtcNow;
                    pOther.InboxStatusUpdatedByUserId = currentUserId;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            return MapConversation(existing, currentUserId);
        }

        var now = DateTimeOffset.UtcNow;
        var creatorPart = CreateParticipant(currentUserId, ConversationParticipantRole.Member, ConversationParticipantSource.Manual, currentUserId, now);
        creatorPart.InboxStatus = ConversationParticipantInboxStatus.Main;

        var recipientPart = CreateParticipant(request.OtherUserId, ConversationParticipantRole.Member, ConversationParticipantSource.Manual, currentUserId, now);
        recipientPart.InboxStatus = ConversationParticipantInboxStatus.Pending;

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            DirectUserAId = userAId,
            DirectUserBId = userBId,
            CreatedByUserId = currentUserId,
            CreatedAt = now,
            UpdatedAt = now,
            Participants = { creatorPart, recipientPart }
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
            RoomingHouseId = request.RoomingHouseId,
            AvatarUrl = NormalizeOptional(request.AvatarUrl),
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

        if (request.Title is not null)
        {
            conversation.Title = NormalizeTitle(request.Title, conversation.Title ?? "Nhóm trò chuyện");
        }

        if (request.AvatarUrl is not null)
        {
            conversation.AvatarUrl = NormalizeOptional(request.AvatarUrl);
        }

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

    public async Task<List<ChatUserResponse>> SearchUsersByEmailAsync(Guid currentUserId, string email, Guid? roomingHouseId = null, CancellationToken cancellationToken = default)
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
        {
            var activeOwnerCount = conversation.Participants.Count(x => x.LeftAt == null && x.Role == ConversationParticipantRole.Owner);
            if (activeOwnerCount <= 1)
            {
                throw new BadRequestException(
                    "CHAT_OWNER_LEAVE_REQUIRES_TRANSFER",
                    "Bạn là trưởng nhóm duy nhất. Vui lòng trao quyền trưởng nhóm cho thành viên khác trước khi rời nhóm.");
            }
        }

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
        var currentParticipant = EnsureParticipant(currentUserId, conversation);

        limit = Math.Clamp(limit, 1, MaxMessagesLimit);
        var query = context.ChatMessages
            .AsNoTracking()
            .Include(x => x.Sender)
            .Where(x => x.ConversationId == conversationId && x.CreatedAt >= currentParticipant.JoinedAt);

        if (before.HasValue)
            query = query.Where(x => x.CreatedAt < before.Value);

        var messages = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return messages.OrderBy(x => x.CreatedAt).Select(x => MapMessage(x, currentUserId)).ToList();
    }

    public async Task<SendChatMessageResponse> SendMessageAsync(Guid currentUserId, Guid conversationId, SendChatMessageRequest request, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        if (conversation.IsClosed)
            throw new BadRequestException("CHAT_CONVERSATION_CLOSED", "Cuộc trò chuyện đã đóng.");

        var senderParticipant = EnsureParticipant(currentUserId, conversation);
        if (conversation.Type == ConversationType.Group && senderParticipant.LeftAt != null)
            throw new ForbiddenException("CHAT_FORBIDDEN", "Bạn không có quyền truy cập cuộc trò chuyện này.");

        if (senderParticipant.InboxStatus == ConversationParticipantInboxStatus.Pending)
        {
            throw new BadRequestException("CHAT_PENDING_APPROVAL", "Yêu cầu nhắn tin của bạn đang chờ phê duyệt. Bạn không thể gửi tin nhắn lúc này.");
        }

        if (conversation.Type == ConversationType.Direct)
        {
            var otherParticipant = conversation.Participants.FirstOrDefault(p => p.UserId != currentUserId);
            if (otherParticipant != null)
            {
                if (otherParticipant.InboxStatus == ConversationParticipantInboxStatus.Rejected || senderParticipant.InboxStatus == ConversationParticipantInboxStatus.Rejected)
                {
                    throw new BadRequestException("CHAT_REQUEST_REJECTED", "Cuộc trò chuyện này đã bị từ chối.");
                }
            }
        }

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
            FileUrl = NormalizeOptional(request.FileUrl),
            FileName = NormalizeOptional(request.FileName),
            FileContentType = NormalizeOptional(request.FileContentType),
            FileSize = request.FileSize,
            CreatedAt = now
        };

        if (senderParticipant.LeftAt != null)
        {
            senderParticipant.LeftAt = null;
            senderParticipant.JoinedAt = now;
        }

        context.ChatMessages.Add(message);
        conversation.LastMessageAt = now;
        conversation.LastMessagePreview = BuildPreview(messageType, message.Content);
        conversation.UpdatedAt = now;

        var recipientIds = new List<Guid>();
        foreach (var participant in conversation.Participants.Where(x => x.UserId != currentUserId))
        {
            if (participant.InboxStatus == ConversationParticipantInboxStatus.Rejected)
                continue;

            if (participant.LeftAt != null)
            {
                participant.LeftAt = null;
                participant.JoinedAt = now;
            }
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
        var response = MapMessage(savedMessage, currentUserId);
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
        if (type == ChatMessageType.File && string.IsNullOrWhiteSpace(request.FileUrl))
            throw new BadRequestException("CHAT_FILE_REQUIRED", "Vui lòng chọn tệp tin đính kèm.");
    }

    private static string BuildPreview(ChatMessageType type, string? content)
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

    private static string BuildNotificationBody(ChatMessage message)
    {
        return BuildPreview(message.MessageType, message.Content);
    }

    private ConversationResponse MapConversation(Conversation conversation, Guid currentUserId)
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
            if (conversation.CreatedByUserId == currentUserId)
            {
                lastMsgPreview = "Bạn đã tạo đoạn chat nhóm mới";
            }
            else
            {
                var creatorName = conversation.CreatedByUser?.DisplayName ?? "Chủ trọ";
                lastMsgPreview = $"{creatorName} đã tạo đoạn chat nhóm mới";
            }
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
            AvatarUrl = conversation.AvatarUrl,
            Participants = conversation.Participants
                .OrderBy(x => x.Role == ConversationParticipantRole.Owner ? 0 : 1)
                .ThenBy(x => x.User?.DisplayName ?? string.Empty)
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
            LeftAt = participant.LeftAt,
            InboxStatus = participant.InboxStatus.ToString()
        };
    }

    private ChatMessageResponse MapMessage(ChatMessage message, Guid currentUserId)
    {
        var isDeleted = message.DeletedAt != null;
        var content = message.Content;

        if (isDeleted)
        {
            content = "Tin nhắn đã bị thu hồi";
        }
        else if (message.MessageType == ChatMessageType.System && content != null && content.Contains("đã tạo đoạn chat nhóm mới"))
        {
            if (message.SenderId == currentUserId)
            {
                content = "Bạn đã tạo đoạn chat nhóm mới";
            }
            else
            {
                content = $"{message.Sender?.DisplayName ?? "Chủ trọ"} đã tạo đoạn chat nhóm mới";
            }
        }

        return new ChatMessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = message.Sender?.DisplayName ?? "Chủ trọ",
            MessageType = message.MessageType.ToString(),
            Content = content,
            ImageUrl = isDeleted ? null : message.ImageUrl,
            FileUrl = isDeleted ? null : message.FileUrl,
            FileName = isDeleted ? null : message.FileName,
            FileContentType = isDeleted ? null : message.FileContentType,
            FileSize = isDeleted ? null : message.FileSize,
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

    public async Task<SendChatMessageResponse> DeleteMessageAsync(Guid currentUserId, Guid conversationId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureActiveParticipant(currentUserId, conversation);

        var message = await context.ChatMessages
            .Include(x => x.Sender)
            .FirstOrDefaultAsync(x => x.Id == messageId && x.ConversationId == conversationId, cancellationToken)
            ?? throw new NotFoundException("CHAT_MESSAGE_NOT_FOUND", "Không tìm thấy tin nhắn.");

        if (message.SenderId != currentUserId)
            throw new ForbiddenException("CHAT_DELETE_FORBIDDEN", "Bạn chỉ có thể xóa tin nhắn của chính mình.");

        if (message.DeletedAt != null)
            throw new BadRequestException("CHAT_MESSAGE_ALREADY_DELETED", "Tin nhắn đã bị xóa từ trước.");

        var now = DateTimeOffset.UtcNow;
        message.DeletedAt = now;

        var latestMessage = await context.ChatMessages
            .Where(x => x.ConversationId == conversationId && x.Id != messageId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestMessage == null)
        {
            conversation.LastMessagePreview = "Tin nhắn đã bị xóa";
            conversation.LastMessageAt = now;
        }
        else if (latestMessage.CreatedAt < message.CreatedAt)
        {
            conversation.LastMessagePreview = latestMessage.DeletedAt != null ? "Tin nhắn đã bị xóa" : BuildPreview(latestMessage.MessageType, latestMessage.Content);
        }

        conversation.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        var recipientUserIds = conversation.Participants
            .Where(x => x.LeftAt == null && x.UserId != currentUserId)
            .Select(x => x.UserId)
            .ToList();

        return new SendChatMessageResponse
        {
            Message = MapMessage(message, currentUserId),
            Conversation = MapConversation(conversation, currentUserId),
            RecipientUserIds = recipientUserIds
        };
    }

    // ─── Implementations for new IChatService methods ───────────────────

    public async Task<List<ConversationResponse>> GetRecentConversationsAsync(Guid currentUserId, string? box, int take, int skip, CancellationToken cancellationToken = default)
    {
        var status = box?.ToLower() == "pending"
            ? ConversationParticipantInboxStatus.Pending
            : ConversationParticipantInboxStatus.Main;

        var conversations = await BaseConversationQuery()
            .Where(x => x.Participants.Any(p => p.UserId == currentUserId && p.LeftAt == null && p.InboxStatus == status) && x.LastMessageAt != null)
            .OrderByDescending(x => x.LastMessageAt ?? x.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return conversations.Select(x => MapConversation(x, currentUserId)).ToList();
    }

    public async Task<ChatCountsResponse> GetConversationCountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var mainUnreadCount = await context.ConversationParticipants
            .Where(x => x.UserId == userId && x.LeftAt == null && x.InboxStatus == ConversationParticipantInboxStatus.Main)
            .SumAsync(x => x.UnreadCount, cancellationToken);

        var pendingCount = await context.ConversationParticipants
            .Where(x => x.UserId == userId && x.LeftAt == null && x.InboxStatus == ConversationParticipantInboxStatus.Pending)
            .CountAsync(cancellationToken);

        return new ChatCountsResponse
        {
            MainUnreadCount = mainUnreadCount,
            PendingCount = pendingCount
        };
    }

    public async Task<ConversationResponse> GetConversationResponseForUserAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var conv = await LoadConversationAsync(conversationId, cancellationToken);
        return MapConversation(conv, userId);
    }

    public async Task<ConversationResponse> CreateDirectConversationByRoomingHouseAsync(Guid currentUserId, Guid roomingHouseId, CancellationToken cancellationToken = default)
    {
        var roomingHouse = await context.RoomingHouses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("ROOMING_HOUSE_NOT_FOUND", "Không tìm thấy khu trọ.");

        if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Approved ||
            roomingHouse.VisibilityStatus != RoomingHouseVisibilityStatus.Visible)
        {
            throw new BadRequestException("ROOMING_HOUSE_NOT_AVAILABLE", "Khu trọ hiện tại không công khai.");
        }

        if (roomingHouse.LandlordUserId == currentUserId)
        {
            throw new BadRequestException("CHAT_SELF_CHAT", "Không thể tạo cuộc trò chuyện với chính mình.");
        }

        return await CreateDirectConversationAsync(currentUserId, new CreateDirectConversationRequest
        {
            OtherUserId = roomingHouse.LandlordUserId
        }, cancellationToken);
    }

    public async Task<ConversationResponse> ContactLandlordAsync(Guid tenantUserId, Guid roomingHouseId, string initialMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(initialMessage))
            throw new BadRequestException("CHAT_EMPTY_MESSAGE", "Lời nhắn không được để trống.");

        var roomingHouse = await context.RoomingHouses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("ROOMING_HOUSE_NOT_FOUND", "Không tìm thấy khu trọ.");

        if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Approved)
            throw new BadRequestException("CHAT_HOUSE_NOT_APPROVED", "Khu trọ chưa được phê duyệt.");

        if (roomingHouse.VisibilityStatus != RoomingHouseVisibilityStatus.Visible)
            throw new BadRequestException("CHAT_HOUSE_HIDDEN", "Khu trọ đã bị ẩn.");

        if (roomingHouse.LandlordUserId == tenantUserId)
            throw new BadRequestException("CHAT_SELF_CONTACT", "Không thể tự gửi tin nhắn liên hệ cho khu trọ của chính mình.");

        var landlordUserId = roomingHouse.LandlordUserId;
        var (userAId, userBId) = NormalizePair(tenantUserId, landlordUserId);

        var existing = await BaseConversationQuery()
            .FirstOrDefaultAsync(x => x.Type == ConversationType.Direct && x.DirectUserAId == userAId && x.DirectUserBId == userBId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        Conversation conversation;

        if (existing is not null)
        {
            conversation = existing;
        }
        else
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                Type = ConversationType.Direct,
                DirectUserAId = userAId,
                DirectUserBId = userBId,
                CreatedByUserId = tenantUserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Conversations.Add(conversation);
        }

        conversation.RoomingHouseId = roomingHouseId;

        // Tenant participant
        var tenantPart = conversation.Participants.FirstOrDefault(p => p.UserId == tenantUserId);
        if (tenantPart == null)
        {
            tenantPart = new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = tenantUserId,
                Role = ConversationParticipantRole.Member,
                Source = ConversationParticipantSource.Manual,
                AddedByUserId = tenantUserId,
                JoinedAt = now,
                InboxStatus = ConversationParticipantInboxStatus.Main
            };
            conversation.Participants.Add(tenantPart);
        }
        else
        {
            tenantPart.LeftAt = null;
            tenantPart.InboxStatus = ConversationParticipantInboxStatus.Main;
        }

        // Landlord participant
        var landlordPart = conversation.Participants.FirstOrDefault(p => p.UserId == landlordUserId);
        if (landlordPart == null)
        {
            landlordPart = new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = landlordUserId,
                Role = ConversationParticipantRole.Member,
                Source = ConversationParticipantSource.Manual,
                AddedByUserId = tenantUserId,
                JoinedAt = now,
                InboxStatus = existing is null ? ConversationParticipantInboxStatus.Pending : ConversationParticipantInboxStatus.Main
            };
            conversation.Participants.Add(landlordPart);
        }
        else
        {
            landlordPart.LeftAt = null;
            if (existing is null)
            {
                landlordPart.InboxStatus = ConversationParticipantInboxStatus.Pending;
            }
        }

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderId = tenantUserId,
            MessageType = ChatMessageType.Text,
            Content = initialMessage,
            CreatedAt = now
        };
        context.ChatMessages.Add(message);

        conversation.LastMessageAt = now;
        conversation.LastMessagePreview = initialMessage;
        conversation.UpdatedAt = now;
        landlordPart.UnreadCount++;

        await context.SaveChangesAsync(cancellationToken);

        var refreshed = await LoadConversationAsync(conversation.Id, cancellationToken);
        return MapConversation(refreshed, tenantUserId);
    }

    public async Task<List<ChatUserResponse>> GetActiveTenantsByRoomingHouseAsync(Guid landlordUserId, Guid roomingHouseId, CancellationToken cancellationToken = default)
    {
        await EnsureLandlordAsync(landlordUserId, cancellationToken);

        var roomingHouse = await context.RoomingHouses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("ROOMING_HOUSE_NOT_FOUND", "Không tìm thấy khu trọ.");

        if (roomingHouse.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException("CHAT_LANDLORD_REQUIRED", "Không có quyền truy cập khu trọ này.");
        }

        var activeContracts = await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room)
            .Include(x => x.MainTenantUser).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.Occupants).ThenInclude(x => x.User).ThenInclude(x => x!.UserRoles).ThenInclude(x => x.Role)
            .Where(x => x.DeletedAt == null &&
                        x.Room.RoomingHouseId == roomingHouseId &&
                        x.Status == RentalContractStatus.Active)
            .ToListAsync(cancellationToken);

        var users = new Dictionary<Guid, ChatUserResponse>();
        foreach (var contract in activeContracts)
        {
            if (contract.MainTenantUser != null && contract.MainTenantUserId != landlordUserId)
            {
                AddQuickContact(users, contract.MainTenantUser, $"Phòng {contract.Room.RoomNumber}");
            }
            foreach (var occupant in contract.Occupants)
            {
                if (occupant.User != null && occupant.UserId.HasValue && occupant.UserId.Value != landlordUserId)
                {
                    AddQuickContact(users, occupant.User, $"Phòng {contract.Room.RoomNumber}");
                }
            }
        }

        return users.Values.OrderBy(x => x.DisplayName).ToList();
    }

    public async Task<List<ChatUserResponse>> GetEligibleMembersAsync(Guid currentUserId, Guid conversationId, Guid? roomingHouseId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureGroup(conversation);
        EnsureActiveParticipant(currentUserId, conversation);

        var targetHouseId = roomingHouseId ?? conversation.RoomingHouseId;
        List<ChatUserResponse> candidateUsers;

        if (targetHouseId.HasValue)
        {
            candidateUsers = await GetUsersInRoomingHouseAsync(targetHouseId.Value, currentUserId, cancellationToken);
        }
        else
        {
            var currentUser = await context.Users
                .Include(x => x.UserRoles).ThenInclude(x => x.Role)
                .FirstOrDefaultAsync(x => x.Id == currentUserId && x.DeletedAt == null, cancellationToken)
                ?? throw new NotFoundException("CHAT_USER_NOT_FOUND", "Không tìm thấy người dùng hiện tại.");

            if (HasRole(currentUser, RoleName.Landlord))
            {
                candidateUsers = await GetLandlordQuickContactsAsync(currentUserId, cancellationToken);
            }
            else
            {
                var landlords = await context.RoomingHouses
                    .AsNoTracking()
                    .Include(x => x.Landlord).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role)
                    .Where(x => x.LandlordUserId != currentUserId &&
                                x.Landlord.DeletedAt == null &&
                                x.Landlord.Status == UserStatus.Active)
                    .Select(x => x.Landlord)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                candidateUsers = landlords.Select(x => MapChatUser(x, "Chủ trọ")).ToList();
            }
        }

        var existingUserIds = conversation.Participants.Where(x => x.LeftAt == null).Select(x => x.UserId).ToHashSet();
        return candidateUsers.Where(x => !existingUserIds.Contains(x.UserId)).ToList();
    }

    public async Task<List<ChatRoomingHouseFilterResponse>> GetFilterRoomingHousesAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var currentUser = await context.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == currentUserId && x.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("CHAT_USER_NOT_FOUND", "Không tìm thấy người dùng.");

        if (HasRole(currentUser, RoleName.Landlord))
        {
            return await context.RoomingHouses
                .AsNoTracking()
                .Where(x => x.LandlordUserId == currentUserId && x.DeletedAt == null)
                .OrderBy(x => x.Name)
                .Select(x => new ChatRoomingHouseFilterResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    Address = x.AddressDisplay ?? string.Empty
                })
                .ToListAsync(cancellationToken);
        }

        return await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room).ThenInclude(x => x.RoomingHouse)
            .Where(x => x.DeletedAt == null &&
                        x.Status == RentalContractStatus.Active &&
                        (x.MainTenantUserId == currentUserId ||
                         x.Occupants.Any(co => co.UserId == currentUserId)))
            .Select(x => x.Room.RoomingHouse)
            .Where(x => x != null && x.DeletedAt == null)
            .Distinct()
            .OrderBy(x => x!.Name)
            .Select(x => new ChatRoomingHouseFilterResponse
            {
                Id = x!.Id,
                Name = x.Name,
                Address = x.AddressDisplay ?? string.Empty
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ConversationResponse> UpdateParticipantRoleAsync(Guid currentUserId, Guid conversationId, Guid targetUserId, ConversationParticipantRole role, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureGroup(conversation);
        EnsureOwner(currentUserId, conversation);

        if (role is not ConversationParticipantRole.Owner and not ConversationParticipantRole.Member)
        {
            throw new BadRequestException("CHAT_ROLE_INVALID", "Vai trò nhóm không hợp lệ.");
        }

        var participant = conversation.Participants.FirstOrDefault(x => x.UserId == targetUserId && x.LeftAt == null)
            ?? throw new NotFoundException("CHAT_PARTICIPANT_NOT_FOUND", "Thành viên không tồn tại trong nhóm.");

        var activeOwnerCount = conversation.Participants.Count(x => x.LeftAt == null && x.Role == ConversationParticipantRole.Owner);
        if (participant.Role == ConversationParticipantRole.Owner &&
            role == ConversationParticipantRole.Member &&
            activeOwnerCount <= 1)
        {
            throw new BadRequestException(
                "CHAT_ROLE_LAST_OWNER_INVALID",
                "Nhóm phải có ít nhất một trưởng nhóm.");
        }

        participant.Role = role;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return MapConversation(conversation, currentUserId);
    }

    public async Task<ConversationResponse> ClearConversationHistoryAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        var participant = conversation.Participants.FirstOrDefault(x => x.UserId == currentUserId)
            ?? throw new NotFoundException("CHAT_PARTICIPANT_NOT_FOUND", "Bạn không tham gia cuộc trò chuyện này.");

        var now = DateTimeOffset.UtcNow;
        participant.LeftAt = now;
        participant.JoinedAt = now;
        participant.UnreadCount = 0;

        await context.SaveChangesAsync(cancellationToken);
        return MapConversation(conversation, currentUserId);
    }

    public async Task<List<ConversationJoinRequestResponse>> GetJoinRequestsAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureGroup(conversation);
        EnsureAdminOrOwner(currentUserId, conversation);

        var requests = await context.ConversationJoinRequests
            .AsNoTracking()
            .Include(x => x.RequesterUser)
            .Include(x => x.ReviewedByUser)
            .Where(x => x.ConversationId == conversationId && x.Status == ConversationJoinRequestStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return requests.Select(x => new ConversationJoinRequestResponse
        {
            Id = x.Id,
            ConversationId = x.ConversationId,
            RequesterUserId = x.RequesterUserId,
            RequesterDisplayName = x.RequesterUser.DisplayName,
            RequesterEmail = x.RequesterUser.Email,
            RequesterAvatarUrl = x.RequesterUser.AvatarUrl,
            Status = x.Status.ToString(),
            CreatedAt = x.CreatedAt,
            ReviewedByUserId = x.ReviewedByUserId,
            ReviewedByDisplayName = x.ReviewedByUser?.DisplayName,
            ReviewedAt = x.ReviewedAt
        }).ToList();
    }

    public async Task<ConversationResponse> CreateJoinRequestAsync(Guid currentUserId, Guid conversationId, Guid? targetUserId = null, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureGroup(conversation);

        var requestUserId = targetUserId ?? currentUserId;

        var existingParticipant = conversation.Participants.FirstOrDefault(x => x.UserId == requestUserId && x.LeftAt == null);
        if (existingParticipant != null)
            throw new BadRequestException("CHAT_ALREADY_MEMBER", "Người dùng đã là thành viên của nhóm chat này.");

        if (!conversation.RequiresJoinApproval)
            throw new BadRequestException("CHAT_APPROVAL_NOT_REQUIRED", "Nhóm không yêu cầu duyệt thành viên, bạn có thể được thêm thẳng.");

        var callerParticipant = conversation.Participants.FirstOrDefault(x => x.UserId == currentUserId && x.LeftAt == null);
        if (callerParticipant == null && requestUserId != currentUserId)
        {
            throw new ForbiddenException("CHAT_FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.");
        }

        var hasPending = await context.ConversationJoinRequests
            .AnyAsync(x => x.ConversationId == conversationId && x.RequesterUserId == requestUserId && x.Status == ConversationJoinRequestStatus.Pending, cancellationToken);

        if (hasPending)
            throw new BadRequestException("CHAT_REQUEST_PENDING", "Đã có yêu cầu tham gia nhóm chat này đang chờ duyệt.");

        var request = new ConversationJoinRequest
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            RequesterUserId = requestUserId,
            Status = ConversationJoinRequestStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.ConversationJoinRequests.Add(request);
        await context.SaveChangesAsync(cancellationToken);

        return MapConversation(conversation, currentUserId);
    }

    public async Task<ConversationResponse> ApproveJoinRequestAsync(Guid currentUserId, Guid conversationId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureGroup(conversation);
        EnsureAdminOrOwner(currentUserId, conversation);

        var request = await context.ConversationJoinRequests
            .FirstOrDefaultAsync(x => x.Id == requestId && x.ConversationId == conversationId, cancellationToken)
            ?? throw new NotFoundException("CHAT_REQUEST_NOT_FOUND", "Không tìm thấy yêu cầu phê duyệt.");

        if (request.Status != ConversationJoinRequestStatus.Pending)
            throw new BadRequestException("CHAT_REQUEST_NOT_PENDING", "Yêu cầu này đã được xử lý.");

        var now = DateTimeOffset.UtcNow;
        request.Status = ConversationJoinRequestStatus.Approved;
        request.ReviewedByUserId = currentUserId;
        request.ReviewedAt = now;

        var participant = conversation.Participants.FirstOrDefault(x => x.UserId == request.RequesterUserId);
        if (participant is null)
        {
            conversation.Participants.Add(CreateParticipant(request.RequesterUserId, ConversationParticipantRole.Member, ConversationParticipantSource.Manual, currentUserId, now.AddSeconds(-5)));
        }
        else
        {
            participant.LeftAt = null;
            participant.JoinedAt = now.AddSeconds(-5);
            participant.AddedByUserId = currentUserId;
            participant.Source = ConversationParticipantSource.Manual;
        }

        conversation.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        return MapConversation(conversation, currentUserId);
    }

    public async Task RejectJoinRequestAsync(Guid currentUserId, Guid conversationId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureGroup(conversation);
        EnsureAdminOrOwner(currentUserId, conversation);

        var request = await context.ConversationJoinRequests
            .FirstOrDefaultAsync(x => x.Id == requestId && x.ConversationId == conversationId, cancellationToken)
            ?? throw new NotFoundException("CHAT_REQUEST_NOT_FOUND", "Không tìm thấy yêu cầu phê duyệt.");

        if (request.Status != ConversationJoinRequestStatus.Pending)
            throw new BadRequestException("CHAT_REQUEST_NOT_PENDING", "Yêu cầu này đã được xử lý.");

        request.Status = ConversationJoinRequestStatus.Rejected;
        request.ReviewedByUserId = currentUserId;
        request.ReviewedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConversationResponse> UpdateApprovalSettingsAsync(Guid currentUserId, Guid conversationId, bool requiresApproval, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureGroup(conversation);
        EnsureAdminOrOwner(currentUserId, conversation);

        conversation.RequiresJoinApproval = requiresApproval;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return MapConversation(conversation, currentUserId);
    }

    public async Task<(ConversationResponse Conversation, ChatMessageResponse? SystemMessage)> AcceptContactRequestAsync(Guid landlordUserId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await context.Conversations
            .Include(x => x.Participants)
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("CHAT_CONVERSATION_NOT_FOUND", "Không tìm thấy cuộc trò chuyện.");

        var landlordPart = conversation.Participants.FirstOrDefault(p => p.UserId == landlordUserId && p.LeftAt == null)
            ?? throw new ForbiddenException("CHAT_NOT_PARTICIPANT", "Bạn không tham gia cuộc trò chuyện này.");

        ChatMessage? systemMsg = null;

        if (landlordPart.InboxStatus == ConversationParticipantInboxStatus.Pending)
        {
            var now = DateTimeOffset.UtcNow;
            landlordPart.InboxStatus = ConversationParticipantInboxStatus.Main;
            landlordPart.InboxStatusUpdatedAt = now;
            landlordPart.InboxStatusUpdatedByUserId = landlordUserId;

            var landlordUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == landlordUserId, cancellationToken)
                ?? throw new NotFoundException("USER_NOT_FOUND", "Không tìm thấy người dùng.");

            systemMsg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                SenderId = landlordUserId,
                MessageType = ChatMessageType.System,
                Content = $"{landlordUser.DisplayName} đã chấp nhận yêu cầu nhắn tin",
                CreatedAt = now
            };
            context.ChatMessages.Add(systemMsg);

            conversation.LastMessageAt = now;
            conversation.LastMessagePreview = systemMsg.Content;
            conversation.UpdatedAt = now;

            await context.SaveChangesAsync(cancellationToken);
        }

        var refreshed = await LoadConversationAsync(conversation.Id, cancellationToken);
        var mappedConv = MapConversation(refreshed, landlordUserId);
        var mappedMsg = systemMsg != null ? MapMessage(systemMsg, landlordUserId) : null;

        return (mappedConv, mappedMsg);
    }

    public async Task<ConversationResponse> RejectContactRequestAsync(Guid landlordUserId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await context.Conversations
            .Include(x => x.Participants)
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("CHAT_CONVERSATION_NOT_FOUND", "Không tìm thấy cuộc trò chuyện.");

        var landlordPart = conversation.Participants.FirstOrDefault(p => p.UserId == landlordUserId && p.LeftAt == null)
            ?? throw new ForbiddenException("CHAT_NOT_PARTICIPANT", "Bạn không tham gia cuộc trò chuyện này.");

        var now = DateTimeOffset.UtcNow;
        landlordPart.InboxStatus = ConversationParticipantInboxStatus.Rejected;
        landlordPart.InboxStatusUpdatedAt = now;
        landlordPart.InboxStatusUpdatedByUserId = landlordUserId;

        await context.SaveChangesAsync(cancellationToken);

        var refreshed = await LoadConversationAsync(conversation.Id, cancellationToken);
        return MapConversation(refreshed, landlordUserId);
    }

    public async Task<ChatMessageResponse> UnsendMessageAsync(Guid currentUserId, Guid conversationId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await context.ChatMessages
            .Include(x => x.Conversation).ThenInclude(c => c.Participants)
            .Include(x => x.Sender)
            .FirstOrDefaultAsync(x => x.Id == messageId && x.ConversationId == conversationId, cancellationToken)
            ?? throw new NotFoundException("CHAT_MESSAGE_NOT_FOUND", "Không tìm thấy tin nhắn.");

        if (message.SenderId != currentUserId)
            throw new ForbiddenException("CHAT_UNSEND_FORBIDDEN", "Bạn chỉ có thể gỡ tin nhắn của chính mình.");

        if (message.DeletedAt != null)
            return MapMessage(message, currentUserId);

        var now = DateTimeOffset.UtcNow;
        message.DeletedAt = now;
        message.Content = "Tin nhắn đã bị thu hồi";
        message.ImageUrl = null;

        var conversation = message.Conversation;
        if (conversation.LastMessagePreview != null)
        {
            conversation.LastMessagePreview = "Tin nhắn đã bị thu hồi";
            conversation.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        return MapMessage(message, currentUserId);
    }

    public async Task<ChatMessageResponse> GetFileMessageAsync(Guid currentUserId, Guid conversationId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadConversationAsync(conversationId, cancellationToken);
        EnsureParticipant(currentUserId, conversation);

        var message = await context.ChatMessages
            .AsNoTracking()
            .Include(x => x.Sender)
            .FirstOrDefaultAsync(x => x.Id == messageId && x.ConversationId == conversationId, cancellationToken)
            ?? throw new NotFoundException("CHAT_MESSAGE_NOT_FOUND", "Không tìm thấy tin nhắn.");

        if (message.MessageType != ChatMessageType.File)
            throw new BadRequestException("CHAT_MESSAGE_NOT_FILE", "Tin nhắn này không phải là tệp đính kèm.");

        if (message.DeletedAt != null)
            throw new BadRequestException("CHAT_MESSAGE_DELETED", "Tin nhắn đã bị thu hồi.");

        return MapMessage(message, currentUserId);
    }

    private async Task<List<ChatUserResponse>> GetUsersInRoomingHouseAsync(Guid roomingHouseId, Guid excludeUserId, CancellationToken cancellationToken)
    {
        var activeContracts = await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room)
            .Include(x => x.MainTenantUser).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.Occupants).ThenInclude(x => x.User).ThenInclude(x => x!.UserRoles).ThenInclude(x => x.Role)
            .Where(x => x.DeletedAt == null &&
                        x.Room.RoomingHouseId == roomingHouseId &&
                        x.Status == RentalContractStatus.Active)
            .ToListAsync(cancellationToken);

        var users = new Dictionary<Guid, ChatUserResponse>();
        
        var house = await context.RoomingHouses
            .AsNoTracking()
            .Include(x => x.Landlord).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);
        if (house?.Landlord != null && house.LandlordUserId != excludeUserId)
        {
            AddQuickContact(users, house.Landlord, "Chủ trọ");
        }

        foreach (var contract in activeContracts)
        {
            if (contract.MainTenantUser != null && contract.MainTenantUserId != excludeUserId && contract.MainTenantUserId != house?.LandlordUserId)
            {
                AddQuickContact(users, contract.MainTenantUser, $"Phòng {contract.Room.RoomNumber}");
            }
            foreach (var occupant in contract.Occupants)
            {
                if (occupant.User != null && occupant.UserId.HasValue && occupant.UserId.Value != excludeUserId && occupant.UserId.Value != house?.LandlordUserId)
                {
                    AddQuickContact(users, occupant.User, $"Phòng {contract.Room.RoomNumber}");
                }
            }
        }

        return users.Values.ToList();
    }

    private static void EnsureAdminOrOwner(Guid currentUserId, Conversation conversation)
    {
        var participant = conversation.Participants.FirstOrDefault(x => x.UserId == currentUserId && x.LeftAt == null);
        if (participant?.Role != ConversationParticipantRole.Owner)
            throw new ForbiddenException("CHAT_OWNER_REQUIRED", "Chỉ trưởng nhóm mới có quyền thực hiện thao tác này.");
    }

    private static void EnsureOwnerOrAdmin(Guid currentUserId, Conversation conversation)
    {
        var participant = conversation.Participants.FirstOrDefault(x => x.UserId == currentUserId && x.LeftAt == null);
        if (participant?.Role != ConversationParticipantRole.Owner)
            throw new ForbiddenException("CHAT_OWNER_REQUIRED", "Chỉ trưởng nhóm mới có quyền thực hiện thao tác này.");
    }

    private static ConversationParticipant EnsureParticipant(Guid currentUserId, Conversation conversation)
    {
        return conversation.Participants.FirstOrDefault(x => x.UserId == currentUserId)
            ?? throw new ForbiddenException("CHAT_FORBIDDEN", "Bạn không có quyền truy cập cuộc trò chuyện này.");
    }

    public async Task<int> GetUnreadMessageCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.ConversationParticipants
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.LeftAt == null)
            .SumAsync(x => x.UnreadCount, cancellationToken);
    }
}
