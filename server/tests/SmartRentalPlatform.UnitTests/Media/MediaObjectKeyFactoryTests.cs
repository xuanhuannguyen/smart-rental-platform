using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Infrastructure.Storage;

namespace SmartRentalPlatform.UnitTests.Media;

public class MediaObjectKeyFactoryTests
{
    private readonly MediaObjectKeyFactory _factory = new();

    [Fact]
    public void Create_ForPublicRoomImage_ShouldUsePublicRoomImagesPrefix()
    {
        var result = _factory.Create(MediaScope.RoomImage, MediaVisibility.Public, "MyCover.JPG");

        Assert.StartsWith("public/room-images/", result.ObjectKey);
        Assert.EndsWith(".jpg", result.StoredFileName);
        Assert.DoesNotContain("MyCover", result.ObjectKey, StringComparison.Ordinal);
        Assert.Contains(result.StoredFileName, result.ObjectKey, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ForPrivateKycDocument_ShouldUsePrivateKycDocumentsPrefix()
    {
        var result = _factory.Create(MediaScope.KycDocument, MediaVisibility.Private, "citizen-card.png");

        Assert.StartsWith("private/kyc-documents/", result.ObjectKey);
        Assert.EndsWith(".png", result.StoredFileName);
        Assert.DoesNotContain("citizen-card", result.ObjectKey, StringComparison.Ordinal);
    }
}
