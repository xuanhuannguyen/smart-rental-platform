using SmartRentalPlatform.Application.Administrative;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Enums.Common;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Catalog;

public class AdministrativeServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();

    [Fact]
    public async Task GetActiveProvincesAsync_ReturnsOnlyActiveProvincesOrderedByName()
    {
        _fixture.Context.AdministrativeProvinces.AddRange(
            new AdministrativeProvince { Code = "UT-P2", Name = "000 Unit Beta", Type = ProvinceType.Province, IsActive = true },
            new AdministrativeProvince { Code = "UT-P1", Name = "000 Unit Alpha", Type = ProvinceType.City, IsActive = true },
            new AdministrativeProvince { Code = "UT-P3", Name = "000 Unit Hidden", Type = ProvinceType.Province, IsActive = false });
        await _fixture.Context.SaveChangesAsync();

        var service = new AdministrativeService(_fixture.Context);

        var result = await service.GetActiveProvincesAsync();

        Assert.DoesNotContain(result, province => province.Code == "UT-P3");
        Assert.Collection(
            result.Take(2),
            province =>
            {
                Assert.Equal("UT-P1", province.Code);
                Assert.Equal("000 Unit Alpha", province.Name);
                Assert.Equal(ProvinceType.City.ToString(), province.Type);
            },
            province => Assert.Equal("UT-P2", province.Code));
    }

    [Fact]
    public async Task GetWardsByProvinceAsync_ReturnsEmptyList_WhenProvinceCodeIsBlank()
    {
        var service = new AdministrativeService(_fixture.Context);

        var result = await service.GetWardsByProvinceAsync("   ");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetWardsByProvinceAsync_ReturnsActiveWardsForProvinceOrderedByName()
    {
        _fixture.Context.AdministrativeProvinces.Add(new AdministrativeProvince
        {
            Code = "UT-P1",
            Name = "000 Unit Province",
            Type = ProvinceType.City,
            IsActive = true
        });
        _fixture.Context.AdministrativeWards.AddRange(
            new AdministrativeWard { Code = "UT-W2", ProvinceCode = "UT-P1", Name = "Beta", Type = WardType.Ward, IsActive = true },
            new AdministrativeWard { Code = "UT-W1", ProvinceCode = "UT-P1", Name = "Alpha", Type = WardType.Commune, IsActive = true },
            new AdministrativeWard { Code = "UT-W3", ProvinceCode = "UT-P1", Name = "Hidden", Type = WardType.Ward, IsActive = false },
            new AdministrativeWard { Code = "UT-W4", ProvinceCode = "UT-P2", Name = "Other", Type = WardType.Ward, IsActive = true });
        await _fixture.Context.SaveChangesAsync();

        var service = new AdministrativeService(_fixture.Context);

        var result = await service.GetWardsByProvinceAsync("UT-P1");

        Assert.Collection(
            result,
            ward =>
            {
                Assert.Equal("UT-W1", ward.Code);
                Assert.Equal("UT-P1", ward.ProvinceCode);
                Assert.Equal(WardType.Commune.ToString(), ward.Type);
            },
            ward => Assert.Equal("UT-W2", ward.Code));
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
