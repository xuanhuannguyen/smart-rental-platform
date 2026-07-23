using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.IntegrationTests.Infrastructure;
using Xunit;

namespace SmartRentalPlatform.IntegrationTests.Property;

public class RoomControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RoomControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetByRoomingHouse_ShouldReturnRooms_WhenUserIsAuthorized()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseResetHelper.ResetDatabaseAsync(context);

        var landlordId = Guid.NewGuid();
        var landlord = new User
        {
            Id = landlordId,
            Email = "landlord@example.com",
            NormalizedEmail = "LANDLORD@EXAMPLE.COM",
            DisplayName = "Landlord User",
            Status = UserStatus.Active
        };

        var province = new SmartRentalPlatform.Domain.Entities.Administrative.AdministrativeProvince { Code = "P1", Name = "Province 1" };
        var ward = new SmartRentalPlatform.Domain.Entities.Administrative.AdministrativeWard { Code = "W1", Name = "Ward 1", ProvinceCode = "P1" };
        context.AdministrativeProvinces.Add(province);
        context.AdministrativeWards.Add(ward);

        var house = new RoomingHouse
        {
            Id = Guid.NewGuid(),
            LandlordUserId = landlordId,
            Name = "Rooming House 1",
            AddressLine = "123 Street",
            WardCode = "W1",
            ProvinceCode = "P1",
            AddressDisplay = "123 Street, W1, P1",
            ApprovalStatus = RoomingHouseApprovalStatus.Approved,
            VisibilityStatus = RoomingHouseVisibilityStatus.Visible
        };

        var policy = new RentalPolicy
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = house.Id,
            DepositMonths = 2,
            MinRentalMonths = 6,
            MaxRentalMonths = 12,
            DefaultPaymentDay = 5,
            IsActive = true
        };

        var rule = new RoomingHouseRule
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = house.Id,
            MediaAssetId = Guid.NewGuid()
        };

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.RentalPolicies.Add(policy);
        context.RoomingHouseRules.Add(rule);
        await context.SaveChangesAsync();

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/rooming-houses/{house.Id}/rooms");
        requestMessage.Headers.Add("X-Test-User-Id", landlordId.ToString());
        requestMessage.Headers.Add("X-Test-User-Email", landlord.Email);
        requestMessage.Headers.Add("X-Test-User-Roles", "Landlord");

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<RoomResponse>>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenAreaIsInvalid()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseResetHelper.ResetDatabaseAsync(context);

        var landlordId = Guid.NewGuid();
        var landlord = new User
        {
            Id = landlordId,
            Email = "create-landlord@example.com",
            NormalizedEmail = "CREATE-LANDLORD@EXAMPLE.COM",
            DisplayName = "Create Landlord",
            Status = UserStatus.Active
        };
        var house = new RoomingHouse
        {
            Id = Guid.NewGuid(),
            LandlordUserId = landlordId,
            Name = "Create House",
            AddressLine = "123 Street",
            WardCode = "W1",
            ProvinceCode = "P1",
            AddressDisplay = "123 Street, W1, P1",
            ApprovalStatus = RoomingHouseApprovalStatus.Approved,
            VisibilityStatus = RoomingHouseVisibilityStatus.Visible
        };
        var policy = new RentalPolicy
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = house.Id,
            DepositMonths = 2,
            MinRentalMonths = 6,
            MaxRentalMonths = 12,
            DefaultPaymentDay = 5,
            IsActive = true
        };
        var rule = new RoomingHouseRule
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = house.Id,
            MediaAssetId = Guid.NewGuid()
        };
        var servicePrice = new RoomingHouseServicePrice
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = house.Id,
            IsActive = true
        };

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.RentalPolicies.Add(policy);
        context.RoomingHouseRules.Add(rule);
        context.RoomingHouseServicePrices.Add(servicePrice);
        await context.SaveChangesAsync();

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/rooming-houses/{house.Id}/rooms")
        {
            Content = JsonContent.Create(new
            {
                RoomNumber = "A101",
                Floor = 1,
                AreaM2 = 0,
                MaxOccupants = 2,
                IsTieredPricing = false
            })
        };
        requestMessage.Headers.Add("X-Test-User-Id", landlordId.ToString());
        requestMessage.Headers.Add("X-Test-User-Email", landlord.Email);
        requestMessage.Headers.Add("X-Test-User-Roles", "Landlord");

        var response = await _client.SendAsync(requestMessage);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenRoomDoesNotExist()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseResetHelper.ResetDatabaseAsync(context);

        var response = await _client.GetAsync($"/api/rooms/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPublicById_ShouldExcludeLegacyPropertyImagesWithoutMediaAssetId()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseResetHelper.ResetDatabaseAsync(context);

        var landlordId = Guid.NewGuid();
        var landlord = new User
        {
            Id = landlordId,
            Email = "public-room-landlord@example.com",
            NormalizedEmail = "PUBLIC-ROOM-LANDLORD@EXAMPLE.COM",
            DisplayName = "Public Room Landlord",
            Status = UserStatus.Active
        };

        var house = new RoomingHouse
        {
            Id = Guid.NewGuid(),
            LandlordUserId = landlordId,
            Name = "Public House",
            AddressLine = "456 Public Street",
            WardCode = "W1",
            ProvinceCode = "P1",
            AddressDisplay = "456 Public Street, W1, P1",
            ApprovalStatus = RoomingHouseApprovalStatus.Approved,
            VisibilityStatus = RoomingHouseVisibilityStatus.Visible
        };

        var room = new Room
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = house.Id,
            RoomNumber = "201",
            Floor = 2,
            AreaM2 = 24,
            MaxOccupants = 2,
            Status = RoomStatus.Available
        };

        var migratedAssetId = Guid.NewGuid();

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.PropertyImages.AddRange(
            new PropertyImage
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                ImageUrl = "/uploads/legacy-room-image.jpg",
                SortOrder = 0,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PropertyImage
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                MediaAssetId = migratedAssetId,
                ImageUrl = "/uploads/stale-room-image.jpg",
                SortOrder = 1,
                CreatedAt = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/public/rooms/{room.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RoomResponse>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var image = Assert.Single(body.Data!.Images);
        Assert.Equal(migratedAssetId, image.MediaAssetId);
        Assert.Equal(PublicMediaPathBuilder.Build(migratedAssetId), image.ImageUrl);
    }
}
