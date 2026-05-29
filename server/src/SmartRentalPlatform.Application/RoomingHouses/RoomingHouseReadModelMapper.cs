using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.LeasePolicies;
using SmartRentalPlatform.Contracts.LegalDocuments;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Application.RoomingHouses;

internal static class RoomingHouseReadModelMapper
{
    public static RoomingHouseResponse ToResponse(RoomingHouse house)
    {
        return new RoomingHouseResponse
        {
            Id = house.Id,
            LandlordUserId = house.LandlordUserId,
            Name = house.Name,
            AddressDisplay = BuildAddressDisplay(house),
            ApprovalStatus = house.ApprovalStatus.ToString(),
            VisibilityStatus = house.VisibilityStatus.ToString(),
            RejectedReason = house.RejectedReason,
            CreatedAt = house.CreatedAt,
            UpdatedAt = house.UpdatedAt,
            CoverImageUrl = house.Images.OrderBy(x => x.SortOrder).FirstOrDefault(x => x.IsCover)?.ImageUrl 
                ?? house.Images.OrderBy(x => x.SortOrder).FirstOrDefault()?.ImageUrl,
            TotalRooms = house.Rooms?.Count(x => x.DeletedAt == null) ?? 0,
            AvailableRooms = house.Rooms?.Count(x => x.Status == RoomStatus.Available && x.DeletedAt == null) ?? 0
        };
    }

    public static RoomingHouseDetailResponse ToDetailResponse(RoomingHouse house)
    {
        return new RoomingHouseDetailResponse
        {
            Id = house.Id,
            LandlordUserId = house.LandlordUserId,
            Name = house.Name,
            Description = house.Description,
            AddressLine = house.AddressLine,
            ProvinceCode = house.ProvinceCode,
            WardCode = house.WardCode,
            AddressDisplay = BuildAddressDisplay(house),
            Latitude = house.Latitude,
            Longitude = house.Longitude,
            ApprovalStatus = house.ApprovalStatus.ToString(),
            VisibilityStatus = house.VisibilityStatus.ToString(),
            RejectedReason = house.RejectedReason,
            ReviewedByAdminId = house.ReviewedByAdminId,
            ReviewedAt = house.ReviewedAt,
            CreatedAt = house.CreatedAt,
            UpdatedAt = house.UpdatedAt,
            LegalDocument = house.LegalDocument is null ? null : new RoomingHouseLegalDocumentResponse
            {
                RoomingHouseId = house.LegalDocument.RoomingHouseId,
                DocumentType = house.LegalDocument.DocumentType.ToString(),
                FrontImageObjectKey = house.LegalDocument.FrontImageObjectKey,
                BackImageObjectKey = house.LegalDocument.BackImageObjectKey,
                ExtraImageObjectKey = house.LegalDocument.ExtraImageObjectKey,
                DocumentNumberMasked = house.LegalDocument.DocumentNumberMasked,
                UploadedAt = house.LegalDocument.UploadedAt,
                CreatedAt = house.LegalDocument.CreatedAt,
                UpdatedAt = house.LegalDocument.UpdatedAt
            },
            LeasePolicy = house.LeasePolicy is null ? null : new LeasePolicyResponse
            {
                Id = house.LeasePolicy.Id,
                RoomingHouseId = house.LeasePolicy.RoomingHouseId,
                AllowShortTermRenewal = house.LeasePolicy.AllowShortTermRenewal,
                RenewalNoticeDays = house.LeasePolicy.RenewalNoticeDays,
                DepositMonths = house.LeasePolicy.DepositMonths,
                Discount6MonthsPercent = house.LeasePolicy.Discount6MonthsPercent,
                Discount9MonthsPercent = house.LeasePolicy.Discount9MonthsPercent,
                Discount12MonthsPercent = house.LeasePolicy.Discount12MonthsPercent,
                Discount24MonthsPercent = house.LeasePolicy.Discount24MonthsPercent,
                IsActive = house.LeasePolicy.IsActive,
                CreatedAt = house.LeasePolicy.CreatedAt,
                UpdatedAt = house.LeasePolicy.UpdatedAt
            },
            Images = house.Images
                .OrderBy(x => x.SortOrder)
                .Select(x => new PropertyImageResponse
                {
                    Id = x.Id,
                    ObjectKey = x.ObjectKey,
                    ImageUrl = x.ImageUrl,
                    Caption = x.Caption,
                    IsCover = x.IsCover,
                    SortOrder = x.SortOrder,
                    CreatedAt = x.CreatedAt
                })
                .ToList(),
            Amenities = house.RoomingHouseAmenities
                .Select(x => new AmenityResponse
                {
                    Id = x.Amenity.Id,
                    Name = x.Amenity.Name,
                    Scope = x.Amenity.Scope.ToString(),
                    IconCode = x.Amenity.IconCode
                })
                .ToList()
        };
    }

    private static string BuildAddressDisplay(RoomingHouse house)
    {
        if (!string.IsNullOrWhiteSpace(house.Ward?.Name) &&
            !string.IsNullOrWhiteSpace(house.Province?.Name))
        {
            return $"{house.AddressLine}, {house.Ward.Name}, {house.Province.Name}";
        }

        return house.AddressDisplay;
    }
}
