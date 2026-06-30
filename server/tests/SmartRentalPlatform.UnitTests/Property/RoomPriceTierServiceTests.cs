using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Rooms;
using SmartRentalPlatform.Contracts.RoomPriceTiers;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Property;

public class RoomPriceTierServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly FakeRoomPriceTierQueryService _roomQueryService;

    public RoomPriceTierServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _roomQueryService = new FakeRoomPriceTierQueryService();
    }

    [Fact]
    public async Task UpdatePriceTiersAsync_ShouldThrowConflictException_WhenRoomIsOccupied()
    {
        // Arrange
        var context = _fixture.Context;
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id, status: RoomStatus.Occupied);

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        var roomAccessService = new RoomAccessService(context);
        var service = new RoomPriceTierService(context, roomAccessService, _roomQueryService);
        var request = new UpdateRoomPriceTiersRequest
        {
            PriceTiers = new List<RoomPriceTierRequest>
            {
                new() { OccupantCount = 1, MonthlyRent = 3000000, IsActive = true }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdatePriceTiersAsync(landlord.Id, room.Id, request, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task UpdatePriceTiersAsync_ShouldReplacePriceTiers_WhenRoomIsEditable()
    {
        var context = _fixture.Context;
        var landlord = TestDataBuilder.BuildUser(email: "tier-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id, status: RoomStatus.Hidden);
        room.IsTieredPricing = true;
        room.MaxOccupants = 2;
        var oldTier = new RoomPriceTier
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            OccupantCount = 1,
            MonthlyRent = 2000000,
            IsActive = true
        };

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RoomPriceTiers.Add(oldTier);
        await context.SaveChangesAsync();

        var roomAccessService = new RoomAccessService(context);
        var service = new RoomPriceTierService(context, roomAccessService, _roomQueryService);
        var request = new UpdateRoomPriceTiersRequest
        {
            PriceTiers = new List<RoomPriceTierRequest>
            {
                new() { OccupantCount = 1, MonthlyRent = 3000000, IsActive = true },
                new() { OccupantCount = 2, MonthlyRent = 3500000, IsActive = true }
            }
        };

        await service.UpdatePriceTiersAsync(landlord.Id, room.Id, request, CancellationToken.None);

        var tiers = await context.RoomPriceTiers
            .Where(x => x.RoomId == room.Id)
            .OrderBy(x => x.OccupantCount)
            .ToListAsync();

        Assert.Equal(2, tiers.Count);
        Assert.Equal(3000000, tiers[0].MonthlyRent);
        Assert.Equal(3500000, tiers[1].MonthlyRent);

        context.ChangeTracker.Clear();
    }
}

#region Fakes for RoomPriceTierService
public class FakeRoomPriceTierQueryService : IRoomQueryService
{
    public Task<List<RoomResponse>> GetByRoomingHouseAsync(Guid landlordUserId, Guid roomingHouseId, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<RoomResponse>());

    public Task<RoomResponse?> GetByIdAsync(Guid landlordUserId, Guid roomId, CancellationToken cancellationToken = default)
        => Task.FromResult<RoomResponse?>(null);

    public Task<RoomResponse?> GetPublicRoomByIdAsync(Guid roomId, CancellationToken cancellationToken = default)
        => Task.FromResult<RoomResponse?>(null);

    public Task<List<RoomResponse>> GetPublicAvailableRoomsAsync(Guid roomingHouseId, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<RoomResponse>());
}
#endregion
