using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Infrastructure.Media;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Media;

public class MediaAssetServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public MediaAssetServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistMediaAssetMetadata()
    {
        var service = new MediaAssetService(_fixture.Context);
        var ownerUserId = Guid.NewGuid();

        var asset = await service.CreateAsync(new CreateMediaAssetRequest
        {
            OwnerUserId = ownerUserId,
            BucketName = "local-media",
            ObjectKey = "private/kyc-documents/2026/07/09/test.jpg",
            OriginalFileName = "citizen-card.jpg",
            StoredFileName = "stored.jpg",
            ContentType = "image/jpeg",
            FileSize = 12345,
            FileHash = "abc123",
            Scope = MediaScope.KycDocument,
            Visibility = MediaVisibility.Private,
            Status = MediaStatus.Uploaded,
            LinkedEntityType = "KycVerification"
        });

        Assert.NotEqual(Guid.Empty, asset.Id);
        Assert.Equal(ownerUserId, asset.OwnerUserId);
        Assert.Equal("local-media", asset.BucketName);
        Assert.Equal("private/kyc-documents/2026/07/09/test.jpg", asset.ObjectKey);
        Assert.Equal(MediaScope.KycDocument, asset.Scope);
        Assert.Equal(MediaVisibility.Private, asset.Visibility);
        Assert.Equal(MediaStatus.Uploaded, asset.Status);

        var stored = await service.GetByIdAsync(asset.Id);
        Assert.NotNull(stored);
        Assert.Equal("KycVerification", stored!.LinkedEntityType);
    }

    [Fact]
    public async Task MarkDeletedAsync_ShouldUpdateStatusAndDeletedAt()
    {
        var service = new MediaAssetService(_fixture.Context);
        var asset = await service.CreateAsync(new CreateMediaAssetRequest
        {
            BucketName = "local-media",
            ObjectKey = "public/room-images/2026/07/09/test.jpg",
            OriginalFileName = "room.jpg",
            StoredFileName = "stored.jpg",
            ContentType = "image/jpeg",
            FileSize = 99,
            Scope = MediaScope.RoomImage,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Linked
        });

        await service.MarkDeletedAsync(asset.Id);

        var stored = await service.GetByIdAsync(asset.Id);
        Assert.NotNull(stored);
        Assert.Equal(MediaStatus.Deleted, stored!.Status);
        Assert.NotNull(stored.DeletedAt);
    }
}
