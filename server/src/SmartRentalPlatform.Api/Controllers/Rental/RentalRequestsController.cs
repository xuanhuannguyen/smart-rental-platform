using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.RentalRequests;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalRequests.Requests;
using SmartRentalPlatform.Contracts.RentalRequests.Responses;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api")]
public class RentalRequestsController : ControllerBase
{
    private readonly IRentalRequestService rentalRequestService;
    private readonly ICurrentUserService currentUserService;

    public RentalRequestsController(
        IRentalRequestService rentalRequestService,
        ICurrentUserService currentUserService)
    {
        this.rentalRequestService = rentalRequestService;
        this.currentUserService = currentUserService;
    }

    [Authorize]
    [HttpPost("rooms/{roomId:guid}/rental-requests")]
    public async Task<ActionResult<ApiResponse<RentalRequestResponse>>> Create(
        Guid roomId,
        CreateRentalRequestRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalRequestService.CreateAsync(userId, roomId, request, cancellationToken);

        return Ok(new ApiResponse<RentalRequestResponse>
        {
            Success = true,
            Message = "Gửi yêu cầu thuê phòng thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("rental-requests/my")]
    public async Task<ActionResult<ApiResponse<List<RentalRequestResponse>>>> GetMyRequests(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalRequestService.GetMyRequestsAsync(userId, cancellationToken);

        return Ok(new ApiResponse<List<RentalRequestResponse>>
        {
            Success = true,
            Message = "Tải danh sách yêu cầu thuê của tôi thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("rental-requests/incoming")]
    public async Task<ActionResult<ApiResponse<List<RentalRequestResponse>>>> GetIncomingRequests(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalRequestService.GetIncomingRequestsAsync(userId, cancellationToken);

        return Ok(new ApiResponse<List<RentalRequestResponse>>
        {
            Success = true,
            Message = "Tải danh sách yêu cầu thuê gửi tới tôi thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("rental-requests/{id:guid}")]
    public async Task<ActionResult<ApiResponse<RentalRequestResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalRequestService.GetByIdAsync(userId, id, cancellationToken);
        return RentalRequestResult(result, "Tải thông tin yêu cầu thuê thành công.");
    }

    [Authorize]
    [HttpPost("rental-requests/{id:guid}/approve")]
    public async Task<ActionResult<ApiResponse<RentalRequestResponse>>> Approve(
        Guid id,
        ApproveRentalRequestRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalRequestService.ApproveAsync(userId, id, request, cancellationToken);
        return RentalRequestResult(result, "Duyệt yêu cầu thuê thành công và đã tạo khoản cọc cho người thuê.");
    }

    [Authorize]
    [HttpPost("rental-requests/{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<RentalRequestResponse>>> Reject(
        Guid id,
        RejectRentalRequestRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalRequestService.RejectAsync(userId, id, request, cancellationToken);
        return RentalRequestResult(result, "Từ chối yêu cầu thuê thành công.");
    }

    [Authorize]
    [HttpPost("rental-requests/{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<RentalRequestResponse>>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalRequestService.CancelAsync(userId, id, cancellationToken);
        return RentalRequestResult(result, "Hủy yêu cầu thuê thành công.");
    }

    private Guid GetCurrentUserId()
    {
        return currentUserService.GetRequiredUserId("Không tìm thấy mã người dùng đã đăng nhập.");
    }

    private ActionResult<ApiResponse<RentalRequestResponse>> RentalRequestResult(
        RentalRequestResponse? result,
        string message)
    {
        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RentalRequestNotFound,
                Message = "Không tìm thấy yêu cầu thuê."
            });
        }

        return Ok(new ApiResponse<RentalRequestResponse>
        {
            Success = true,
            Message = message,
            Data = result
        });
    }
}
