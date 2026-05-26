using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.LegalDocuments;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomingHouses;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/rooming-houses")]
public class RoomingHousesController : ControllerBase
{
    private readonly IRoomingHouseService roomingHouseService;
    private readonly ICurrentUserService currentUserService;

    public RoomingHousesController(
        IRoomingHouseService roomingHouseService,
        ICurrentUserService currentUserService)
    {
        this.roomingHouseService = roomingHouseService;
        this.currentUserService = currentUserService;
    }

    [Authorize]
    [HttpGet("my/onboarding")]
    public async Task<ActionResult<ApiResponse<RoomingHouseOnboardingResponse>>> GetMyOnboarding(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomingHouseService.GetOnboardingAsync(userId, cancellationToken);

        return Ok(new ApiResponse<RoomingHouseOnboardingResponse>
        {
            Success = true,
            Message = "Tải trạng thái đăng ký chủ trọ thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("draft")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> CreateDraft(
        CreateRoomingHouseDraftRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomingHouseService.CreateDraftAsync(userId, request, cancellationToken);

        return Ok(new ApiResponse<RoomingHouseDetailResponse>
        {
            Success = true,
            Message = "Bản nháp khu trọ đã sẵn sàng.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<ActionResult<ApiResponse<List<RoomingHouseResponse>>>> GetMyRoomingHouses(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomingHouseService.GetByLandlordAsync(userId, cancellationToken);

        return Ok(new ApiResponse<List<RoomingHouseResponse>>
        {
            Success = true,
            Message = "Tải danh sách khu trọ của tôi thành công.",
            Data = result
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<RoomingHouseResponse>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await roomingHouseService.GetAllAsync(cancellationToken);

        return Ok(new ApiResponse<List<RoomingHouseResponse>>
        {
            Success = true,
            Message = "Tải danh sách khu trọ thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await roomingHouseService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return RoomingHouseNotFound();
        }

        if (!CanAccess(result))
        {
            return Forbidden(ErrorCodes.Forbidden, "Bạn không có quyền truy cập khu trọ này.");
        }

        return Ok(new ApiResponse<RoomingHouseDetailResponse>
        {
            Success = true,
            Message = "Tải thông tin khu trọ thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> Update(
        Guid id,
        UpdateRoomingHouseRequest request,
        CancellationToken cancellationToken)
    {
        var ownership = await EnsureOwnerAsync(id, cancellationToken);
        if (ownership is not null)
        {
            return ownership;
        }

        var result = await roomingHouseService.UpdateAsync(id, request, cancellationToken);
        return RoomingHouseDetailResult(result, "Lưu thông tin cơ bản của khu trọ thành công.");
    }

    [Authorize]
    [HttpPut("{id:guid}/amenities")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> UpdateAmenities(
        Guid id,
        UpdateAmenitiesRequest request,
        CancellationToken cancellationToken)
    {
        var ownership = await EnsureOwnerAsync(id, cancellationToken);
        if (ownership is not null)
        {
            return ownership;
        }

        var result = await roomingHouseService.UpdateAmenitiesAsync(id, request, cancellationToken);
        return RoomingHouseDetailResult(result, "Lưu tiện ích khu trọ thành công.");
    }

    [Authorize]
    [HttpPut("{id:guid}/images")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> UpdateImages(
        Guid id,
        UpdatePropertyImagesRequest request,
        CancellationToken cancellationToken)
    {
        var ownership = await EnsureOwnerAsync(id, cancellationToken);
        if (ownership is not null)
        {
            return ownership;
        }

        var result = await roomingHouseService.UpdateImagesAsync(id, request, cancellationToken);
        return RoomingHouseDetailResult(result, "Lưu ảnh khu trọ thành công.");
    }

    [Authorize]
    [HttpPut("{id:guid}/legal-document")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> UpdateLegalDocument(
        Guid id,
        UpdateRoomingHouseLegalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var ownership = await EnsureOwnerAsync(id, cancellationToken);
        if (ownership is not null)
        {
            return ownership;
        }

        var result = await roomingHouseService.UpdateLegalDocumentAsync(id, request, cancellationToken);
        return RoomingHouseDetailResult(result, "Lưu giấy tờ pháp lý của khu trọ thành công.");
    }

    [Authorize]
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> Submit(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomingHouseService.SubmitAsync(id, userId, cancellationToken);
        if (result is null)
        {
            return NotFound(RoomingHouseNotFoundError());
        }

        return RoomingHouseDetailResult(result, "Gửi khu trọ để xét duyệt thành công.");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetCurrentUserId();
        var result = await roomingHouseService.ApproveAsync(id, adminUserId, cancellationToken);
        return RoomingHouseDetailResult(result, "Duyệt khu trọ thành công.");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> Reject(
        Guid id,
        RejectRoomingHouseRequest request,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetCurrentUserId();
        var result = await roomingHouseService.RejectAsync(id, adminUserId, request, cancellationToken);
        return RoomingHouseDetailResult(result, "Từ chối khu trọ thành công.");
    }

    private Guid GetCurrentUserId()
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            throw new UnauthorizedAccessException("Không tìm thấy mã người dùng đã đăng nhập.");
        }

        return currentUserService.UserId.Value;
    }

    private async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>?> EnsureOwnerAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken)
    {
        var roomingHouse = await roomingHouseService.GetByIdAsync(roomingHouseId, cancellationToken);
        if (roomingHouse is null)
        {
            return RoomingHouseNotFound();
        }

        if (roomingHouse.LandlordUserId != GetCurrentUserId())
        {
            return Forbidden(ErrorCodes.Forbidden, "Bạn không có quyền cập nhật khu trọ này.");
        }

        return null;
    }

    private bool CanAccess(RoomingHouseDetailResponse roomingHouse)
    {
        return currentUserService.Roles.Contains("Admin") ||
               roomingHouse.LandlordUserId == GetCurrentUserId();
    }

    private ActionResult<ApiResponse<RoomingHouseDetailResponse>> RoomingHouseDetailResult(
        RoomingHouseDetailResponse? result,
        string message)
    {
        if (result is null)
        {
            return RoomingHouseNotFound();
        }

        return Ok(new ApiResponse<RoomingHouseDetailResponse>
        {
            Success = true,
            Message = message,
            Data = result
        });
    }

    private ActionResult<ApiResponse<RoomingHouseDetailResponse>> RoomingHouseNotFound()
    {
        return NotFound(RoomingHouseNotFoundError());
    }

    private static ApiErrorResponse RoomingHouseNotFoundError()
    {
        return new ApiErrorResponse
        {
            Success = false,
            ErrorCode = ErrorCodes.HouseNotFound,
            Message = "Không tìm thấy khu trọ."
        };
    }

    private ActionResult<ApiResponse<RoomingHouseDetailResponse>> Forbidden(
        string errorCode,
        string message)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        });
    }
}
