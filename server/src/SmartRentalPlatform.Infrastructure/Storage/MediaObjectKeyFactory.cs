using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Infrastructure.Storage;

public class MediaObjectKeyFactory : IMediaObjectKeyFactory
{
    private static readonly IReadOnlyDictionary<MediaScope, string> FolderByScope = new Dictionary<MediaScope, string>
    {
        [MediaScope.RoomingHouseImage] = "rooming-house-images",
        [MediaScope.RoomImage] = "room-images",
        [MediaScope.RoomingHouseLegalDocument] = "rooming-house-legal-documents",
        [MediaScope.KycDocument] = "kyc-documents",
        [MediaScope.ContractPdf] = "contract-pdfs",
        [MediaScope.ContractAppendixPdf] = "contract-appendix-pdfs",
        [MediaScope.MeterReadingImage] = "meter-reading-images",
        [MediaScope.ChatAttachment] = "chat-attachments",
        [MediaScope.RoomingHouseRulePdf] = "rooming-house-rule-pdfs",
        [MediaScope.Avatar] = "avatars"
    };

    public MediaObjectKeyResult Create(
        MediaScope scope,
        MediaVisibility visibility,
        string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var folder = FolderByScope[scope];
        var visibilityPrefix = visibility == MediaVisibility.Public ? "public" : "private";
        var today = DateTimeOffset.UtcNow;

        var objectKey = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{visibilityPrefix}/{folder}/{today:yyyy/MM/dd}/{storedFileName}");

        return new MediaObjectKeyResult
        {
            ObjectKey = objectKey,
            StoredFileName = storedFileName
        };
    }
}
