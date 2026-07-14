using Microsoft.AspNetCore.Http;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Users;
using SmartRentalPlatform.Contracts.Users.Requests;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Users;

public class UserServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();

    [Fact]
    public async Task UpdateUserProfileAsync_ShouldLinkAvatarMediaAssetAndResolveMediaAssetUrl()
    {
        var user = TestDataBuilder.BuildUser(email: "avatar-link@unit.test", displayName: "Avatar Link");
        var mediaAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            BucketName = "test-bucket",
            ObjectKey = "public/avatars/2026/07/11/avatar.jpg",
            OriginalFileName = "avatar.jpg",
            StoredFileName = "avatar.jpg",
            ContentType = "image/jpeg",
            FileSize = 1234,
            Scope = MediaScope.Avatar,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _fixture.Context.Users.Add(user);
        _fixture.Context.MediaAssets.Add(mediaAsset);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService(user.Id);

        var result = await service.UpdateUserProfileAsync(
            new UpdateUserProfileRequest
            {
                DisplayName = "Avatar Link Updated",
                AvatarMediaAssetId = mediaAsset.Id
            });

        var updatedUser = _fixture.Context.Users.Single(x => x.Id == user.Id);
        var updatedAsset = _fixture.Context.MediaAssets.Single(x => x.Id == mediaAsset.Id);

        Assert.Equal(mediaAsset.Id, updatedUser.AvatarMediaAssetId);
        Assert.Null(updatedUser.AvatarUrl);
        Assert.Equal(mediaAsset.Id, result.AvatarMediaAssetId);
        Assert.Equal($"/api/media/public/{mediaAsset.Id:D}", result.AvatarUrl);
        Assert.Equal(nameof(SmartRentalPlatform.Domain.Entities.Users.User), updatedAsset.LinkedEntityType);
        Assert.Equal(user.Id, updatedAsset.LinkedEntityId);
        Assert.Equal(MediaStatus.Linked, updatedAsset.Status);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_ShouldThrowBadRequest_WhenAvatarMediaAssetHasWrongScope()
    {
        var user = TestDataBuilder.BuildUser(email: "avatar-invalid@unit.test", displayName: "Avatar Invalid");
        var mediaAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            BucketName = "test-bucket",
            ObjectKey = "public/room-images/2026/07/11/room.jpg",
            OriginalFileName = "room.jpg",
            StoredFileName = "room.jpg",
            ContentType = "image/jpeg",
            FileSize = 1234,
            Scope = MediaScope.RoomImage,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _fixture.Context.Users.Add(user);
        _fixture.Context.MediaAssets.Add(mediaAsset);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService(user.Id);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateUserProfileAsync(
            new UpdateUserProfileRequest
            {
                DisplayName = "Avatar Invalid Updated",
                AvatarMediaAssetId = mediaAsset.Id
            }));

        Assert.Equal("Media asset được chọn không phải avatar hợp lệ.", exception.Message);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ShouldIgnoreLegacyRelativeAvatarUrl_WhenNoMediaAsset()
    {
        var user = TestDataBuilder.BuildUser(email: "avatar-legacy@unit.test", displayName: "Avatar Legacy");
        user.AvatarUrl = "/api/media/public/legacy-avatar-path";

        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService(user.Id);

        var result = await service.GetCurrentUserAsync();

        Assert.Null(result.AvatarUrl);
        Assert.Null(result.AvatarMediaAssetId);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ShouldPreserveExternalAvatarUrl_WhenNoMediaAsset()
    {
        var user = TestDataBuilder.BuildUser(email: "avatar-external@unit.test", displayName: "Avatar External");
        user.AvatarUrl = "https://example.test/avatar.jpg";

        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService(user.Id);

        var result = await service.GetCurrentUserAsync();

        Assert.Equal("https://example.test/avatar.jpg", result.AvatarUrl);
        Assert.Null(result.AvatarMediaAssetId);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ShouldRenderLinkedAvatarMediaAssetUrl_AndIgnoreLegacyAvatarUrl()
    {
        var user = TestDataBuilder.BuildUser(email: "avatar-render@unit.test", displayName: "Avatar Render");
        var avatarAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            BucketName = "test-bucket",
            ObjectKey = "public/avatars/2026/07/14/avatar-render.jpg",
            OriginalFileName = "avatar-render.jpg",
            StoredFileName = "avatar-render.jpg",
            ContentType = "image/jpeg",
            FileSize = 1234,
            Scope = MediaScope.Avatar,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Linked,
            LinkedEntityType = nameof(SmartRentalPlatform.Domain.Entities.Users.User),
            LinkedEntityId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        user.AvatarUrl = "/api/media/public/legacy-avatar-path";
        user.AvatarMediaAssetId = avatarAsset.Id;

        _fixture.Context.Users.Add(user);
        _fixture.Context.MediaAssets.Add(avatarAsset);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService(user.Id);

        var result = await service.GetCurrentUserAsync();

        Assert.Equal(avatarAsset.Id, result.AvatarMediaAssetId);
        Assert.Equal($"/api/media/public/{avatarAsset.Id:D}", result.AvatarUrl);
    }

    private UserService CreateService(Guid userId)
    {
        return new UserService(
            _fixture.Context,
            new FakeCurrentUserService(userId),
            new HttpContextAccessor());
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }

        public bool IsAuthenticated => true;

        public string? Email => null;

        public IReadOnlyCollection<string> Roles => Array.Empty<string>();
    }
}
