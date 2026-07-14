using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomPriceTiers;
using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Application.Rooms;

internal static class RoomValidationRules
{
    public static void ValidateRoomFields(
        string roomNumber,
        int floor,
        decimal? areaM2,
        int maxOccupants)
    {
        if (string.IsNullOrWhiteSpace(roomNumber))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Số phòng là bắt buộc.",
                new { field = nameof(roomNumber) });
        }

        if (floor < 0)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Tầng phải lớn hơn hoặc bằng 0.",
                new { field = nameof(floor) });
        }

        if (areaM2 is <= 0)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Diện tích phải lớn hơn 0.",
                new { field = nameof(areaM2) });
        }

        if (maxOccupants <= 0)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Số người tối đa phải lớn hơn 0.",
                new { field = nameof(maxOccupants) });
        }
    }

    public static void ValidatePropertyImages(IReadOnlyCollection<UpdatePropertyImageItemRequest> images)
    {
        if (images.Count < 3)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Phòng cần có ít nhất 3 ảnh.",
                new { field = nameof(images) });
        }

        var coverCount = images.Count(x => x.IsCover);
        if (coverCount != 1)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Phòng cần có đúng 1 ảnh đại diện.",
                new { field = nameof(images) });
        }

        if (images.Any(x => !x.MediaAssetId.HasValue))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Mỗi ảnh phòng phải có mediaAssetId hợp lệ.",
                new { field = nameof(images) });
        }
    }

    public static void ValidateRequiredRoomImages(IEnumerable<PropertyImage> images)
    {
        var imageList = images.ToList();

        if (imageList.Count < 3)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Phòng cần có ít nhất 3 ảnh trước khi gửi hiển thị.",
                new { field = nameof(images) });
        }

        var coverCount = imageList.Count(x => x.IsCover);
        if (coverCount != 1)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Phòng cần có đúng 1 ảnh đại diện trước khi gửi hiển thị.",
                new { field = nameof(images) });
        }

        if (imageList.Any(x => !x.MediaAssetId.HasValue))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset ảnh là bắt buộc.",
                new { field = nameof(images) });
        }
    }

    public static void ValidatePriceTiers(
        IReadOnlyCollection<RoomPriceTierRequest> priceTiers,
        int maxOccupants,
        bool isTieredPricing)
    {
        if (priceTiers.Count == 0)
        {
            throw new BadRequestException(
                ErrorCodes.PriceTierInvalid,
                "Cần có ít nhất 1 mức giá phòng.",
                new { field = nameof(priceTiers) });
        }

        if (isTieredPricing)
        {
            if (priceTiers.Count != maxOccupants)
            {
                throw new BadRequestException(
                    ErrorCodes.PriceTierInvalid,
                    $"Yêu cầu định cấu hình giá cho tất cả mốc số lượng người ở từ 1 đến {maxOccupants}.",
                    new { expectedCount = maxOccupants, actualCount = priceTiers.Count });
            }

            var occupantCounts = priceTiers.Select(x => x.OccupantCount).OrderBy(x => x).ToList();
            for (var i = 1; i <= maxOccupants; i++)
            {
                if (occupantCounts[i - 1] != i)
                {
                    throw new BadRequestException(
                        ErrorCodes.PriceTierInvalid,
                        $"Thiếu mức giá cho mốc {i} người ở.",
                        new { missingOccupantCount = i });
                }
            }
        }
        else
        {
            if (priceTiers.Count != 1)
            {
                throw new BadRequestException(
                    ErrorCodes.PriceTierInvalid,
                    "Khi áp dụng đơn giá cố định, chỉ được phép cấu hình duy nhất một mức giá.",
                    new { actualCount = priceTiers.Count });
            }

            var firstTier = priceTiers.First();
            if (firstTier.OccupantCount != 1)
            {
                throw new BadRequestException(
                    ErrorCodes.PriceTierInvalid,
                    "Đối với đơn giá cố định, số lượng người áp dụng mặc định phải là 1.",
                    new { occupantCount = firstTier.OccupantCount });
            }
        }

        foreach (var tier in priceTiers)
        {
            ValidatePriceTier(tier.OccupantCount, tier.MonthlyRent, maxOccupants);
        }
    }

    public static void ValidateRoomCanBeSubmitted(Room room)
    {
        var priceTierRequests = room.PriceTiers
            .Select(x => new RoomPriceTierRequest
            {
                OccupantCount = x.OccupantCount,
                MonthlyRent = x.MonthlyRent
            })
            .ToList();

        ValidatePriceTiers(priceTierRequests, room.MaxOccupants, room.IsTieredPricing);
    }

    private static void ValidatePriceTier(
        int occupantCount,
        decimal monthlyRent,
        int maxOccupants)
    {
        if (occupantCount <= 0 || occupantCount > maxOccupants)
        {
            throw new BadRequestException(
                ErrorCodes.PriceTierInvalid,
                "Số người áp dụng phải nằm trong khoảng từ 1 đến số người tối đa.",
                new { occupantCount, maxOccupants });
        }

        if (monthlyRent <= 0)
        {
            throw new BadRequestException(
                ErrorCodes.PriceTierInvalid,
                "Giá thuê hằng tháng phải lớn hơn 0.",
                new { monthlyRent });
        }
    }
}
