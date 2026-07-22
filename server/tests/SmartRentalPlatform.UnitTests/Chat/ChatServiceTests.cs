using SmartRentalPlatform.Application.Chat;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Chat.Requests;
using SmartRentalPlatform.Domain.Entities.Notifications;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Chat;
using SmartRentalPlatform.Domain.Enums.Notifications;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.Infrastructure.Caching;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Chat;

public sealed class ChatServiceTests : IDisposable
{
    private readonly TestDatabaseFixture fixture = new();
    private readonly InMemoryChatPresenceTracker presence = new();
    private readonly FakeNotificationService notifications = new();

    [Fact]
    public async Task CreateDirectConversationAsync_DeduplicatesReversedPair()
    {
        var landlord = SeedUser(RoleName.Landlord, "landlord@test.local");
        var tenant = SeedUser(RoleName.Tenant, "tenant@test.local");
        await fixture.Context.SaveChangesAsync();
        var service = CreateService();

        var first = await service.CreateDirectConversationAsync(tenant.Id, new CreateDirectConversationRequest { OtherUserId = landlord.Id });
        var second = await service.CreateDirectConversationAsync(landlord.Id, new CreateDirectConversationRequest { OtherUserId = tenant.Id });

        Assert.Equal(first.Id, second.Id);
        Assert.Single(fixture.Context.Conversations);
    }

