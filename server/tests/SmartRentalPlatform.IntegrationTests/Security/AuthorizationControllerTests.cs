using System.Net;
using SmartRentalPlatform.IntegrationTests.Infrastructure;

namespace SmartRentalPlatform.IntegrationTests.Security;

public class AuthorizationControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthorizationControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProtectedEndpoint_ShouldReturnUnauthorized_WhenAuthenticationIsMissing()
    {
        using var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_ShouldReturnForbidden_WhenCallerIsTenant()
    {
        using var client = _factory.CreateAuthenticatedClient("Tenant");

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PublicHealthEndpoint_ShouldRemainAnonymous()
    {
        using var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
