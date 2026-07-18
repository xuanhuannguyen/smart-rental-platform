using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Infrastructure.Media;

public static class MediaFileValidationPolicy
{
    private static readonly string[] ImageContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    private static readonly string[] ImageExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    ];

    private static readonly string[] PdfContentTypes =
    [
        "application/pdf"
    ];

    private static readonly string[] PdfExtensions =
    [
        ".pdf"
    ];

    public static void ValidateDeclaredUpload(
        MediaScope scope,
        string originalFileName,
        string contentType,
        long fileSize)
    {
        var rules = ResolveRules(scope);
        var normalizedContentType = NormalizeContentType(contentType);
        var extension = Path.GetExtension(originalFileName)?.ToLowerInvariant() ?? string.Empty;

        if (!rules.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                $"Định dạng file '{extension}' không được hỗ trợ cho scope {scope}.",
                new { scope = scope.ToString(), extension, allowedExtensions = rules.Extensions });
        }

        if (!rules.ContentTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                $"Content-Type '{normalizedContentType}' không hợp lệ cho scope {scope}.",
                new { scope = scope.ToString(), contentType = normalizedContentType, allowedContentTypes = rules.ContentTypes });
        }

        if (fileSize > rules.MaxFileSizeBytes)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                $"Kích thước file vượt quá giới hạn {rules.MaxFileSizeBytes} bytes cho scope {scope}.",
                new { scope = scope.ToString(), fileSize, maxFileSizeBytes = rules.MaxFileSizeBytes });
        }
    }

    public static void ValidateStoredObject(
        MediaScope scope,
        string originalFileName,
        string expectedContentType,
        long expectedFileSize,
        MediaObjectMetadataResult metadata)
    {
        ValidateDeclaredUpload(scope, originalFileName, expectedContentType, expectedFileSize);

        var normalizedExpectedContentType = NormalizeContentType(expectedContentType);
        var normalizedActualContentType = NormalizeContentType(metadata.ContentType);

        if (!string.Equals(normalizedExpectedContentType, normalizedActualContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Content-Type thực tế của object không khớp với metadata upload session.",
                new
                {
                    scope = scope.ToString(),
                    expectedContentType = normalizedExpectedContentType,
                    actualContentType = normalizedActualContentType
                });
        }

        if (metadata.FileSize <= 0)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Object trong storage có kích thước không hợp lệ.",
                new { scope = scope.ToString(), fileSize = metadata.FileSize });
        }

        if (metadata.FileSize != expectedFileSize)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Kích thước object thực tế không khớp với upload session.",
                new
                {
                    scope = scope.ToString(),
                    expectedFileSize,
                    actualFileSize = metadata.FileSize
                });
        }
    }

    public static void ValidateProxyUpload(
        MediaScope scope,
        string originalFileName,
        string expectedContentType,
        long expectedFileSize,
        string? incomingContentType,
        long? incomingFileSize)
    {
        ValidateDeclaredUpload(scope, originalFileName, expectedContentType, expectedFileSize);

        if (!string.IsNullOrWhiteSpace(incomingContentType))
        {
            var normalizedIncoming = NormalizeContentType(incomingContentType);
            var normalizedExpected = NormalizeContentType(expectedContentType);

            if (!string.Equals(normalizedIncoming, normalizedExpected, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Content-Type upload không khớp với upload session.",
                    new
                    {
                        scope = scope.ToString(),
                        expectedContentType = normalizedExpected,
                        actualContentType = normalizedIncoming
                    });
            }
        }

        if (incomingFileSize.HasValue && incomingFileSize.Value != expectedFileSize)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Kích thước upload không khớp với upload session.",
                new
                {
                    scope = scope.ToString(),
                    expectedFileSize,
                    actualFileSize = incomingFileSize.Value
                });
        }
    }

    private static MediaScopeRules ResolveRules(MediaScope scope)
    {
        return scope switch
        {
            MediaScope.RoomingHouseImage or
            MediaScope.RoomImage => new MediaScopeRules(ImageExtensions, ImageContentTypes, 10 * 1024 * 1024),
            MediaScope.RoomingHouseLegalDocument or
            MediaScope.KycDocument or
            MediaScope.MeterReadingImage => new MediaScopeRules(ImageExtensions, ImageContentTypes, 10 * 1024 * 1024),
            MediaScope.Avatar => new MediaScopeRules(ImageExtensions, ImageContentTypes, 5 * 1024 * 1024),
            MediaScope.ContractPdf or
            MediaScope.ContractAppendixPdf or
            MediaScope.RoomingHouseRulePdf => new MediaScopeRules(PdfExtensions, PdfContentTypes, 20 * 1024 * 1024),
            MediaScope.ChatAttachment => new MediaScopeRules(
                [.. ImageExtensions, .. PdfExtensions],
                [.. ImageContentTypes, .. PdfContentTypes],
                20 * 1024 * 1024),
            _ => throw new BadRequestException(ErrorCodes.ValidationError, $"Scope media '{scope}' chưa được cấu hình rule validate file.")
        };
    }

    private static string NormalizeContentType(string? contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? string.Empty
            : contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
    }

    private sealed record MediaScopeRules(
        IReadOnlyCollection<string> Extensions,
        IReadOnlyCollection<string> ContentTypes,
        long MaxFileSizeBytes);
}
