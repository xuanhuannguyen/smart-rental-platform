using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Contracts.Files;
using SmartRentalPlatform.Contracts.Files.Responses;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Infrastructure.Storage;

public sealed class MediaBackedFileStorageService : IFileStorageService
{
    private readonly IMediaObjectKeyFactory _mediaObjectKeyFactory;
    private readonly IMediaStorageService _mediaStorageService;
    private readonly IMediaAssetService _mediaAssetService;
    private readonly ICurrentUserService _currentUserService;

    public MediaBackedFileStorageService(
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        IMediaStorageService mediaStorageService,
        IMediaAssetService mediaAssetService,
        ICurrentUserService currentUserService)
    {
        _mediaObjectKeyFactory = mediaObjectKeyFactory;
        _mediaStorageService = mediaStorageService;
        _mediaAssetService = mediaAssetService;
        _currentUserService = currentUserService;
    }

    public Task<FileUploadResponse> UploadImageAsync(
        ImageUploadFile file,
        FileUploadScope scope,
        CancellationToken cancellationToken = default)
    {
        return UploadAsync(
            file.Content,
            file.FileName,
            file.ContentType,
            file.Length,
            scope,
            cancellationToken);
    }

    public Task<FileUploadResponse> UploadPdfAsync(
        ImageUploadFile file,
        FileUploadScope scope,
        CancellationToken cancellationToken = default)
    {
        return UploadAsync(
            file.Content,
            file.FileName,
            file.ContentType,
            file.Length,
            scope,
            cancellationToken);
    }

    public Task<FileUploadResponse> UploadPdfAsync(
        Stream content,
        string fileName,
        FileUploadScope scope,
        CancellationToken cancellationToken = default)
    {
        return UploadAsync(
            content,
            fileName,
            "application/pdf",
            null,
            scope,
            cancellationToken);
    }

    private async Task<FileUploadResponse> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        long? fileSize,
        FileUploadScope scope,
        CancellationToken cancellationToken)
    {
        var mediaScope = MapScope(scope);
        var visibility = MapVisibility(scope);
        var objectKeyResult = _mediaObjectKeyFactory.Create(mediaScope, visibility, fileName);
        var resolvedFileSize = fileSize ?? TryGetFileSize(content);

        MediaStoredObjectResult storedObject;
        Domain.Entities.Media.MediaAsset mediaAsset;
        try
        {
            storedObject = await _mediaStorageService.UploadAsync(
                new MediaUploadRequest
                {
                    Content = content,
                    OriginalFileName = fileName,
                    ContentType = contentType,
                    FileSize = resolvedFileSize,
                    ObjectKey = objectKeyResult.ObjectKey,
                    Visibility = visibility
                },
                cancellationToken);

            mediaAsset = await _mediaAssetService.CreateAsync(
                new CreateMediaAssetRequest
                {
                    OwnerUserId = _currentUserService.UserId,
                    BucketName = storedObject.BucketName,
                    ObjectKey = storedObject.ObjectKey,
                    OriginalFileName = fileName,
                    StoredFileName = storedObject.StoredFileName,
                    ContentType = contentType,
                    FileSize = resolvedFileSize,
                    Scope = mediaScope,
                    Visibility = visibility,
                    Status = MediaStatus.Uploaded
                },
                cancellationToken);
        }
        catch
        {
            if (await _mediaStorageService.ExistsAsync(objectKeyResult.ObjectKey, cancellationToken))
            {
                await _mediaStorageService.DeleteAsync(objectKeyResult.ObjectKey, cancellationToken);
            }

            throw;
        }

        return new FileUploadResponse
        {
            MediaAssetId = mediaAsset.Id,
            ObjectKey = storedObject.ObjectKey,
            Url = visibility == MediaVisibility.Public
                ? storedObject.PublicUrl ?? PublicMediaPathBuilder.Build(storedObject.ObjectKey)
                : PrivateMediaPathBuilder.Build(mediaAsset.Id)
        };
    }

    private static MediaScope MapScope(FileUploadScope scope)
    {
        return scope switch
        {
            FileUploadScope.RoomingHouse => MediaScope.RoomingHouseImage,
            FileUploadScope.Room => MediaScope.RoomImage,
            FileUploadScope.LegalDocument => MediaScope.RoomingHouseLegalDocument,
            FileUploadScope.Avatar => MediaScope.Avatar,
            FileUploadScope.HouseRule => MediaScope.RoomingHouseRulePdf,
            FileUploadScope.MeterReading => MediaScope.MeterReadingImage,
            FileUploadScope.ChatAttachment => MediaScope.ChatAttachment,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported file upload scope.")
        };
    }

    private static MediaVisibility MapVisibility(FileUploadScope scope)
    {
        return scope switch
        {
            FileUploadScope.LegalDocument => MediaVisibility.Private,
            FileUploadScope.HouseRule => MediaVisibility.Private,
            FileUploadScope.MeterReading => MediaVisibility.Private,
            FileUploadScope.ChatAttachment => MediaVisibility.Private,
            _ => MediaVisibility.Public
        };
    }

    private static long TryGetFileSize(Stream content)
    {
        return content.CanSeek ? Math.Max(0, content.Length - content.Position) : 0;
    }
}
