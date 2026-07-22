using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Contracts.Admin;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Users;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.LegalDocuments;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.AdminApproval;

public class AdminRoomingHouseApprovalService : IAdminRoomingHouseApprovalService
{
    private readonly IAppDbContext _context;
    private readonly IUserService _userService;

    public AdminRoomingHouseApprovalService(
        IAppDbContext context,
        IUserService userService)
    {
        _context = context;
        _userService = userService;
    }

    public async Task<AdminRoomingHouseListResponse> GetPendingAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RoomingHouses
            .AsNoTracking()
            .Where(x => x.ApprovalStatus == RoomingHouseApprovalStatus.Pending && x.DeletedAt == null);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminRoomingHouseListItemResponse
            {
                Id = x.Id,
                LandlordUserId = x.LandlordUserId,
                LandlordEmail = x.Landlord.Email,
                LandlordName = x.Landlord.DisplayName,
                Name = x.Name,
                AddressDisplay = x.AddressDisplay,
                ApprovalStatus = x.ApprovalStatus.ToString(),
                VisibilityStatus = x.VisibilityStatus.ToString(),
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminRoomingHouseListResponse
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AdminRoomingHouseDetailResponse?> GetDetailAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        var house = await _context.RoomingHouses
            .AsNoTracking()
            .Include(x => x.Landlord)
            .Include(x => x.LegalDocument)
            .Include(x => x.Images)
            .Include(x => x.RoomingHouseAmenities)
                .ThenInclude(x => x.Amenity)
            .Include(x => x.Rooms)
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

        if (house is null)
        {
            return null;
        }

        return new AdminRoomingHouseDetailResponse
        {
            Id = house.Id,
            LandlordUserId = house.LandlordUserId,
            LandlordEmail = house.Landlord.Email,
            LandlordName = house.Landlord.DisplayName,
            Name = house.Name,
            Description = house.Description,
            AddressLine = house.AddressLine,
            ProvinceCode = house.ProvinceCode,
            WardCode = house.WardCode,
            AddressDisplay = house.AddressDisplay,
            Latitude = house.Latitude,
            Longitude = house.Longitude,
            ApprovalStatus = house.ApprovalStatus.ToString(),
            VisibilityStatus = house.VisibilityStatus.ToString(),
            RejectedReason = house.RejectedReason,
            ReviewedByAdminId = house.ReviewedByAdminId,
            ReviewedAt = house.ReviewedAt,
            CreatedAt = house.CreatedAt,
            LegalDocument = house.LegalDocument is null
                ? null
                : new RoomingHouseLegalDocumentResponse
                {
                    RoomingHouseId = house.LegalDocument.RoomingHouseId,
                    FrontMediaAssetId = house.LegalDocument.FrontMediaAssetId,
                    BackMediaAssetId = house.LegalDocument.BackMediaAssetId,
                    ExtraMediaAssetId = house.LegalDocument.ExtraMediaAssetId,
                    DocumentType = house.LegalDocument.DocumentType.ToString(),
                    FrontImageUrl = BuildPrivateLegalDocumentUrl(house.LegalDocument.FrontMediaAssetId),
                    BackImageUrl = BuildPrivateLegalDocumentUrl(house.LegalDocument.BackMediaAssetId),
                    ExtraImageUrl = BuildOptionalPrivateLegalDocumentUrl(house.LegalDocument.ExtraMediaAssetId),
                    DocumentNumberMasked = house.LegalDocument.DocumentNumberMasked,
                    UploadedAt = house.LegalDocument.UploadedAt,
                    CreatedAt = house.LegalDocument.CreatedAt,
                    UpdatedAt = house.LegalDocument.UpdatedAt
                },
            Images = house.Images
                .Where(x => x.MediaAssetId.HasValue)
                .OrderBy(x => x.SortOrder)
                .Select(x => new PropertyImageResponse
                {
                    Id = x.Id,
                    MediaAssetId = x.MediaAssetId,
                    ImageUrl = x.MediaAssetId.HasValue
                        ? PublicMediaPathBuilder.Build(x.MediaAssetId.Value)
                        : string.Empty,
                    Caption = x.Caption,
                    IsCover = x.IsCover,
                    SortOrder = x.SortOrder,
                    CreatedAt = x.CreatedAt
                })
                .ToList(),
            Amenities = house.RoomingHouseAmenities
                .Select(x => new AmenityResponse
                {
                    Id = x.AmenityId,
                    Name = x.Amenity.Name,
                    Scope = x.Amenity.Scope.ToString(),
                    IconCode = x.Amenity.IconCode
                })
                .ToList(),
            Rooms = house.Rooms
                .Where(x => x.DeletedAt == null)
                .OrderBy(x => x.Floor)
                .ThenBy(x => x.RoomNumber)
                .Select(x => new AdminRoomInfoResponse
                {
                    Id = x.Id,
                    RoomNumber = x.RoomNumber,
                    Floor = x.Floor,
                    AreaM2 = x.AreaM2,
                    MaxOccupants = x.MaxOccupants,
                    Status = x.Status.ToString()
                })
                .ToList()
        };
    }

    private static string BuildPrivateLegalDocumentUrl(Guid? mediaAssetId)
    {
        return mediaAssetId.HasValue
            ? PrivateMediaPathBuilder.Build(mediaAssetId.Value)
            : string.Empty;
    }

    private static string? BuildOptionalPrivateLegalDocumentUrl(Guid? mediaAssetId)
    {
        return mediaAssetId.HasValue
            ? PrivateMediaPathBuilder.Build(mediaAssetId.Value)
            : null;
    }

    public async Task<bool> ApproveAsync(
        Guid roomingHouseId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            var house = await _context.RoomingHouses
                .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

            if (house is null || house.ApprovalStatus != RoomingHouseApprovalStatus.Pending)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            house.ApprovalStatus = RoomingHouseApprovalStatus.Approved;
            house.VisibilityStatus = RoomingHouseVisibilityStatus.Visible;
            house.RejectedReason = null;
            house.ReviewedByAdminId = adminId;
            house.ReviewedAt = now;
            house.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await _userService.GrantLandlordRoleAfterRoomingHouseApprovedAsync(roomingHouseId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> RejectAsync(
        Guid roomingHouseId,
        Guid adminId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        var house = await _context.RoomingHouses
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

        if (house is null || house.ApprovalStatus != RoomingHouseApprovalStatus.Pending)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        house.ApprovalStatus = RoomingHouseApprovalStatus.Rejected;
        house.VisibilityStatus = RoomingHouseVisibilityStatus.Hidden;
        house.RejectedReason = reason.Trim();
        house.ReviewedByAdminId = adminId;
        house.ReviewedAt = now;
        house.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdminRoomingHouseListResponse> GetPublicAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RoomingHouses
            .AsNoTracking()
            .Where(x => x.ApprovalStatus == RoomingHouseApprovalStatus.Approved && x.DeletedAt == null);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminRoomingHouseListItemResponse
            {
                Id = x.Id,
                LandlordUserId = x.LandlordUserId,
                LandlordEmail = x.Landlord.Email,
                LandlordName = x.Landlord.DisplayName,
                Name = x.Name,
                AddressDisplay = x.AddressDisplay,
                ApprovalStatus = x.ApprovalStatus.ToString(),
                VisibilityStatus = x.VisibilityStatus.ToString(),
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminRoomingHouseListResponse
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
