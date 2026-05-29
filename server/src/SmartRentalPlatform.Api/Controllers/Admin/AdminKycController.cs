using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Contracts.Admin;
using SmartRentalPlatform.Application.AdminApproval;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/kyc")]
public class AdminKycController : ControllerBase
{
    private readonly IAdminKycApprovalService _kycApprovalService;
    private readonly IApprovalAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public AdminKycController(
        IAdminKycApprovalService kycApprovalService,
        IApprovalAuditService auditService,
        ICurrentUserService currentUserService)
    {
        _kycApprovalService = kycApprovalService;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<AdminKycListResponse>>> GetPending(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(ValidationError("Thông tin phân trang không hợp lệ."));
        }

        var result = await _kycApprovalService.GetPendingAsync(pageNumber, pageSize, cancellationToken);
        return Ok(new ApiResponse<AdminKycListResponse>
        {
            Success = true,
            Message = "Tải danh sách KYC chờ duyệt thành công.",
            Data = result
        });
    }

    [HttpGet("{kycId:guid}")]
    public async Task<ActionResult<ApiResponse<AdminKycDetailResponse>>> GetDetail(
        Guid kycId,
        CancellationToken cancellationToken = default)
    {
        var result = await _kycApprovalService.GetDetailAsync(kycId, cancellationToken);
        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.KycNotFound,
                Message = "Không tìm thấy hồ sơ KYC."
            });
        }

        return Ok(new ApiResponse<AdminKycDetailResponse>
        {
            Success = true,
            Message = "Tải chi tiết KYC thành công.",
            Data = result
        });
    }

    [HttpPost("{kycId:guid}/approve")]
    public async Task<ActionResult<ApiResponse<object>>> Approve(
        Guid kycId,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        var success = await _kycApprovalService.ApproveAsync(kycId, adminId, cancellationToken);
        if (!success)
        {
            return BadRequest(ValidationError("Chỉ hồ sơ KYC đang chờ admin duyệt mới có thể được duyệt."));
        }

        await _auditService.LogAsync(adminId, "KYC", kycId, "Approved", cancellationToken: cancellationToken);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Duyệt KYC thành công.",
            Data = new { kycId }
        });
    }

    [HttpPost("{kycId:guid}/reject")]
    public async Task<ActionResult<ApiResponse<object>>> Reject(
        Guid kycId,
        AdminRejectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(ValidationError("Lý do từ chối là bắt buộc."));
        }

        var adminId = GetCurrentUserId();
        var success = await _kycApprovalService.RejectAsync(kycId, adminId, request.Reason, cancellationToken);
        if (!success)
        {
            return BadRequest(ValidationError("Chỉ hồ sơ KYC đang chờ admin duyệt mới có thể bị từ chối."));
        }

        await _auditService.LogAsync(adminId, "KYC", kycId, "Rejected", request.Reason, cancellationToken: cancellationToken);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Từ chối KYC thành công.",
            Data = new { kycId }
        });
    }

    [HttpGet("history/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<System.Collections.Generic.List<AdminKycDetailResponse>>>> GetHistory(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await _kycApprovalService.GetHistoryAsync(userId, cancellationToken);
        return Ok(new ApiResponse<System.Collections.Generic.List<AdminKycDetailResponse>>
        {
            Success = true,
            Message = "Tải lịch sử KYC thành công.",
            Data = result
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
