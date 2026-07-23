using SmartRentalPlatform.Application.Common.Media;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Kyc;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Application.Rooms;
using SmartRentalPlatform.Application.Users;
using SmartRentalPlatform.Contracts.Kyc.Requests;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Kyc;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.UnitTests.Auth;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.MediaMigration;

public class MediaMigrationRegressionTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();

    [Fact]
    public async Task KycSubmit_ShouldLinkUploadedMediaAssets_AndMoveUserToKycPending()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "media-migration-kyc@unit.test", displayName: "Media Migration Kyc");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var frontAsset = BuildKycMediaAsset(user.Id, "front-regression.jpg");
        var backAsset = BuildKycMediaAsset(user.Id, "back-regression.jpg");
        var selfieAsset = BuildKycMediaAsset(user.Id, "selfie-regression.jpg");

        context.MediaAssets.AddRange(frontAsset, backAsset, selfieAsset);
        await context.SaveChangesAsync();

        var vnptClient = new FakeVnptEkycClient
        {
            VerifyAsyncFunc = _ => Task.FromResult(new VnptEkycClientResult
            {
                SessionId = "media-migration-kyc-session",
                EkycResult = EkycResult.Passed.ToString(),
                OcrFullName = "PHAM VAN C",
                OcrCitizenId = "123456789012",
                OcrDateOfBirth = new DateTime(1999, 5, 20),
                OcrGender = "Male",
                OcrAddress = "Da Nang, Viet Nam",
                OcrConfidence = 0.98m,
                DocumentCheckResult = DocumentCheckResult.Valid.ToString(),
                FaceMatchScore = 0.94m,
                FaceMatchResult = FaceMatchResult.Matched.ToString(),
                LivenessResult = LivenessResult.Passed.ToString()
            })
        };

        var hashService = new FakeHashService
        {
            HashSha256HexFunc = _ => "media_migration_hash"
        };

        var sensitiveDataProtector = new FakeSensitiveDataProtector
        {
            EncryptFunc = value => $"protected::{value}"
        };

        var service = new KycService(
            context,
            vnptClient,
            hashService,
            new FakeMediaAccessService(),
            sensitiveDataProtector);

        var result = await service.SubmitAsync(
            user.Id,
            new SubmitKycRequest
            {
                DocumentType = KycDocumentType.CCCD.ToString(),
                SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
                FrontMediaAssetId = frontAsset.Id,
                BackMediaAssetId = backAsset.Id,
                SelfieMediaAssetId = selfieAsset.Id
            },
            CancellationToken.None);

        Assert.Equal(KycVerificationStatus.PendingAdminReview.ToString(), result.Status);
        Assert.Equal("Submission received. Your profile is pending admin review.", result.Message);

        context.ChangeTracker.Clear();

        var savedUser = await context.Users.SingleAsync(x => x.Id == user.Id);
        var savedKyc = await context.KycVerifications.SingleAsync(x => x.UserId == user.Id);
        var linkedAssets = await context.MediaAssets
            .Where(x => x.LinkedEntityType == nameof(SmartRentalPlatform.Domain.Entities.Users.KycVerification) &&
                        x.LinkedEntityId == savedKyc.Id)
            .OrderBy(x => x.OriginalFileName)
            .ToListAsync();

        Assert.Equal(OnboardingStatus.KycPending, savedUser.OnboardingStatus);
        Assert.Equal(frontAsset.Id, savedKyc.FrontMediaAssetId);
        Assert.Equal(backAsset.Id, savedKyc.BackMediaAssetId);
        Assert.Equal(selfieAsset.Id, savedKyc.SelfieMediaAssetId);
        Assert.Equal(3, linkedAssets.Count);
        Assert.All(linkedAssets, x =>
        {
            Assert.Equal(MediaScope.KycDocument, x.Scope);
            Assert.Equal(MediaVisibility.Private, x.Visibility);
            Assert.Equal(MediaStatus.Linked, x.Status);
        });
    }

    [Fact]
    public async Task KycSubmit_ShouldRejectMediaAssetsWithWrongScopeOrVisibility()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "media-migration-kyc-invalid@unit.test", displayName: "Media Migration Kyc Invalid");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var frontAsset = BuildKycMediaAsset(user.Id, "front-invalid.jpg");
        var backAsset = BuildKycMediaAsset(user.Id, "back-invalid.jpg");
        var selfieAsset = BuildKycMediaAsset(user.Id, "selfie-invalid.jpg");
        selfieAsset.Scope = MediaScope.Avatar;
        selfieAsset.Visibility = MediaVisibility.Public;

        context.MediaAssets.AddRange(frontAsset, backAsset, selfieAsset);
        await context.SaveChangesAsync();

        var service = new KycService(
            context,
            new FakeVnptEkycClient(),
            new FakeHashService(),
            new FakeMediaAccessService(),
            new FakeSensitiveDataProtector());

        var exception = await Assert.ThrowsAsync<KycBusinessException>(() =>
            service.SubmitAsync(
                user.Id,
                new SubmitKycRequest
                {
                    DocumentType = KycDocumentType.CCCD.ToString(),
                    SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
                    FrontMediaAssetId = frontAsset.Id,
                    BackMediaAssetId = backAsset.Id,
                    SelfieMediaAssetId = selfieAsset.Id
                },
                CancellationToken.None));

        Assert.Equal(ErrorCodes.ValidationError, exception.ErrorCode);
        Assert.Empty(context.KycVerifications.Where(x => x.UserId == user.Id));
        Assert.All(context.MediaAssets.Where(x => x.OwnerUserId == user.Id), x => Assert.Equal(MediaStatus.Uploaded, x.Status));
    }

    [Fact]
    public async Task CurrentUser_ShouldIgnoreLegacyRelativeAvatarUrl_WhenNoLinkedAvatarAssetExists()
    {
        var user = TestDataBuilder.BuildUser(email: "media-migration-avatar-legacy@unit.test", displayName: "Legacy Avatar User");
        user.AvatarUrl = "/api/media/public/legacy-avatar-path";

        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateUserService(user.Id);

        var result = await service.GetCurrentUserAsync();

        Assert.Null(result.AvatarMediaAssetId);
        Assert.Null(result.AvatarUrl);
    }

    [Fact]
    public async Task CurrentUser_ShouldPreserveExternalAvatarUrl_WhenNoLinkedAvatarAssetExists()
    {
        var user = TestDataBuilder.BuildUser(email: "media-migration-avatar-external@unit.test", displayName: "External Avatar User");
        user.AvatarUrl = "https://example.test/avatar.jpg";

        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateUserService(user.Id);

        var result = await service.GetCurrentUserAsync();

        Assert.Null(result.AvatarMediaAssetId);
        Assert.Equal("https://example.test/avatar.jpg", result.AvatarUrl);
    }

    [Fact]
    public async Task CurrentUser_ShouldRenderLinkedPublicAvatar_AndOverrideLegacyAvatarUrl()
    {
        var user = TestDataBuilder.BuildUser(email: "media-migration-avatar-linked@unit.test", displayName: "Linked Avatar User");
        var avatarAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            BucketName = "local-media",
            ObjectKey = "public/avatars/2026/07/14/avatar-linked.jpg",
            OriginalFileName = "avatar-linked.jpg",
            StoredFileName = "avatar-linked.jpg",
            ContentType = "image/jpeg",
            FileSize = 256,
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

        var service = CreateUserService(user.Id);

        var result = await service.GetCurrentUserAsync();

        Assert.Equal(avatarAsset.Id, result.AvatarMediaAssetId);
        Assert.Equal(PublicMediaPathBuilder.Build(avatarAsset.Id), result.AvatarUrl);
    }

    [Fact]
    public async Task PublicRoomQuery_ShouldExcludeLegacyPropertyImagesWithoutMediaAssetId()
    {
        var landlord = TestDataBuilder.BuildUser(email: "media-migration-room@unit.test", displayName: "Media Migration Room");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Approved);
        house.VisibilityStatus = RoomingHouseVisibilityStatus.Visible;
        var room = TestDataBuilder.BuildRoom(house.Id, roomNumber: "301", status: RoomStatus.Available);
        var mediaAssetId = Guid.NewGuid();

        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.Rooms.Add(room);
        _fixture.Context.MediaAssets.Add(new MediaAsset
        {
            Id = mediaAssetId,
            OwnerUserId = landlord.Id,
            BucketName = "local-media",
            ObjectKey = "public/room-images/room-301.jpg",
            OriginalFileName = "room-301.jpg",
            StoredFileName = "room-301.jpg",
            ContentType = "image/jpeg",
            FileSize = 512,
            Scope = MediaScope.RoomImage,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Linked,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _fixture.Context.PropertyImages.AddRange(
            new PropertyImage
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                ImageUrl = "/uploads/legacy-room-image.jpg",
                SortOrder = 0,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PropertyImage
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                MediaAssetId = mediaAssetId,
                ImageUrl = "/uploads/stale-room-image.jpg",
                SortOrder = 1,
                CreatedAt = DateTimeOffset.UtcNow
            });
        await _fixture.Context.SaveChangesAsync();

        var service = new RoomQueryService(
            _fixture.Context,
            new RoomAccessService(_fixture.Context),
            new MemoryCache(new MemoryCacheOptions()));

        var result = await service.GetPublicRoomByIdAsync(room.Id);

        Assert.NotNull(result);
        var image = Assert.Single(result!.Images);
        Assert.Equal(mediaAssetId, image.MediaAssetId);
        Assert.Equal(PublicMediaPathBuilder.Build(mediaAssetId), image.ImageUrl);
    }

    private UserService CreateUserService(Guid userId)
    {
        return new UserService(
            _fixture.Context,
            new FakeCurrentUserService(userId),
            new HttpContextAccessor());
    }

    private static MediaAsset BuildKycMediaAsset(Guid ownerUserId, string originalFileName)
    {
        return new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            BucketName = "local-media",
            ObjectKey = $"private/kyc-documents/2026/07/14/{originalFileName}",
            OriginalFileName = originalFileName,
            StoredFileName = originalFileName,
            ContentType = "image/jpeg",
            FileSize = 128,
            Scope = MediaScope.KycDocument,
            Visibility = MediaVisibility.Private,
            Status = MediaStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
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
