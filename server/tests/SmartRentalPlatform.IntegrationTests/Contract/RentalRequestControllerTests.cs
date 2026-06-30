using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.IntegrationTests.Infrastructure;
using Xunit;

namespace SmartRentalPlatform.IntegrationTests.Contract;

public class RentalRequestControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RentalRequestControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenDesiredStartDateIsTooSoon()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseResetHelper.ResetDatabaseAsync(context);

        var response = await _client.PostAsJsonAsync($"/api/rooms/{Guid.NewGuid()}/rental-requests", new
        {
            DesiredStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            ExpectedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            ExpectedOccupantCount = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
