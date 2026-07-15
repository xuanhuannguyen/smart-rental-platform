using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Files;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Requests;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Responses;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class RoomingHouseRuleService : IRoomingHouseRuleService
{
    private sealed record LinkedMediaAssetResolution(Guid MediaAssetId);

    private readonly IAppDbContext context;
    private readonly IFileStorageService fileStorageService;

    public RoomingHouseRuleService(
        IAppDbContext context,
        IFileStorageService fileStorageService)
    {
        this.context = context;
        this.fileStorageService = fileStorageService;
    }

    public async Task<RoomingHouseRuleResponse?> GetRuleAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        CancellationToken cancellationToken = default)
    {
        var roomingHouse = await context.RoomingHouses
            .AsNoTracking()
            .Include(x => x.HouseRule)
            .FirstOrDefaultAsync(
                x => x.Id == roomingHouseId &&
                     x.LandlordUserId == landlordUserId &&
                     x.DeletedAt == null,
                cancellationToken);

        if (roomingHouse is null)
        {
            throw new NotFoundException(
                ErrorCodes.HouseNotFound,
                "Không tìm thấy khu trọ.",
                new { roomingHouseId });
        }

        return roomingHouse.HouseRule is null ? null : ToResponse(roomingHouse.HouseRule);
    }

    public async Task<RoomingHouseRuleResponse> UpsertRuleAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        UpsertRoomingHouseRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var roomingHouse = await context.RoomingHouses
            .Include(x => x.HouseRule)
            .FirstOrDefaultAsync(
                x => x.Id == roomingHouseId &&
                     x.LandlordUserId == landlordUserId &&
                     x.DeletedAt == null,
                cancellationToken);

        if (roomingHouse is null)
        {
            throw new NotFoundException(
                ErrorCodes.HouseNotFound,
                "Không tìm thấy khu trọ.",
                new { roomingHouseId });
        }

        var requestedSourceType = ParseSourceType(request.SourceType);
        ValidateSourceTransition(roomingHouse.HouseRule, requestedSourceType);
        ValidateRequest(requestedSourceType, request);

        var now = DateTimeOffset.UtcNow;
        var rule = roomingHouse.HouseRule;
        if (rule is null)
        {
            rule = new RoomingHouseRule
            {
                Id = Guid.NewGuid(),
                RoomingHouseId = roomingHouseId,
                SourceType = requestedSourceType,
                CreatedAt = now
            };
            context.RoomingHouseRules.Add(rule);
        }

        rule.SourceType = requestedSourceType;
        if (requestedSourceType == RoomingHouseRuleSourceType.PdfUpload)
        {
            var uploadedRulePdf = await EnsureRuleMediaAssetAsync(
                roomingHouseId,
                landlordUserId,
                request.PdfMediaAssetId,
                rule.MediaAssetId,
                now,
                cancellationToken);
            rule.MediaAssetId = uploadedRulePdf.MediaAssetId;
            ClearFormFields(rule);
        }
        else
        {
            ApplyFormFields(rule, request);
            await using var pdfStream = RoomingHouseRulePdfGenerator.Generate(roomingHouse, request);
            var uploaded = await fileStorageService.UploadPdfAsync(
                pdfStream,
                $"house-rule-{roomingHouseId:N}.pdf",
                FileUploadScope.HouseRule,
                cancellationToken);
            var generatedRulePdf = await EnsureRuleMediaAssetAsync(
                roomingHouseId,
                landlordUserId,
                uploaded.MediaAssetId,
                rule.MediaAssetId,
                now,
                cancellationToken);
            rule.MediaAssetId = generatedRulePdf.MediaAssetId;
        }

        rule.UpdatedAt = now;
        roomingHouse.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        return ToResponse(rule);
    }

    public async Task<System.IO.Stream> PreviewRuleAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        UpsertRoomingHouseRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var roomingHouse = await context.RoomingHouses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == roomingHouseId &&
                     x.LandlordUserId == landlordUserId &&
                     x.DeletedAt == null,
                cancellationToken);

        if (roomingHouse is null)
        {
            throw new NotFoundException(
                ErrorCodes.HouseNotFound,
                "Không tìm thấy khu trọ.",
                new { roomingHouseId });
        }

        var requestedSourceType = ParseSourceType(request.SourceType);
        if (requestedSourceType == RoomingHouseRuleSourceType.PdfUpload)
        {
            throw new BadRequestException(
                ErrorCodes.HouseRuleInvalid,
                "Chỉ hỗ trợ xem trước luật khu trọ được tạo từ form.",
                new { field = nameof(request.SourceType) });
        }

        ValidateRequest(requestedSourceType, request);

        return RoomingHouseRulePdfGenerator.Generate(roomingHouse, request);
    }

    private async Task<LinkedMediaAssetResolution> EnsureRuleMediaAssetAsync(
        Guid roomingHouseId,
        Guid ownerUserId,
        Guid? requestedMediaAssetId,
        Guid? existingMediaAssetId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!requestedMediaAssetId.HasValue)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "PDF luật khu trọ phải gửi mediaAssetId.",
                new { field = nameof(requestedMediaAssetId) });
        }

        var mediaAsset = await context.MediaAssets
            .FirstOrDefaultAsync(x => x.Id == requestedMediaAssetId.Value, cancellationToken);

        if (mediaAsset is null)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset được chọn không tồn tại.",
                new { mediaAssetId = requestedMediaAssetId.Value });
        }

        EnsureRuleAssetIsReusable(mediaAsset, ownerUserId);

        if (existingMediaAssetId.HasValue && existingMediaAssetId.Value != mediaAsset.Id)
        {
            var currentLinkedAsset = await context.MediaAssets
                .FirstOrDefaultAsync(x => x.Id == existingMediaAssetId.Value, cancellationToken);

            if (currentLinkedAsset is not null)
            {
                currentLinkedAsset.LinkedEntityType = null;
                currentLinkedAsset.LinkedEntityId = null;
                currentLinkedAsset.Status = MediaStatus.Deleted;
                currentLinkedAsset.DeletedAt = now;
                currentLinkedAsset.UpdatedAt = now;
            }
        }

        mediaAsset.OwnerUserId = ownerUserId;
        mediaAsset.Scope = MediaScope.RoomingHouseRulePdf;
        mediaAsset.Visibility = MediaVisibility.Public;
        mediaAsset.Status = MediaStatus.Linked;
        mediaAsset.LinkedEntityType = nameof(RoomingHouseRule);
        mediaAsset.LinkedEntityId = roomingHouseId;
        mediaAsset.DeletedAt = null;
        mediaAsset.UpdatedAt = now;

        return new LinkedMediaAssetResolution(mediaAsset.Id);
    }

    private static RoomingHouseRuleSourceType ParseSourceType(string sourceType)
    {
        if (!Enum.TryParse<RoomingHouseRuleSourceType>(sourceType, ignoreCase: true, out var parsed))
        {
            throw new BadRequestException(
                ErrorCodes.HouseRuleInvalid,
                "Nguồn tạo luật khu trọ không hợp lệ.",
                new { field = nameof(UpsertRoomingHouseRuleRequest.SourceType) });
        }

        return parsed;
    }

    private static void ValidateSourceTransition(
        RoomingHouseRule? existingRule,
        RoomingHouseRuleSourceType requestedSourceType)
    {
        if (existingRule is null || existingRule.SourceType == requestedSourceType)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.HouseRuleInvalid,
            existingRule.SourceType == RoomingHouseRuleSourceType.PdfUpload
                ? "Luật khu trọ đã tạo bằng PDF chỉ có thể thay bằng PDF khác."
                : "Luật khu trọ đã tạo bằng form chỉ có thể chỉnh bằng form.",
            new
            {
                currentSourceType = existingRule.SourceType.ToString(),
                requestedSourceType = requestedSourceType.ToString()
            });
    }

    private static void ValidateRequest(
        RoomingHouseRuleSourceType sourceType,
        UpsertRoomingHouseRuleRequest request)
    {
        if (sourceType == RoomingHouseRuleSourceType.PdfUpload)
        {
            if (!request.PdfMediaAssetId.HasValue)
            {
                throw new BadRequestException(
                    ErrorCodes.HouseRuleInvalid,
                    "Vui lòng tải PDF luật khu trọ.",
                    new { field = nameof(request.PdfMediaAssetId) });
            }

            return;
        }

        if (!GetFormValues(request).Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            throw new BadRequestException(
                ErrorCodes.HouseRuleInvalid,
                "Vui lòng nhập ít nhất một nội dung luật khu trọ.",
                new { field = "form" });
        }
    }

    private static IEnumerable<string?> GetFormValues(UpsertRoomingHouseRuleRequest request)
    {
        yield return request.GeneralRules;
        yield return request.QuietHours;
        yield return request.SecurityPolicy;
        yield return request.CleaningPolicy;
        yield return request.GuestPolicy;
        yield return request.ParkingPolicy;
        yield return request.UtilityPolicy;
        yield return request.DamageCompensationPolicy;
        yield return request.AdditionalNotes;
    }

    private static void ApplyFormFields(RoomingHouseRule rule, UpsertRoomingHouseRuleRequest request)
    {
        rule.GeneralRules = Normalize(request.GeneralRules);
        rule.QuietHours = Normalize(request.QuietHours);
        rule.SecurityPolicy = Normalize(request.SecurityPolicy);
        rule.CleaningPolicy = Normalize(request.CleaningPolicy);
        rule.GuestPolicy = Normalize(request.GuestPolicy);
        rule.ParkingPolicy = Normalize(request.ParkingPolicy);
        rule.UtilityPolicy = Normalize(request.UtilityPolicy);
        rule.DamageCompensationPolicy = Normalize(request.DamageCompensationPolicy);
        rule.AdditionalNotes = Normalize(request.AdditionalNotes);
    }

    private static void ClearFormFields(RoomingHouseRule rule)
    {
        rule.GeneralRules = null;
        rule.QuietHours = null;
        rule.SecurityPolicy = null;
        rule.CleaningPolicy = null;
        rule.GuestPolicy = null;
        rule.ParkingPolicy = null;
        rule.UtilityPolicy = null;
        rule.DamageCompensationPolicy = null;
        rule.AdditionalNotes = null;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void EnsureRuleAssetIsReusable(
        MediaAsset mediaAsset,
        Guid ownerUserId)
    {
        if (mediaAsset.OwnerUserId.HasValue && mediaAsset.OwnerUserId.Value != ownerUserId)
        {
            throw new BadRequestException(
                ErrorCodes.ImageInvalidOwner,
                "Bạn không có quyền sử dụng media asset PDF này.",
                new { mediaAssetId = mediaAsset.Id });
        }

        if (mediaAsset.Scope != MediaScope.RoomingHouseRulePdf)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset không phù hợp với PDF luật khu trọ.",
                new { mediaAssetId = mediaAsset.Id });
        }

        if (mediaAsset.Status is MediaStatus.PendingUpload or MediaStatus.Deleted)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset PDF chưa sẵn sàng để liên kết.",
                new { mediaAssetId = mediaAsset.Id, status = mediaAsset.Status.ToString() });
        }
    }

    internal static RoomingHouseRuleResponse ToResponse(RoomingHouseRule rule)
    {
        return new RoomingHouseRuleResponse
        {
            Id = rule.Id,
            RoomingHouseId = rule.RoomingHouseId,
            SourceType = rule.SourceType.ToString(),
            MediaAssetId = rule.MediaAssetId,
            PdfUrl = rule.MediaAssetId.HasValue
                ? PublicMediaPathBuilder.Build(rule.MediaAssetId.Value)
                : string.Empty,
            GeneralRules = rule.GeneralRules,
            QuietHours = rule.QuietHours,
            SecurityPolicy = rule.SecurityPolicy,
            CleaningPolicy = rule.CleaningPolicy,
            GuestPolicy = rule.GuestPolicy,
            ParkingPolicy = rule.ParkingPolicy,
            UtilityPolicy = rule.UtilityPolicy,
            DamageCompensationPolicy = rule.DamageCompensationPolicy,
            AdditionalNotes = rule.AdditionalNotes,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt
        };
    }
}
