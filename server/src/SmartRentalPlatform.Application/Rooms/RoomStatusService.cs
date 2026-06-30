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


        if (!Enum.TryParse<RoomStatus>(request.Status, ignoreCase: true, out var status))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Trạng thái phòng không hợp lệ.",
                new { field = nameof(request.Status) });
        }


        EnsureAllowedManualTransition(room.Status, status);

        room.Status = status;
        room.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return await roomQueryService.GetByIdAsync(landlordUserId, roomId, cancellationToken);
    }

    private static void EnsureAllowedManualTransition(RoomStatus currentStatus, RoomStatus requestedStatus)
    {
        if (currentStatus == requestedStatus)
        {
            return;
        }

        var allowed =
            currentStatus == RoomStatus.Available && requestedStatus == RoomStatus.Maintenance ||
            currentStatus == RoomStatus.Maintenance && requestedStatus == RoomStatus.Available;

        if (allowed)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RoomInvalidStatus,
            "Chỉ có thể chuyển phòng giữa trạng thái còn trống và tạm ngưng. Phòng ẩn phải được publish, phòng giữ chỗ hoặc đang thuê không thể đổi trạng thái thủ công.",
            new
            {
                currentStatus = currentStatus.ToString(),
                requestedStatus = requestedStatus.ToString()
            });
    }
}

