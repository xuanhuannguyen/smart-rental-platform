using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Rooms;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomPriceTiers;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;

[ApiController]
[Route("api")]
public class RoomsController : ControllerBase
{
    private readonly IRoomQueryService roomQueryService;
    private readonly IRoomCommandService roomCommandService;
    private readonly IRoomMediaService roomMediaService;
    private readonly IRoomPriceTierService roomPriceTierService;
    private readonly IRoomStatusService roomStatusService;
    private readonly ICurrentUserService currentUserService;
    private readonly IRentalContractService rentalContractService;

    public RoomsController(
        IRoomQueryService roomQueryService,
        IRoomCommandService roomCommandService,
        IRoomMediaService roomMediaService,
        IRoomPriceTierService roomPriceTierService,
        IRoomStatusService roomStatusService,
        ICurrentUserService currentUserService,
        IRentalContractService rentalContractService)
    {
        this.roomQueryService = roomQueryService;
        this.roomCommandService = roomCommandService;
        this.roomMediaService = roomMediaService;
        this.roomPriceTierService = roomPriceTierService;
        this.roomStatusService = roomStatusService;
        this.currentUserService = currentUserService;
        this.rentalContractService = rentalContractService;
    }

    [Authorize]
    [HttpPost("rooming-houses/{roomingHouseId:guid}/rooms")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> Create(
        Guid roomingHouseId,
        CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomCommandService.CreateAsync(userId, roomingHouseId, request, cancellationToken);

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
        var result = await roomQueryService.GetByRoomingHouseAsync(userId, roomingHouseId, cancellationToken);

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
        var result = await roomQueryService.GetByIdAsync(userId, id, cancellationToken);
        return RoomResult(result, "Tải thông tin phòng thành công.");
    }

    [Authorize]
    [HttpGet("rooms/{id:guid}/active-contract")]
    public async Task<ActionResult<ApiResponse<ContractDetailResponse>>> GetActiveContract(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.GetActiveContractByRoomIdAsync(userId, id, cancellationToken);
        
        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RentalContractNotFound,
                Message = "Không tìm thấy hợp đồng đang active của phòng này."
            });
        }

        return Ok(new ApiResponse<ContractDetailResponse>
        {
            Success = true,
            Message = "Tải thông tin hợp đồng thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("rooms/{id:guid}/tenants")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ContractOccupantResponse>>>> GetActiveTenants(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.GetActiveTenantsByRoomIdAsync(userId, id, cancellationToken);

        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RentalContractNotFound,
                Message = "Không tìm thấy hợp đồng đang active của phòng này."
            });
        }

        return Ok(new ApiResponse<IReadOnlyCollection<ContractOccupantResponse>>
        {
            Success = true,
            Message = "Tải danh sách người ở thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPut("rooms/{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> Update(
        Guid id,
        UpdateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomCommandService.UpdateAsync(userId, id, request, cancellationToken);
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
        var result = await roomMediaService.UpdateImagesAsync(userId, id, request, cancellationToken);
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
        var result = await roomMediaService.UpdateAmenitiesAsync(userId, id, request, cancellationToken);
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
        var result = await roomPriceTierService.UpdatePriceTiersAsync(userId, id, request, cancellationToken);
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
        var result = await roomStatusService.UpdateStatusAsync(userId, id, request, cancellationToken);
        return RoomResult(result, "Cập nhật trạng thái phòng thành công.");
    }

    [Authorize]
    [HttpPost("rooms/{id:guid}/submit")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> Submit(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomStatusService.SubmitAsync(userId, id, cancellationToken);
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
