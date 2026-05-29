using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Rooms;

public class RoomStatusService : IRoomStatusService
{
    private readonly IAppDbContext context;
    private readonly RoomAccessService roomAccessService;
    private readonly IRoomQueryService roomQueryService;

    public RoomStatusService(
        IAppDbContext context,
        RoomAccessService roomAccessService,
        IRoomQueryService roomQueryService)
    {
        this.context = context;
        this.roomAccessService = roomAccessService;
        this.roomQueryService = roomQueryService;
    }

    public async Task<RoomResponse?> SubmitAsync(
        Guid landlordUserId,
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var room = await context.Rooms
            .Include(x => x.RoomingHouse)
            .Include(x => x.PriceTiers)
            .Include(x => x.Images)
            .FirstOrDefaultAsync(
                x => x.Id == roomId &&
                     x.DeletedAt == null &&
                     x.RoomingHouse.LandlordUserId == landlordUserId,
                cancellationToken);

        if (room is null)
        {
            return null;
        }

        roomAccessService.EnsureRoomingHouseApproved(room.RoomingHouse);

        if (room.Status != RoomStatus.Hidden)
        {
            throw new ConflictException(
                ErrorCodes.RoomInvalidStatus,
                "Chỉ phòng đang ẩn mới có thể gửi hiển thị.",
                new { currentStatus = room.Status.ToString() });
        }

        RoomValidationRules.ValidateRoomFields(
            room.RoomNumber,
            room.Floor,
            room.AreaM2,
            room.MaxOccupants);

        RoomValidationRules.ValidateRequiredRoomImages(room.Images);
        RoomValidationRules.ValidateRoomCanBeSubmitted(room);

        room.Status = RoomStatus.Available;
        room.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return await roomQueryService.GetByIdAsync(landlordUserId, roomId, cancellationToken);
    }

    public async Task<RoomResponse?> UpdateStatusAsync(
        Guid landlordUserId,
        Guid roomId,
        UpdateRoomStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var room = await roomAccessService.GetOwnedRoomForUpdateAsync(
            landlordUserId,
            roomId,
            cancellationToken);

        if (room is null)
        {
            return null;
        }

        roomAccessService.EnsureRoomingHouseApproved(room.RoomingHouse);

        if (room.Status == RoomStatus.Hidden)
        {
            throw new ConflictException(
                ErrorCodes.RoomInvalidStatus,
                "Phòng đang ẩn phải được gửi hiển thị trước khi thay đổi trạng thái vận hành.",
                new { currentStatus = room.Status.ToString() });
        }

        if (!Enum.TryParse<RoomStatus>(request.Status, ignoreCase: true, out var status))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Trạng thái phòng không hợp lệ.",
                new { field = nameof(request.Status) });
        }

        if (status == RoomStatus.Hidden)
        {
            throw new ConflictException(
                ErrorCodes.RoomInvalidStatus,
                "Phòng đã hiển thị không thể chuyển lại sang trạng thái ẩn bằng API trạng thái.",
                new { requestedStatus = request.Status });
        }

        room.Status = status;
        room.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return await roomQueryService.GetByIdAsync(landlordUserId, roomId, cancellationToken);
    }
}
