using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Rooms;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomPriceTiers;
using SmartRentalPlatform.Contracts.Rooms;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService roomService;
    private readonly ICurrentUserService currentUserService;

    public RoomsController(
        IRoomService roomService,
        ICurrentUserService currentUserService)
    {
        this.roomService = roomService;
        this.currentUserService = currentUserService;
    }

    [Authorize]
    [HttpPost("rooming-houses/{roomingHouseId:guid}/rooms")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> Create(
        Guid roomingHouseId,
        CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomService.CreateAsync(userId, roomingHouseId, request, cancellationToken);

        return Ok(new ApiResponse<RoomResponse>
        {
            Success = true,
            Message = "Tạo phòng thành công và phòng đang ở trạng thái ẩn.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("rooming-houses/{roomingHouseId:guid}/rooms")]
    public async Task<ActionResult<ApiResponse<List<RoomResponse>>>> GetByRoomingHouse(
        Guid roomingHouseId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomService.GetByRoomingHouseAsync(userId, roomingHouseId, cancellationToken);

        return Ok(new ApiResponse<List<RoomResponse>>
        {
            Success = true,
            Message = "Tải danh sách phòng thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("rooms/{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomService.GetByIdAsync(userId, id, cancellationToken);
        return RoomResult(result, "Tải thông tin phòng thành công.");
    }

    [Authorize]
    [HttpPut("rooms/{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> Update(
        Guid id,
        UpdateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomService.UpdateAsync(userId, id, request, cancellationToken);
        return RoomResult(result, "Lưu thông tin cơ bản của phòng thành công.");
    }

    [Authorize]
    [HttpPut("rooms/{id:guid}/images")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> UpdateImages(
        Guid id,
        UpdatePropertyImagesRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomService.UpdateImagesAsync(userId, id, request, cancellationToken);
        return RoomResult(result, "Lưu ảnh phòng thành công.");
    }

    [Authorize]
    [HttpPut("rooms/{id:guid}/amenities")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> UpdateAmenities(
        Guid id,
        UpdateAmenitiesRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomService.UpdateAmenitiesAsync(userId, id, request, cancellationToken);
        return RoomResult(result, "Lưu tiện ích phòng thành công.");
    }

    [Authorize]
    [HttpPut("rooms/{id:guid}/price-tiers")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> UpdatePriceTiers(
        Guid id,
        UpdateRoomPriceTiersRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomService.UpdatePriceTiersAsync(userId, id, request, cancellationToken);
        return RoomResult(result, "Lưu bảng giá phòng thành công.");
    }

    [Authorize]
    [HttpPut("rooms/{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> UpdateStatus(
        Guid id,
        UpdateRoomStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomService.UpdateStatusAsync(userId, id, request, cancellationToken);
        return RoomResult(result, "Cập nhật trạng thái phòng thành công.");
    }

    [Authorize]
    [HttpPost("rooms/{id:guid}/submit")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> Submit(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomService.SubmitAsync(userId, id, cancellationToken);
        return RoomResult(result, "Gửi phòng thành công và phòng đã chuyển sang còn trống.");
    }

    private Guid GetCurrentUserId()
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            throw new UnauthorizedAccessException("Không tìm thấy mã người dùng đã đăng nhập.");
        }

        return currentUserService.UserId.Value;
    }

    private ActionResult<ApiResponse<RoomResponse>> RoomResult(RoomResponse? result, string message)
    {
        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RoomNotFound,
                Message = "Không tìm thấy phòng."
            });
        }

        return Ok(new ApiResponse<RoomResponse>
        {
            Success = true,
            Message = message,
            Data = result
        });
    }
}