    [Fact]
    public async Task CreateDirectConversationAsync_RejectsSelfChat()
    {
        var tenant = SeedUser(RoleName.Tenant, "tenant@test.local");
        await fixture.Context.SaveChangesAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CreateDirectConversationAsync(tenant.Id, new CreateDirectConversationRequest { OtherUserId = tenant.Id }));
    }

    [Fact]
    public async Task CreateDirectConversationAsync_AllowsLandlordToChatWithAnotherLandlord()
    {
        var firstLandlord = SeedUser(RoleName.Landlord, "first@test.local");
        var secondLandlord = SeedUser(RoleName.Landlord, "second@test.local");
        await fixture.Context.SaveChangesAsync();
        var service = CreateService();

        var conversation = await service.CreateDirectConversationAsync(
            firstLandlord.Id,
            new CreateDirectConversationRequest { OtherUserId = secondLandlord.Id });

        Assert.Equal("Direct", conversation.Type);
        Assert.Contains(conversation.Participants, x => x.UserId == firstLandlord.Id);
        Assert.Contains(conversation.Participants, x => x.UserId == secondLandlord.Id);
    }

    [Fact]
    public async Task CreateGroupConversationAsync_RejectsOutOfScopeUser()
    {
        var landlord = SeedUser(RoleName.Landlord, "landlord@test.local");
        var outsider = SeedUser(RoleName.Tenant, "outsider@test.local");
        await fixture.Context.SaveChangesAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateGroupConversationAsync(landlord.Id, new CreateGroupConversationRequest
            {
                Title = "Group",
                ParticipantUserIds = [outsider.Id]
            }));
    }

    [Fact]
    public async Task GetLandlordQuickContactsAsync_ReturnsScopedTenantsAndDeduplicates()
    {
        var landlord = SeedUser(RoleName.Landlord, "landlord@test.local");
        var tenant = SeedUser(RoleName.Tenant, "tenant@test.local");
        SeedActiveRental(landlord, tenant);
        SeedActiveRental(landlord, tenant);
        await fixture.Context.SaveChangesAsync();
        var service = CreateService();

        var result = await service.GetLandlordQuickContactsAsync(landlord.Id);

        var contact = Assert.Single(result);
        Assert.Equal(tenant.Id, contact.UserId);
    }

    [Fact]
    public async Task SendMessageAsync_IncrementsUnreadAndCreatesNotificationWhenRecipientAbsent()
    {
        var landlord = SeedUser(RoleName.Landlord, "landlord@test.local");
        var tenant = SeedUser(RoleName.Tenant, "tenant@test.local");
        await fixture.Context.SaveChangesAsync();
        var service = CreateService();
        var conversation = await service.CreateDirectConversationAsync(tenant.Id, new CreateDirectConversationRequest { OtherUserId = landlord.Id });

        await service.SendMessageAsync(tenant.Id, conversation.Id, new SendChatMessageRequest
        {
            MessageType = ChatMessageType.Text.ToString(),
            Content = "Xin chao"
        });

        var landlordParticipant = await fixture.Context.ConversationParticipants.FindAsync(conversation.Id, landlord.Id);
        Assert.Equal(1, landlordParticipant!.UnreadCount);
        var notification = Assert.Single(notifications.Created);
        Assert.Equal(landlord.Id, notification.UserId);
        Assert.Equal(NotificationType.NewChatMessage, notification.Type);
    }

    [Fact]
    public async Task SendMessageAsync_DoesNotCreateNotificationWhenRecipientViewingConversation()
    {
        var landlord = SeedUser(RoleName.Landlord, "landlord@test.local");
        var tenant = SeedUser(RoleName.Tenant, "tenant@test.local");
        await fixture.Context.SaveChangesAsync();
        var service = CreateService();
        var conversation = await service.CreateDirectConversationAsync(tenant.Id, new CreateDirectConversationRequest { OtherUserId = landlord.Id });
        presence.JoinConversation(conversation.Id, landlord.Id, "connection-1");

        await service.SendMessageAsync(tenant.Id, conversation.Id, new SendChatMessageRequest
        {
            MessageType = ChatMessageType.Text.ToString(),
            Content = "Xin chao"
        });

        Assert.Empty(notifications.Created);
    }

    [Fact]
    public async Task LeaveConversationAsync_MarksMemberLeftAndBlocksSending()
    {
        var landlord = SeedUser(RoleName.Landlord, "landlord@test.local");
        var tenant = SeedUser(RoleName.Tenant, "tenant@test.local");
        SeedActiveRental(landlord, tenant);
        await fixture.Context.SaveChangesAsync();
        var service = CreateService();
        var group = await service.CreateGroupConversationAsync(landlord.Id, new CreateGroupConversationRequest
        {
            Title = "Room group",
            ParticipantUserIds = [tenant.Id]
        });

        await service.LeaveConversationAsync(tenant.Id, group.Id);

        var participant = await fixture.Context.ConversationParticipants.FindAsync(group.Id, tenant.Id);
        Assert.NotNull(participant!.LeftAt);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.SendMessageAsync(tenant.Id, group.Id, new SendChatMessageRequest
            {
                MessageType = ChatMessageType.Text.ToString(),
                Content = "Still here"
            }));
    }

    [Fact]
    public void PresenceTracker_RemovesOnlyDisconnectedConnection()
    {
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        presence.JoinConversation(conversationId, userId, "tab-1");
        presence.JoinConversation(conversationId, userId, "tab-2");

        presence.RemoveConnection("tab-1");

        Assert.True(presence.IsUserViewingConversation(conversationId, userId));

        presence.RemoveConnection("tab-2");

        Assert.False(presence.IsUserViewingConversation(conversationId, userId));
    }

    private ChatService CreateService()
    {
        return new ChatService(fixture.Context, notifications, presence);
    }

    private User SeedUser(RoleName roleName, string email)
    {
        var role = fixture.Context.Roles.Local.FirstOrDefault(x => x.Name == roleName) ??
            fixture.Context.Roles.FirstOrDefault(x => x.Name == roleName);

        if (role is null)
        {
            role = new Role { Id = (int)roleName, Name = roleName };
            fixture.Context.Roles.Add(role);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = email.Split('@')[0],
            Status = UserStatus.Active,
            EmailConfirmed = true,
            UserRoles = { new UserRole { Role = role, RoleId = role.Id } }
        };

        fixture.Context.Users.Add(user);
        return user;
    }

    private void SeedActiveRental(User landlord, User tenant)
    {
        var house = new RoomingHouse
        {
            Id = Guid.NewGuid(),
            LandlordUserId = landlord.Id,
            Landlord = landlord,
            Name = "House",
            AddressLine = "Address",
            WardCode = "W",
            ProvinceCode = "P",
            AddressDisplay = "Address"
        };
        var room = new Room
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = house.Id,
            RoomingHouse = house,
            RoomNumber = "101"
        };
        var request = new RentalRequest
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            Room = room,
            TenantUserId = tenant.Id,
            TenantUser = tenant,
            ApprovedByLandlordId = landlord.Id,
            ApprovedByLandlord = landlord,
            Status = RentalRequestStatus.Accepted
        };
        var deposit = new RoomDeposit
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            Room = room,
            TenantUserId = tenant.Id,
            LandlordUserId = landlord.Id,
            RentalRequestId = request.Id,
            RentalRequest = request
        };
        var contract = new RentalContract
        {
            Id = Guid.NewGuid(),
            RentalRequestId = request.Id,
            RentalRequest = request,
            RoomDepositId = deposit.Id,
            RoomDeposit = deposit,
            RoomId = room.Id,
            Room = room,
            MainTenantUserId = tenant.Id,
            MainTenantUser = tenant,
            ContractNumber = Guid.NewGuid().ToString("N"),
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(6)),
            Status = RentalContractStatus.Active
        };

        fixture.Context.RoomingHouses.Add(house);
        fixture.Context.Rooms.Add(room);
        fixture.Context.RentalRequests.Add(request);
        fixture.Context.RoomDeposits.Add(deposit);
        fixture.Context.RentalContracts.Add(contract);
    }

    public void Dispose()
    {
        fixture.Dispose();
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<(Guid UserId, NotificationType Type, string Title, string Body, string? ReferenceId, string? ReferenceType)> Created { get; } = new();

        public Task CreateAsync(Guid userId, NotificationType type, string title, string body, string? referenceId = null, string? referenceType = null, CancellationToken cancellationToken = default)
        {
            Created.Add((userId, type, title, body, referenceId, referenceType));
            return Task.CompletedTask;
        }

        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<List<SmartRentalPlatform.Contracts.Notifications.Responses.NotificationResponse>> GetNotificationsAsync(Guid userId, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult(new List<SmartRentalPlatform.Contracts.Notifications.Responses.NotificationResponse>());
        public Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
