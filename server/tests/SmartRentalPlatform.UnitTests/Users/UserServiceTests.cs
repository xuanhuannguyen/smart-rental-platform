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
    public async Task UpdateUserProfileAsync_ShouldLinkAvatarMediaAssetAndKeepCompatibilityUrl()
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
                AvatarUrl = "/api/media/public/public/avatars/2026/07/11/avatar.jpg",
                AvatarMediaAssetId = mediaAsset.Id
            });

        var updatedUser = _fixture.Context.Users.Single(x => x.Id == user.Id);
        var updatedAsset = _fixture.Context.MediaAssets.Single(x => x.Id == mediaAsset.Id);

        Assert.Equal(mediaAsset.Id, updatedUser.AvatarMediaAssetId);
        Assert.Equal("/api/media/public/public/avatars/2026/07/11/avatar.jpg", updatedUser.AvatarUrl);
        Assert.Equal(mediaAsset.Id, result.AvatarMediaAssetId);
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
                AvatarUrl = "/api/media/public/public/room-images/2026/07/11/room.jpg",
                AvatarMediaAssetId = mediaAsset.Id
            }));

        Assert.Equal("Media asset được chọn không phải avatar hợp lệ.", exception.Message);
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
