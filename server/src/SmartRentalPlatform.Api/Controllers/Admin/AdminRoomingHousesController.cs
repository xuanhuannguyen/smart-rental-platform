using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Contracts.Admin;
using SmartRentalPlatform.Application.AdminApproval;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/rooming-houses")]
public class AdminRoomingHousesController : ControllerBase
{
    private readonly IAdminRoomingHouseApprovalService _roomingHouseApprovalService;
    private readonly IApprovalAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public AdminRoomingHousesController(
        IAdminRoomingHouseApprovalService roomingHouseApprovalService,
        IApprovalAuditService auditService,
        ICurrentUserService currentUserService)
    {
        _roomingHouseApprovalService = roomingHouseApprovalService;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<AdminRoomingHouseListResponse>>> GetPending(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(ValidationError("Thông tin phân trang không hợp lệ."));
        }

        var result = await _roomingHouseApprovalService.GetPendingAsync(pageNumber, pageSize, cancellationToken);
        return Ok(new ApiResponse<AdminRoomingHouseListResponse>
        {
            Success = true,
            Message = "Tải danh sách khu trọ chờ duyệt thành công.",
            Data = result
        });
    }

    [HttpGet("public")]
    public async Task<ActionResult<ApiResponse<AdminRoomingHouseListResponse>>> GetPublic(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(ValidationError("Thông tin phân trang không hợp lệ."));
        }

        var result = await _roomingHouseApprovalService.GetPublicAsync(pageNumber, pageSize, cancellationToken);
        return Ok(new ApiResponse<AdminRoomingHouseListResponse>
        {
            Success = true,
            Message = "Tải danh sách khu trọ đã public thành công.",
            Data = result
        });
    }

    [HttpGet("{roomingHouseId:guid}")]
    public async Task<ActionResult<ApiResponse<AdminRoomingHouseDetailResponse>>> GetDetail(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        var result = await _roomingHouseApprovalService.GetDetailAsync(roomingHouseId, cancellationToken);
        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.HouseNotFound,
                Message = "Không tìm thấy khu trọ."
            });
        }

        return Ok(new ApiResponse<AdminRoomingHouseDetailResponse>
        {
            Success = true,
            Message = "Tải chi tiết khu trọ thành công.",
            Data = result
        });
    }

    [HttpPost("{roomingHouseId:guid}/approve")]
    public async Task<ActionResult<ApiResponse<object>>> Approve(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        var success = await _roomingHouseApprovalService.ApproveAsync(roomingHouseId, adminId, cancellationToken);
        if (!success)
        {
            return BadRequest(ValidationError("Chỉ khu trọ đang chờ duyệt mới có thể được duyệt."));
        }

        await _auditService.LogAsync(adminId, "RoomingHouse", roomingHouseId, "Approved", cancellationToken: cancellationToken);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Duyệt khu trọ thành công.",
            Data = new { roomingHouseId }
        });
    }

    [HttpPost("{roomingHouseId:guid}/reject")]
    public async Task<ActionResult<ApiResponse<object>>> Reject(
        Guid roomingHouseId,
        AdminRejectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(ValidationError("Lý do từ chối là bắt buộc."));
        }

        var adminId = GetCurrentUserId();
        var success = await _roomingHouseApprovalService.RejectAsync(roomingHouseId, adminId, request.Reason, cancellationToken);
        if (!success)
        {
            return BadRequest(ValidationError("Chỉ khu trọ đang chờ duyệt mới có thể bị từ chối."));
        }

        await _auditService.LogAsync(adminId, "RoomingHouse", roomingHouseId, "Rejected", request.Reason, cancellationToken: cancellationToken);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Từ chối khu trọ thành công.",
            Data = new { roomingHouseId }
        });
    }
    private Guid GetCurrentUserId()
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            throw new UnauthorizedAccessException("Không tìm thấy người dùng admin hiện tại.");
        }

        return _currentUserService.UserId.Value;
    }

    private static ApiErrorResponse ValidationError(string message)
    {
        return new ApiErrorResponse
        {
            Success = false,
            ErrorCode = ErrorCodes.ValidationError,
            Message = message
        };
    }
}
