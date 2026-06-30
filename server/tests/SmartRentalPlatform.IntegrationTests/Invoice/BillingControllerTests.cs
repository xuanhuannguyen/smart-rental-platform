using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.IntegrationTests.Infrastructure;
using Xunit;

namespace SmartRentalPlatform.IntegrationTests.Invoice;

public class BillingControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public BillingControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetBillingServiceTypes_ShouldReturnOk()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseResetHelper.ResetDatabaseAsync(context);

        var response = await _client.GetAsync("/api/billing/service-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
