using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.ViewingAppointments;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.ViewingAppointments.Requests;
using SmartRentalPlatform.Contracts.ViewingAppointments.Responses;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartRentalPlatform.Api.Controllers.Properties
{
    [ApiController]
    [Route("api")]
    public class ViewingAppointmentsController : ControllerBase
    {
        private readonly IViewingAppointmentService _viewingAppointmentService;
        private readonly ICurrentUserService _currentUserService;

        public ViewingAppointmentsController(
            IViewingAppointmentService viewingAppointmentService,
            ICurrentUserService currentUserService)
        {
            _viewingAppointmentService = viewingAppointmentService;
            _currentUserService = currentUserService;
        }

        [Authorize]
        [HttpPost("viewing-appointments")]
        public async Task<ActionResult<ApiResponse<ViewingAppointmentResponse>>> Create(
            CreateViewingAppointmentRequest request,
            CancellationToken cancellationToken)
        {
            var tenantUserId = GetCurrentUserId();
            var result = await _viewingAppointmentService.CreateAsync(tenantUserId, request, cancellationToken);

            return Ok(new ApiResponse<ViewingAppointmentResponse>
            {
                Success = true,
                Message = "Đặt lịch xem phòng thành công, vui lòng chờ Landlord xác nhận.",
                Data = result
            });
        }

        [Authorize]
        [HttpGet("me/viewing-appointments")]
        public async Task<ActionResult<ApiResponse<List<ViewingAppointmentResponse>>>> GetMyAppointments(
            CancellationToken cancellationToken)
        {
            var tenantUserId = GetCurrentUserId();
            var result = await _viewingAppointmentService.GetMyAppointmentsAsync(tenantUserId, cancellationToken);

            return Ok(new ApiResponse<List<ViewingAppointmentResponse>>
            {
                Success = true,
                Message = "Tải danh sách lịch xem phòng của bạn thành công.",
                Data = result
            });
        }

        [Authorize]
        [HttpGet("landlord/viewing-appointments")]
        public async Task<ActionResult<ApiResponse<List<ViewingAppointmentResponse>>>> GetLandlordAppointments(
            [FromQuery] string? status,
            CancellationToken cancellationToken)
        {
            var landlordUserId = GetCurrentUserId();
            var result = await _viewingAppointmentService.GetLandlordAppointmentsAsync(landlordUserId, status, cancellationToken);

            return Ok(new ApiResponse<List<ViewingAppointmentResponse>>
            {
                Success = true,
                Message = "Tải danh sách lịch hẹn xem phòng thành công.",
                Data = result
            });
        }

        [Authorize]
        [HttpGet("landlord/viewing-appointments/{id:guid}/conflict-check")]
        public async Task<ActionResult<ApiResponse<ConflictCheckResponse>>> CheckConflict(
            Guid id,
            CancellationToken cancellationToken)
        {
            var landlordUserId = GetCurrentUserId();
            var result = await _viewingAppointmentService.CheckConflictAsync(landlordUserId, id, cancellationToken);

            return Ok(new ApiResponse<ConflictCheckResponse>
            {
                Success = true,
                Message = "Kiểm tra trùng lịch thành công.",
                Data = result
            });
        }

        [Authorize]
        [HttpPost("landlord/viewing-appointments/{id:guid}/confirm")]
        public async Task<ActionResult<ApiResponse<ViewingAppointmentResponse>>> Confirm(
            Guid id,
            ConfirmViewingAppointmentRequest request,
            CancellationToken cancellationToken)
        {
            var landlordUserId = GetCurrentUserId();
            var result = await _viewingAppointmentService.ConfirmAsync(landlordUserId, id, request, cancellationToken);

            return Ok(new ApiResponse<ViewingAppointmentResponse>
            {
                Success = true,
                Message = "Xác nhận lịch xem phòng thành công.",
                Data = result
            });
        }

        [Authorize]
        [HttpPost("landlord/viewing-appointments/{id:guid}/reject")]
        public async Task<ActionResult<ApiResponse<ViewingAppointmentResponse>>> Reject(
            Guid id,
            RejectViewingAppointmentRequest request,
            CancellationToken cancellationToken)
        {
            var landlordUserId = GetCurrentUserId();
            var result = await _viewingAppointmentService.RejectAsync(landlordUserId, id, request, cancellationToken);

            return Ok(new ApiResponse<ViewingAppointmentResponse>
            {
                Success = true,
                Message = "Đã từ chối lịch xem phòng.",
                Data = result
            });
        }

        [Authorize]
        [HttpPost("viewing-appointments/{id:guid}/cancel")]
        public async Task<ActionResult<ApiResponse<ViewingAppointmentResponse>>> CancelByTenant(
            Guid id,
            CancelViewingAppointmentRequest request,
            CancellationToken cancellationToken)
        {
            var tenantUserId = GetCurrentUserId();
            var result = await _viewingAppointmentService.CancelByTenantAsync(tenantUserId, id, request, cancellationToken);

            return Ok(new ApiResponse<ViewingAppointmentResponse>
            {
                Success = true,
                Message = "Đã hủy lịch xem phòng thành công.",
                Data = result
            });
        }

        [Authorize]
        [HttpPost("landlord/viewing-appointments/{id:guid}/cancel")]
        public async Task<ActionResult<ApiResponse<ViewingAppointmentResponse>>> CancelByLandlord(
            Guid id,
            CancelViewingAppointmentRequest request,
            CancellationToken cancellationToken)
        {
            var landlordUserId = GetCurrentUserId();
            var result = await _viewingAppointmentService.CancelByLandlordAsync(landlordUserId, id, request, cancellationToken);

            return Ok(new ApiResponse<ViewingAppointmentResponse>
            {
                Success = true,
                Message = "Đã hủy lịch xem phòng thành công.",
                Data = result
            });
        }

        [Authorize]
        [HttpPost("landlord/viewing-appointments/{id:guid}/complete")]
        public async Task<ActionResult<ApiResponse<ViewingAppointmentResponse>>> Complete(
            Guid id,
            CancellationToken cancellationToken)
        {
            var landlordUserId = GetCurrentUserId();
            var result = await _viewingAppointmentService.CompleteAsync(landlordUserId, id, cancellationToken);

            return Ok(new ApiResponse<ViewingAppointmentResponse>
            {
                Success = true,
                Message = "Đã đánh dấu hoàn tất buổi xem phòng.",
                Data = result
            });
        }

        [Authorize]
        [HttpPost("viewing-appointments/{id:guid}/accept-proposal")]
        public async Task<ActionResult<ApiResponse<ViewingAppointmentResponse>>> AcceptProposal(
            Guid id,
            CancellationToken cancellationToken)
        {
            var tenantUserId = GetCurrentUserId();
            var result = await _viewingAppointmentService.AcceptProposalAsync(tenantUserId, id, cancellationToken);

            return Ok(new ApiResponse<ViewingAppointmentResponse>
            {
                Success = true,
                Message = "Bạn đã chấp nhận đề xuất lịch xem phòng mới.",
                Data = result
            });
        }

        [Authorize]
        [HttpPost("viewing-appointments/{id:guid}/reject-proposal")]
        public async Task<ActionResult<ApiResponse<ViewingAppointmentResponse>>> RejectProposal(
            Guid id,
            CancellationToken cancellationToken)
        {
            var tenantUserId = GetCurrentUserId();
            var result = await _viewingAppointmentService.RejectProposalAsync(tenantUserId, id, cancellationToken);

            return Ok(new ApiResponse<ViewingAppointmentResponse>
            {
                Success = true,
                Message = "Bạn đã từ chối đề xuất lịch xem phòng.",
                Data = result
            });
        }

        private Guid GetCurrentUserId()
        {
            return _currentUserService.GetRequiredUserId("Không tìm thấy mã người dùng đã đăng nhập.");
        }
    }
}
