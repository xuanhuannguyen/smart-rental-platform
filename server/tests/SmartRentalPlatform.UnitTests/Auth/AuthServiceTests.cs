using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Auth;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Auth;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Auth;

public class AuthServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly FakePasswordService _passwordService;
    private readonly FakeTokenService _tokenService;
    private readonly FakeEmailSender _emailSender;
    private readonly FakeHttpContextAccessor _httpContextAccessor;

    public AuthServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _passwordService = new FakePasswordService();
        _tokenService = new FakeTokenService();
        _emailSender = new FakeEmailSender();
        _httpContextAccessor = new FakeHttpContextAccessor();
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateTenantAccount_WhenInputIsValid()
    {
        // Arrange
        var context = _fixture.Context;
        var role = new Role { Id = 1001, Name = RoleName.Tenant };
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _passwordService, _tokenService, _emailSender, _httpContextAccessor);
        var request = new RegisterRequest
        {
            Email = "newtenant@example.com",
            Password = "SecurePassword123!",
            DisplayName = "New Tenant",
            PhoneNumber = "0987654321"
        };

        _passwordService.HashPasswordFunc = (p) => "hashed_pwd";
        _tokenService.GenerateOtpFunc = (len) => "123456";
        _tokenService.HashTokenFunc = (t) => "hashed_otp";

        // Act
        var result = await authService.RegisterAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Email, result.Email);
        Assert.Equal(UserStatus.Active.ToString(), result.Status);
        Assert.Equal(OnboardingStatus.NeedProfileUpdate.ToString(), result.OnboardingStatus);

        var userInDb = await context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        Assert.NotNull(userInDb);
        Assert.Equal("hashed_pwd", userInDb!.PasswordHash);

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowConflictException_WhenEmailAlreadyExists()
    {
        // Arrange
        var context = _fixture.Context;
        var existingUser = TestDataBuilder.BuildUser(email: "existing@example.com");
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _passwordService, _tokenService, _emailSender, _httpContextAccessor);
        var request = new RegisterRequest
        {
            Email = "existing@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Existing User"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => authService.RegisterAsync(request, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        // Arrange
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "login@example.com");
        user.PasswordHash = "hashed_pwd";
        user.EmailConfirmed = true;

        var role = new Role { Id = 1002, Name = RoleName.Tenant };
        var userRole = new UserRole { UserId = user.Id, RoleId = role.Id, Role = role };
        user.UserRoles.Add(userRole);

        context.Users.Add(user);
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _passwordService, _tokenService, _emailSender, _httpContextAccessor);
        var request = new LoginRequest
        {
            Email = "login@example.com",
            Password = "CorrectPassword123!"
        };

        _passwordService.VerifyPasswordFunc = (h, p) => true;
        _tokenService.GenerateAccessTokenFunc = (u, r) => "access_token_123";
        _tokenService.GenerateRefreshTokenFunc = () => "refresh_token_123";

        // Act
        var result = await authService.LoginAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("access_token_123", result.AccessToken);
        Assert.Equal("refresh_token_123", result.RefreshToken);

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowForbiddenException_WhenAccountBanned()
    {
        // Arrange
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "banned@example.com", status: UserStatus.Banned);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _passwordService, _tokenService, _emailSender, _httpContextAccessor);
        var request = new LoginRequest
        {
            Email = "banned@example.com",
            Password = "AnyPassword123!"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => authService.LoginAsync(request, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task LoginAsync_ShouldLockAccount_WhenWrongPasswordReachedFiveTimes()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "lockme@example.com");
        user.PasswordHash = "hashed_pwd";
        user.EmailConfirmed = true;

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _passwordService, _tokenService, _emailSender, _httpContextAccessor);
        var request = new LoginRequest
        {
            Email = "lockme@example.com",
            Password = "WrongPassword123!"
        };

        _passwordService.VerifyPasswordFunc = (_, _) => false;

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => authService.LoginAsync(request, CancellationToken.None));
        }

        var userInDb = await context.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(5, userInDb.AccessFailedCount);
        Assert.NotNull(userInDb.LockoutEndAt);

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowForbiddenException_WhenEmailIsNotConfirmed()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "unconfirmed@example.com");
        user.PasswordHash = "hashed_pwd";
        user.EmailConfirmed = false;

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _passwordService, _tokenService, _emailSender, _httpContextAccessor);
        var request = new LoginRequest
        {
            Email = "unconfirmed@example.com",
            Password = "CorrectPassword123!"
        };

        _passwordService.VerifyPasswordFunc = (_, _) => true;

        await Assert.ThrowsAsync<ForbiddenException>(() => authService.LoginAsync(request, CancellationToken.None));

        context.ChangeTracker.Clear();
    }
}

#region Fakes for AuthService
public class FakePasswordService : IPasswordService
{
    public Func<string, string> HashPasswordFunc { get; set; } = _ => "hashed_pwd";
    public Func<string, string, bool> VerifyPasswordFunc { get; set; } = (_, _) => true;

    public string HashPassword(string password) => HashPasswordFunc(password);
    public bool VerifyPassword(string hashedPassword, string password) => VerifyPasswordFunc(hashedPassword, password);
}

public class FakeTokenService : ITokenService
{
    public Func<User, IReadOnlyCollection<string>, string> GenerateAccessTokenFunc { get; set; } = (_, _) => "access_token_123";
    public Func<string> GenerateRefreshTokenFunc { get; set; } = () => "refresh_token_123";
    public Func<int, string> GenerateOtpFunc { get; set; } = _ => "123456";
    public Func<string, string> HashTokenFunc { get; set; } = _ => "hashed_otp";

    public string GenerateAccessToken(User user, IReadOnlyCollection<string> roles) => GenerateAccessTokenFunc(user, roles);
    public string GenerateRefreshToken() => GenerateRefreshTokenFunc();
    public string GenerateOtp(int length = 6) => GenerateOtpFunc(length);
    public string HashToken(string token) => HashTokenFunc(token);
}

public class FakeEmailSender : IEmailSender
{
    public Task SendEmailVerificationOtpAsync(string email, string displayName, string otp, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendResetPasswordOtpAsync(string email, string displayName, string otp, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class FakeHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
#endregion
