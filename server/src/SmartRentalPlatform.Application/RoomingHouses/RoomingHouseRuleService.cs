using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Files;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Requests;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Properties;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class RoomingHouseRuleService : IRoomingHouseRuleService
{
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
            rule.PdfObjectKey = request.PdfObjectKey!.Trim();
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
            rule.PdfObjectKey = uploaded.ObjectKey;
        }

        rule.UpdatedAt = now;
        roomingHouse.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        return ToResponse(rule);
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
            if (string.IsNullOrWhiteSpace(request.PdfObjectKey))
            {
                throw new BadRequestException(
                    ErrorCodes.HouseRuleInvalid,
                    "Vui lòng tải PDF luật khu trọ.",
                    new { field = nameof(request.PdfObjectKey) });
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

    internal static RoomingHouseRuleResponse ToResponse(RoomingHouseRule rule)
    {
        return new RoomingHouseRuleResponse
        {
            Id = rule.Id,
            RoomingHouseId = rule.RoomingHouseId,
            SourceType = rule.SourceType.ToString(),
            PdfObjectKey = rule.PdfObjectKey,
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
