using System.Text;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Users;
using SmartRentalPlatform.Infrastructure.Media;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Media;

public class MediaAccessServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public MediaAccessServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task OpenReadAsync_ForAdminPrivateKycMedia_ShouldWriteAuditWithMetadata()
    {
        var adminId = Guid.NewGuid();
        _fixture.Context.Users.Add(new User
        {
            Id = adminId,
            Email = "admin-audit@test.com",
            NormalizedEmail = "ADMIN-AUDIT@TEST.COM",
            DisplayName = "Admin",
            Status = UserStatus.Active,
            OnboardingStatus = OnboardingStatus.Completed,
            UserRoles =
            [
                new UserRole
                {
                    UserId = adminId,
                    RoleId = (int)RoleName.Admin
                }
            ]
        });

        var mediaAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            BucketName = "local-media",
            ObjectKey = $"private/kyc-documents/{Guid.NewGuid():N}.jpg",
            OriginalFileName = "citizen-front.jpg",
            StoredFileName = "citizen-front.jpg",
            ContentType = "image/jpeg",
            FileSize = 100,
            Scope = MediaScope.KycDocument,
            Visibility = MediaVisibility.Private,
            Status = MediaStatus.Linked,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _fixture.Context.MediaAssets.Add(mediaAsset);
        await _fixture.Context.SaveChangesAsync();

        var service = new MediaAccessService(
            _fixture.Context,
            new FakeMediaStorageService(),
            new DefaultMediaPermissionService(_fixture.Context));

        var result = await service.OpenReadAsync(
            mediaAsset.Id,
            adminId,
            auditContext: new MediaAuditContext
            {
                Action = "View",
                IpAddress = "127.0.0.1",
                UserAgent = "unit-test",
                Reason = "review",
                MetadataJson = "{\"mode\":\"inline\"}"
            });

        Assert.Equal("image/jpeg", result.ContentType);
        await result.Stream.DisposeAsync();

        var audit = Assert.Single(_fixture.Context.MediaAuditLogs);
        Assert.Equal(mediaAsset.Id, audit.MediaAssetId);
        Assert.Equal(adminId, audit.ActorUserId);
        Assert.Equal("View", audit.Action);
        Assert.Equal("127.0.0.1", audit.IpAddress);
        Assert.Equal("unit-test", audit.UserAgent);
        Assert.Equal("review", audit.Reason);
        Assert.Equal("{\"mode\":\"inline\"}", audit.MetadataJson);
    }

    [Fact]
    public async Task OpenReadAsync_ForForbiddenPrivateMedia_ShouldWriteDeniedAudit()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var mediaAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerId,
            BucketName = "local-media",
            ObjectKey = $"private/avatars/{Guid.NewGuid():N}.png",
            OriginalFileName = "avatar.png",
            StoredFileName = "avatar.png",
            ContentType = "image/png",
            FileSize = 100,
            Scope = MediaScope.Avatar,
            Visibility = MediaVisibility.Private,
            Status = MediaStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _fixture.Context.MediaAssets.Add(mediaAsset);
        await _fixture.Context.SaveChangesAsync();

        var service = new MediaAccessService(
            _fixture.Context,
            new FakeMediaStorageService(),
            new DefaultMediaPermissionService(_fixture.Context));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.OpenReadAsync(
                mediaAsset.Id,
                outsiderId,
                auditContext: new MediaAuditContext
                {
                    Action = "View",
                    IpAddress = "10.0.0.1"
                }));

        var audit = Assert.Single(_fixture.Context.MediaAuditLogs);
        Assert.Equal("ViewDenied", audit.Action);
        Assert.Equal(outsiderId, audit.ActorUserId);
        Assert.Equal("10.0.0.1", audit.IpAddress);
    }

    private sealed class FakeMediaStorageService : IMediaStorageService
    {
        public Task<MediaStoredObjectResult> UploadAsync(
            MediaUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MediaStoredObjectResult
            {
                BucketName = "local-media",
                ObjectKey = request.ObjectKey,
                StoredFileName = request.ObjectKey
            });
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes($"media:{objectKey}")));
        }

        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<MediaObjectMetadataResult> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MediaObjectMetadataResult
            {
                ObjectKey = objectKey,
                ContentType = "image/jpeg",
                FileSize = 100
            });
        }

        public string GetBucketName()
        {
            return "local-media";
        }

        public Task<MediaUploadUrlResult> GetUploadUrlAsync(
            string objectKey,
            string contentType,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MediaUploadUrlResult
            {
                Url = $"/upload/{objectKey}",
                HttpMethod = "PUT",
                ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl)
            });
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"/media/{objectKey}");
        }
    }
}
