using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Rooms;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Property;

public class RoomCommandServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly FakeRoomQueryService _roomQueryService;

    public RoomCommandServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _roomQueryService = new FakeRoomQueryService();
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowBadRequestException_WhenRequiredServicePricesAreMissing()
    {
        // Arrange
        var context = _fixture.Context;
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var policy = TestDataBuilder.BuildRentalPolicy(house.Id);
        var rule = new RoomingHouseRule { Id = Guid.NewGuid(), RoomingHouseId = house.Id, PdfObjectKey = "key_rule" };

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.RentalPolicies.Add(policy);
        context.RoomingHouseRules.Add(rule);
        await context.SaveChangesAsync();

        var roomAccessService = new RoomAccessService(context);
        var service = new RoomCommandService(context, roomAccessService, _roomQueryService);
        var request = new CreateRoomRequest
        {
            RoomNumber = "101",
            Floor = 1,
            AreaM2 = 25,
            MaxOccupants = 2,
            IsTieredPricing = false,
            Description = "Test Room"
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(landlord.Id, house.Id, request, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateRoom_WhenDataIsValid()
    {
        // Arrange
        var context = _fixture.Context;
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var policy = TestDataBuilder.BuildRentalPolicy(house.Id);
        var rule = new RoomingHouseRule { Id = Guid.NewGuid(), RoomingHouseId = house.Id, PdfObjectKey = "key_rule" };
        var servicePrice = new RoomingHouseServicePrice { Id = Guid.NewGuid(), RoomingHouseId = house.Id, IsActive = true };

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.RentalPolicies.Add(policy);
        context.RoomingHouseRules.Add(rule);
        context.RoomingHouseServicePrices.Add(servicePrice);
        await context.SaveChangesAsync();

        var roomAccessService = new RoomAccessService(context);
        var service = new RoomCommandService(context, roomAccessService, _roomQueryService);
        var request = new CreateRoomRequest
        {
            RoomNumber = "102",
            Floor = 1,
            AreaM2 = 25,
            MaxOccupants = 2,
            IsTieredPricing = false,
            Description = "Test Room 102"
        };

        var expectedResponse = new RoomResponse
        {
            Id = Guid.NewGuid(),
            RoomNumber = "102",
            Floor = 1,
            AreaM2 = 25,
            MaxOccupants = 2,
            Status = RoomStatus.Hidden.ToString()
        };

        _roomQueryService.GetByIdAsyncFunc = (lId, rId) => Task.FromResult<RoomResponse?>(expectedResponse);

        // Act
        var result = await service.CreateAsync(landlord.Id, house.Id, request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("102", result.RoomNumber);

        var roomInDb = await context.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == "102" && r.RoomingHouseId == house.Id);
        Assert.NotNull(roomInDb);
        Assert.Equal(RoomStatus.Hidden, roomInDb!.Status);

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowBadRequestException_WhenAreaIsLessThanOrEqualZero()
    {
        var context = _fixture.Context;
        var landlord = TestDataBuilder.BuildUser(email: "area-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var policy = TestDataBuilder.BuildRentalPolicy(house.Id);
        var rule = new RoomingHouseRule { Id = Guid.NewGuid(), RoomingHouseId = house.Id, PdfObjectKey = "key_rule" };
        var servicePrice = new RoomingHouseServicePrice { Id = Guid.NewGuid(), RoomingHouseId = house.Id, IsActive = true };

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.RentalPolicies.Add(policy);
        context.RoomingHouseRules.Add(rule);
        context.RoomingHouseServicePrices.Add(servicePrice);
        await context.SaveChangesAsync();

        var roomAccessService = new RoomAccessService(context);
        var service = new RoomCommandService(context, roomAccessService, _roomQueryService);
        var request = new CreateRoomRequest
        {
            RoomNumber = "A01",
            Floor = 1,
            AreaM2 = 0,
            MaxOccupants = 2
        };

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(landlord.Id, house.Id, request, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowConflictException_WhenRoomNumberDuplicated()
    {
        var context = _fixture.Context;
        var landlord = TestDataBuilder.BuildUser(email: "dup-room-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var policy = TestDataBuilder.BuildRentalPolicy(house.Id);
        var rule = new RoomingHouseRule { Id = Guid.NewGuid(), RoomingHouseId = house.Id, PdfObjectKey = "key_rule" };
        var servicePrice = new RoomingHouseServicePrice { Id = Guid.NewGuid(), RoomingHouseId = house.Id, IsActive = true };
        var existingRoom = TestDataBuilder.BuildRoom(house.Id, roomNumber: "DUP-01");

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.RentalPolicies.Add(policy);
        context.RoomingHouseRules.Add(rule);
        context.RoomingHouseServicePrices.Add(servicePrice);
        context.Rooms.Add(existingRoom);
        await context.SaveChangesAsync();

        var roomAccessService = new RoomAccessService(context);
        var service = new RoomCommandService(context, roomAccessService, _roomQueryService);
        var request = new CreateRoomRequest
        {
            RoomNumber = "dup-01",
            Floor = 1,
            AreaM2 = 25,
            MaxOccupants = 2
        };

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(landlord.Id, house.Id, request, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowConflictException_WhenRoomIsOccupied()
    {
        var context = _fixture.Context;
        var landlord = TestDataBuilder.BuildUser(email: "occupied-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id, status: RoomStatus.Occupied);

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        var roomAccessService = new RoomAccessService(context);
        var service = new RoomCommandService(context, roomAccessService, _roomQueryService);
        var request = new UpdateRoomRequest
        {
            RoomNumber = "NEW-01",
            Floor = 1,
            AreaM2 = 30,
            MaxOccupants = 2
        };

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(landlord.Id, room.Id, request, CancellationToken.None));

        context.ChangeTracker.Clear();
    }
}

#region Fakes for RoomCommandService
public class FakeRoomQueryService : IRoomQueryService
{
    public Func<Guid, Guid, Task<RoomResponse?>> GetByIdAsyncFunc { get; set; } = (_, _) => Task.FromResult<RoomResponse?>(null);

    public Task<List<RoomResponse>> GetByRoomingHouseAsync(Guid landlordUserId, Guid roomingHouseId, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<RoomResponse>());

    public Task<RoomResponse?> GetByIdAsync(Guid landlordUserId, Guid roomId, CancellationToken cancellationToken = default)
        => GetByIdAsyncFunc(landlordUserId, roomId);

    public Task<RoomResponse?> GetPublicRoomByIdAsync(Guid roomId, CancellationToken cancellationToken = default)
        => Task.FromResult<RoomResponse?>(null);

    public Task<List<RoomResponse>> GetPublicAvailableRoomsAsync(Guid roomingHouseId, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<RoomResponse>());
}
#endregion
