using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.IntegrationTests.Infrastructure;
using Xunit;

namespace SmartRentalPlatform.IntegrationTests.Auth;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Register_ShouldReturnConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseResetHelper.ResetDatabaseAsync(context);

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            NormalizedEmail = "EXISTING@EXAMPLE.COM",
            DisplayName = "Existing User",
            Status = UserStatus.Active,
            PasswordHash = "hashed-password"
        };

        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        var request = new
        {
            Email = "existing@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenUserDoesNotExist()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseResetHelper.ResetDatabaseAsync(context);

        var request = new
        {
            Email = "nonexistent@example.com",
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ShouldReturnForbidden_WhenEmailIsNotConfirmed()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        await DatabaseResetHelper.ResetDatabaseAsync(context);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "unconfirmed.api@example.com",
            NormalizedEmail = "UNCONFIRMED.API@EXAMPLE.COM",
            DisplayName = "Unconfirmed API User",
            Status = UserStatus.Active,
            EmailConfirmed = false,
            PasswordHash = passwordService.HashPassword("Password123!")
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var request = new
        {
            Email = "unconfirmed.api@example.com",
            Password = "Password123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
