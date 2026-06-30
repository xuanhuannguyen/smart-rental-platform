using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.RentalRequests;
using SmartRentalPlatform.Contracts.Notifications.Responses;
using SmartRentalPlatform.Contracts.RentalRequests.Requests;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Notifications;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Contract;

public class RentalRequestServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly FakeNotificationService _notificationService;

    public RentalRequestServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _notificationService = new FakeNotificationService();
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowBadRequestException_WhenDesiredStartDateIsTooSoon()
    {
        // Arrange
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser();
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);
        var request = new CreateRentalRequestRequest
        {
            DesiredStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), // Less than 3 days
            ExpectedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            ExpectedOccupantCount = 1,
            TenantNote = "Please approve"
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(tenant.Id, room.Id, request, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreatePendingRequest_WhenInputIsValid()
    {
        // Arrange
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id, status: RoomStatus.Available);
        room.PriceTiers.Add(new RoomPriceTier { Id = Guid.NewGuid(), OccupantCount = 1, MonthlyRent = 3000000, IsActive = true });
        var policy = TestDataBuilder.BuildRentalPolicy(house.Id);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalPolicies.Add(policy);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);
        var request = new CreateRentalRequestRequest
        {
            DesiredStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)),
            ExpectedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4).AddMonths(6)),
            ExpectedOccupantCount = 1,
            TenantNote = "Please approve"
        };

        // Act
        var result = await service.CreateAsync(tenant.Id, room.Id, request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RentalRequestStatus.Pending.ToString(), result.Status);

        var reqInDb = await context.RentalRequests.FirstOrDefaultAsync(r => r.TenantUserId == tenant.Id && r.RoomId == room.Id);
        Assert.NotNull(reqInDb);
        Assert.Equal(RentalRequestStatus.Pending, reqInDb!.Status);

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowConflictException_WhenDuplicatePendingRequestExists()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "dup-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "dup-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id, status: RoomStatus.Available);
        var existingRequest = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(existingRequest);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);
        var request = new CreateRentalRequestRequest
        {
            DesiredStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)),
            ExpectedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4).AddMonths(6)),
            ExpectedOccupantCount = 1
        };

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(tenant.Id, room.Id, request, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CancelAsync_ShouldCancelRequest_WhenPendingAndOwnedByTenant()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "cancel-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "cancel-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var rentalRequest = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(rentalRequest);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);

        var result = await service.CancelAsync(tenant.Id, rentalRequest.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(RentalRequestStatus.Cancelled.ToString(), result!.Status);
        Assert.Equal(RentalRequestStatus.Cancelled, rentalRequest.Status);

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CancelAsync_ShouldThrowForbiddenException_WhenTenantDoesNotOwnRequest()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "owner@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "owner-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var rentalRequest = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(rentalRequest);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CancelAsync(Guid.NewGuid(), rentalRequest.Id, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowNotFoundException_WhenRoomDoesNotExist()
    {
        var context = _fixture.Context;
        var service = new RentalRequestService(context, _notificationService);
        var request = new CreateRentalRequestRequest
        {
            DesiredStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)),
            ExpectedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4).AddMonths(6)),
            ExpectedOccupantCount = 1
        };

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowForbiddenException_WhenTenantIsLandlord()
    {
        var context = _fixture.Context;
        var landlord = TestDataBuilder.BuildUser(email: "self-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id, status: RoomStatus.Available);

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);
        var request = new CreateRentalRequestRequest
        {
            DesiredStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)),
            ExpectedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4).AddMonths(6)),
            ExpectedOccupantCount = 1
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(landlord.Id, room.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowConflictException_WhenRentalPolicyIsMissing()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "nopolicy-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "nopolicy-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id, status: RoomStatus.Available);
        room.PriceTiers.Add(new RoomPriceTier { Id = Guid.NewGuid(), OccupantCount = 1, MonthlyRent = 3000000, IsActive = true });

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);
        var request = new CreateRentalRequestRequest
        {
            DesiredStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)),
            ExpectedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4).AddMonths(6)),
            ExpectedOccupantCount = 1
        };

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(tenant.Id, room.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowBadRequestException_WhenOccupantCountExceedsRoomCapacity()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "capacity-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "capacity-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id, status: RoomStatus.Available);
        room.MaxOccupants = 1;
        room.PriceTiers.Add(new RoomPriceTier { Id = Guid.NewGuid(), OccupantCount = 1, MonthlyRent = 3000000, IsActive = true });
        var policy = TestDataBuilder.BuildRentalPolicy(house.Id);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalPolicies.Add(policy);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);
        var request = new CreateRentalRequestRequest
        {
            DesiredStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)),
            ExpectedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4).AddMonths(6)),
            ExpectedOccupantCount = 2
        };

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(tenant.Id, room.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task RejectAsync_ShouldRejectPendingRequest_WhenReasonIsValid()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "reject-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "reject-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var rentalRequest = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(rentalRequest);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);

        var result = await service.RejectAsync(landlord.Id, rentalRequest.Id, new RejectRentalRequestRequest { RejectedReason = "Not suitable" }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(RentalRequestStatus.Rejected.ToString(), result!.Status);
        Assert.Equal("Not suitable", rentalRequest.RejectedReason);
    }

    [Fact]
    public async Task RejectAsync_ShouldThrowBadRequestException_WhenReasonIsBlank()
    {
        var context = _fixture.Context;
        var service = new RentalRequestService(context, _notificationService);

        await Assert.ThrowsAsync<BadRequestException>(() => service.RejectAsync(Guid.NewGuid(), Guid.NewGuid(), new RejectRentalRequestRequest { RejectedReason = " " }, CancellationToken.None));
    }

    [Fact]
    public async Task GetMyRequestsAsync_ShouldReturnOnlyTenantRequests()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "mine@example.com");
        var otherTenant = TestDataBuilder.BuildUser(email: "other@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "mine-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var mine = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var other = TestDataBuilder.BuildRentalRequest(otherTenant.Id, room.Id);

        context.Users.AddRange(tenant, otherTenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.AddRange(mine, other);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);

        var result = await service.GetMyRequestsAsync(tenant.Id, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(mine.Id, result[0].Id);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrowForbiddenException_WhenUserCannotViewRequest()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "view-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "view-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var rentalRequest = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(rentalRequest);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(Guid.NewGuid(), rentalRequest.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetIncomingRequestsAsync_ShouldReturnOnlyLandlordRequests()
    {
        var context = _fixture.Context;
        var landlord = TestDataBuilder.BuildUser(email: "incoming-landlord@example.com");
        var otherLandlord = TestDataBuilder.BuildUser(email: "incoming-other-landlord@example.com");
        var tenant = TestDataBuilder.BuildUser(email: "incoming-tenant@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var otherHouse = TestDataBuilder.BuildRoomingHouse(otherLandlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var otherRoom = TestDataBuilder.BuildRoom(otherHouse.Id);
        var mine = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var other = TestDataBuilder.BuildRentalRequest(tenant.Id, otherRoom.Id);

        context.Users.AddRange(landlord, otherLandlord, tenant);
        context.RoomingHouses.AddRange(house, otherHouse);
        context.Rooms.AddRange(room, otherRoom);
        context.RentalRequests.AddRange(mine, other);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);

        var result = await service.GetIncomingRequestsAsync(landlord.Id, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(mine.Id, result[0].Id);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenRequestDoesNotExist()
    {
        var context = _fixture.Context;
        var service = new RentalRequestService(context, _notificationService);

        var result = await service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ApproveAsync_ShouldThrowBadRequestException_WhenPaymentDeadlineIsMissing()
    {
        var context = _fixture.Context;
        var service = new RentalRequestService(context, _notificationService);

        await Assert.ThrowsAsync<BadRequestException>(() => service.ApproveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ApproveRentalRequestRequest { PaymentDeadlineAt = null },
            CancellationToken.None));
    }

    [Fact]
    public async Task RejectAsync_ShouldThrowForbiddenException_WhenLandlordDoesNotOwnRequest()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "reject-forbid-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "reject-forbid-landlord@example.com");
        var otherLandlord = TestDataBuilder.BuildUser(email: "reject-forbid-other@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var rentalRequest = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);

        context.Users.AddRange(tenant, landlord, otherLandlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(rentalRequest);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.RejectAsync(
            otherLandlord.Id,
            rentalRequest.Id,
            new RejectRentalRequestRequest { RejectedReason = "No" },
            CancellationToken.None));
    }

    [Fact]
    public async Task CancelAsync_ShouldThrowConflictException_WhenRequestIsNotPending()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "cancel-status-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "cancel-status-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var rentalRequest = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id, status: RentalRequestStatus.Accepted);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(rentalRequest);
        await context.SaveChangesAsync();

        var service = new RentalRequestService(context, _notificationService);

        await Assert.ThrowsAsync<ConflictException>(() => service.CancelAsync(tenant.Id, rentalRequest.Id, CancellationToken.None));
    }
}

#region Fakes for RentalRequestService
public class FakeNotificationService : INotificationService
{
    public Task CreateAsync(Guid userId, NotificationType type, string title, string body, string? referenceId = null, string? referenceType = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<List<NotificationResponse>> GetNotificationsAsync(Guid userId, int limit = 20, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<NotificationResponse>());

    public Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
#endregion
