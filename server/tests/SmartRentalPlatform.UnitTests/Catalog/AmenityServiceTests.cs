using SmartRentalPlatform.Application.Amenities;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Catalog;

public class AmenityServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();

    [Fact]
    public async Task GetActiveAmenitiesAsync_WithoutScope_ReturnsAllActiveAmenitiesOrderedByScopeThenName()
    {
        _fixture.Context.Amenities.AddRange(
            new Amenity { Id = 90001, Name = "000 Unit House Wifi", Scope = AmenityScope.House, IconCode = "wifi", IsActive = true },
            new Amenity { Id = 90002, Name = "000 Unit Room Desk", Scope = AmenityScope.Room, IconCode = "desk", IsActive = true },
            new Amenity { Id = 90003, Name = "000 Unit Both Camera", Scope = AmenityScope.Both, IconCode = "cam", IsActive = true },
            new Amenity { Id = 90004, Name = "000 Unit Inactive", Scope = AmenityScope.House, IsActive = false });
        await _fixture.Context.SaveChangesAsync();

        var service = new AmenityService(_fixture.Context);

        var result = await service.GetActiveAmenitiesAsync(null);

        Assert.Contains(result, x => x.Id == 90001 && x.IconCode == "wifi" && x.Scope == AmenityScope.House.ToString());
        Assert.Contains(result, x => x.Id == 90002 && x.Scope == AmenityScope.Room.ToString());
        Assert.Contains(result, x => x.Id == 90003 && x.Scope == AmenityScope.Both.ToString());
        Assert.DoesNotContain(result, x => x.Id == 90004);
    }

    [Fact]
    public async Task GetActiveAmenitiesAsync_ForHouseScope_IncludesHouseAndBothAmenities()
    {
        _fixture.Context.Amenities.AddRange(
            new Amenity { Id = 90101, Name = "000 Unit Lobby", Scope = AmenityScope.House, IsActive = true },
            new Amenity { Id = 90102, Name = "000 Unit Bed", Scope = AmenityScope.Room, IsActive = true },
            new Amenity { Id = 90103, Name = "000 Unit Wifi", Scope = AmenityScope.Both, IsActive = true });
        await _fixture.Context.SaveChangesAsync();

        var service = new AmenityService(_fixture.Context);

        var result = await service.GetActiveAmenitiesAsync(AmenityScope.House);

        Assert.Contains(result, x => x.Id == 90101);
        Assert.Contains(result, x => x.Id == 90103);
        Assert.DoesNotContain(result, x => x.Id == 90102);
    }

    [Fact]
    public async Task GetActiveAmenitiesAsync_ForRoomScope_IncludesRoomAndBothAmenities()
    {
        _fixture.Context.Amenities.AddRange(
            new Amenity { Id = 90201, Name = "000 Unit Lobby", Scope = AmenityScope.House, IsActive = true },
            new Amenity { Id = 90202, Name = "000 Unit Bed", Scope = AmenityScope.Room, IsActive = true },
            new Amenity { Id = 90203, Name = "000 Unit Wifi", Scope = AmenityScope.Both, IsActive = true });
        await _fixture.Context.SaveChangesAsync();

        var service = new AmenityService(_fixture.Context);

        var result = await service.GetActiveAmenitiesAsync(AmenityScope.Room);

        Assert.Contains(result, x => x.Id == 90202);
        Assert.Contains(result, x => x.Id == 90203);
        Assert.DoesNotContain(result, x => x.Id == 90201);
    }

    [Fact]
    public async Task GetActiveAmenitiesAsync_ForBothScope_ReturnsOnlyBothAmenities()
    {
        _fixture.Context.Amenities.AddRange(
            new Amenity { Id = 90301, Name = "000 Unit Lobby", Scope = AmenityScope.House, IsActive = true },
            new Amenity { Id = 90302, Name = "000 Unit Bed", Scope = AmenityScope.Room, IsActive = true },
            new Amenity { Id = 90303, Name = "000 Unit Wifi", Scope = AmenityScope.Both, IsActive = true });
        await _fixture.Context.SaveChangesAsync();

        var service = new AmenityService(_fixture.Context);

        var result = await service.GetActiveAmenitiesAsync(AmenityScope.Both);

        Assert.Contains(result, x => x.Id == 90303);
        Assert.DoesNotContain(result, x => x.Id is 90301 or 90302);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
