using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Rooms;

public class RoomAccessService
{
    private readonly IAppDbContext context;

    public RoomAccessService(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task EnsureOwnedApprovedRoomingHouseAsync(
        Guid landlordUserId,
        Guid roomingHouseId,
        CancellationToken cancellationToken)
    {
        var roomingHouse = await context.RoomingHouses
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

        EnsureRoomingHouseApproved(roomingHouse);
        await EnsureLeasePolicyExistsAsync(roomingHouseId, cancellationToken);
    }

    public async Task<Room?> GetOwnedRoomForUpdateAsync(
        Guid landlordUserId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        return await context.Rooms
            .Include(x => x.RoomingHouse)
            .FirstOrDefaultAsync(
                x => x.Id == roomId &&
                     x.DeletedAt == null &&
                     x.RoomingHouse.LandlordUserId == landlordUserId,
                cancellationToken);
    }

    public void EnsureRoomingHouseApproved(RoomingHouse roomingHouse)
    {
        if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Approved)
        {
            throw new ConflictException(
                ErrorCodes.HouseNotApproved,
                "Chỉ khu trọ đã được duyệt mới có thể quản lý phòng.",
                new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
        }
    }

    public async Task EnsureRoomNumberAvailableAsync(
        Guid roomingHouseId,
        string roomNumber,
        Guid? excludedRoomId,
        CancellationToken cancellationToken)
    {
        var normalizedRoomNumber = roomNumber.ToLowerInvariant();
        var duplicateExists = await context.Rooms.AnyAsync(
            x => x.RoomingHouseId == roomingHouseId &&
                 x.DeletedAt == null &&
                 x.RoomNumber.ToLower() == normalizedRoomNumber &&
                 (!excludedRoomId.HasValue || x.Id != excludedRoomId.Value),
            cancellationToken);

        if (duplicateExists)
        {
            throw new ConflictException(
                ErrorCodes.RoomDuplicateNumber,
                "Số phòng đã tồn tại trong khu trọ này.",
                new { roomingHouseId, roomNumber });
        }
    }

    public async Task<List<int>> ValidateRoomAmenityIdsAsync(
        IEnumerable<int> requestedAmenityIds,
        CancellationToken cancellationToken)
    {
        var amenityIds = requestedAmenityIds.Distinct().ToList();
        var validAmenityIds = await context.Amenities
            .Where(x =>
                amenityIds.Contains(x.Id) &&
                x.IsActive &&
                (x.Scope == AmenityScope.Room || x.Scope == AmenityScope.Both))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (validAmenityIds.Count != amenityIds.Count)
        {
            throw new BadRequestException(
                ErrorCodes.AmenityNotFound,
                "Một hoặc nhiều mã tiện ích không hợp lệ.",
                new { amenityIds });
        }

        return amenityIds;
    }

    private async Task EnsureLeasePolicyExistsAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken)
    {
        var hasLeasePolicy = await context.LeasePolicies
            .AnyAsync(x => x.RoomingHouseId == roomingHouseId && x.IsActive, cancellationToken);

        if (!hasLeasePolicy)
        {
            throw new ConflictException(
                ErrorCodes.LeasePolicyRequired,
                "Vui lòng hoàn thành chính sách thuê trước khi tạo hoặc quản lý phòng.",
                new { roomingHouseId });
        }
    }
}
