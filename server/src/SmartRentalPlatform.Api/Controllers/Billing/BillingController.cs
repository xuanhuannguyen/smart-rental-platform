using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Billing;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api")]
public class BillingController : ControllerBase
{
    private readonly IBillingService billingService;
    private readonly ICurrentUserService currentUserService;

    public BillingController(
        IBillingService billingService,
        ICurrentUserService currentUserService)
    {
        this.billingService = billingService;
        this.currentUserService = currentUserService;
    }

    [Authorize]
    [HttpGet("billing/service-types")]
    public async Task<ActionResult<ApiResponse<List<BillingServiceTypeResponse>>>> GetBillingServiceTypes(
        CancellationToken cancellationToken)
    {
        var result = await billingService.GetBillingServiceTypesAsync(cancellationToken);

        return Ok(new ApiResponse<List<BillingServiceTypeResponse>>
        {
            Success = true,
            Message = "Tải danh sách loại dịch vụ thành công.",
            Data = result
        });
    }


    [Authorize]
    [HttpGet("landlord/rooms/{roomId:guid}/billing-context")]
    public async Task<ActionResult<ApiResponse<RoomBillingContextResponse>>> GetRoomBillingContext(
        Guid roomId,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GetRoomBillingContextAsync(GetCurrentUserId(), roomId, cancellationToken);

        return Ok(new ApiResponse<RoomBillingContextResponse>
        {
            Success = true,
            Message = "Tai thong tin phong va hop dong active thanh cong.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("landlord/rooms/{roomId:guid}/invoice-preview")]
    public async Task<ActionResult<ApiResponse<RoomInvoicePreviewResponse>>> GetRoomInvoicePreview(
        Guid roomId,
        [FromQuery] DateOnly billingPeriodStart,
        [FromQuery] DateOnly? billingPeriodEnd,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GetRoomInvoicePreviewAsync(
            GetCurrentUserId(),
            roomId,
            billingPeriodStart,
            billingPeriodEnd,
            cancellationToken);

        return Ok(new ApiResponse<RoomInvoicePreviewResponse>
        {
            Success = true,
            Message = "Tai thong tin xem truoc hoa don thanh cong.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("landlord/contracts/{contractId:guid}/termination-invoice-preview")]
    public async Task<ActionResult<ApiResponse<RoomInvoicePreviewResponse>>> GetTerminationInvoicePreview(
        Guid contractId,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GetTerminationInvoicePreviewAsync(
            GetCurrentUserId(),
            contractId,
            cancellationToken);

        return Ok(new ApiResponse<RoomInvoicePreviewResponse>
        {
            Success = true,
            Message = "Tải thông tin hóa đơn cần tạo thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Tạo hóa đơn kết hợp nhập chỉ số điện/nước trong một bước.
    /// Các dịch vụ MeterReading có giá hiệu lực trong kỳ bắt buộc phải có chỉ số.
    /// MeterReading và Invoice được tạo atomic trong cùng một transaction.
    /// </summary>
    [Authorize]
    [HttpPost("landlord/invoices/generate-with-readings")]
    public async Task<ActionResult<ApiResponse<InvoiceResponse>>> GenerateInvoiceWithReadings(
        GenerateInvoiceWithReadingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GenerateInvoiceWithReadingsAsync(
            GetCurrentUserId(), request, cancellationToken);

        return Ok(new ApiResponse<InvoiceResponse>
        {
            Success = true,
            Message = "Tạo hóa đơn thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("landlord/invoices")]
    public async Task<ActionResult<ApiResponse<List<InvoiceResponse>>>> GetLandlordInvoices(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] Guid? contractId,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GetLandlordInvoicesAsync(GetCurrentUserId(), status, search, contractId, cancellationToken);

        return Ok(new ApiResponse<List<InvoiceResponse>>
        {
            Success = true,
            Message = "Tai danh sach hoa don thanh cong.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("landlord/invoices/{id:guid}")]
    public async Task<ActionResult<ApiResponse<InvoiceResponse>>> GetLandlordInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GetLandlordInvoiceAsync(GetCurrentUserId(), id, cancellationToken);

        return Ok(new ApiResponse<InvoiceResponse>
        {
            Success = true,
            Message = "Tai chi tiet hoa don thanh cong.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("landlord/invoices/{id:guid}/issue")]
    public async Task<ActionResult<ApiResponse<InvoiceResponse>>> IssueInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await billingService.IssueInvoiceAsync(GetCurrentUserId(), id, cancellationToken);

        return Ok(new ApiResponse<InvoiceResponse>
        {
            Success = true,
            Message = "Phat hanh hoa don thanh cong.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("landlord/invoices/{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<InvoiceResponse>>> CancelInvoice(
        Guid id,
        CancelInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingService.CancelInvoiceAsync(GetCurrentUserId(), id, request.Reason, cancellationToken);

        return Ok(new ApiResponse<InvoiceResponse>
        {
            Success = true,
            Message = "Huy hoa don thanh cong.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("me/invoices")]
    public async Task<ActionResult<ApiResponse<List<InvoiceResponse>>>> GetMyInvoices(
        CancellationToken cancellationToken)
    {
        var result = await billingService.GetMyInvoicesAsync(GetCurrentUserId(), cancellationToken);

        return Ok(new ApiResponse<List<InvoiceResponse>>
        {
            Success = true,
            Message = "Tai danh sach hoa don thanh cong.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("landlord/contracts/{contractId:guid}/termination-invoices")]
    public async Task<ActionResult<ApiResponse<InvoiceResponse>>> CreateNextTerminationInvoice(
        Guid contractId,
        CreateTerminationInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingService.CreateNextTerminationInvoiceAsync(
            GetCurrentUserId(),
            contractId,
            request,
            cancellationToken);

        return Ok(new ApiResponse<InvoiceResponse>
        {
            Success = true,
            Message = "Tạo hóa đơn thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("me/contracts/{contractId:guid}/invoices")]
    public async Task<ActionResult<ApiResponse<List<InvoiceResponse>>>> GetMyContractInvoices(
        Guid contractId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GetMyContractInvoicesAsync(GetCurrentUserId(), contractId, status, cancellationToken);

        return Ok(new ApiResponse<List<InvoiceResponse>>
        {
            Success = true,
            Message = "Tải danh sách hóa đơn hợp đồng thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("me/invoices/{id:guid}")]
    public async Task<ActionResult<ApiResponse<InvoiceResponse>>> GetMyInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GetMyInvoiceAsync(GetCurrentUserId(), id, cancellationToken);

        return Ok(new ApiResponse<InvoiceResponse>
        {
            Success = true,
            Message = "Tai chi tiet hoa don thanh cong.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("me/invoices/{id:guid}/pay")]
    public async Task<ActionResult<ApiResponse<InvoiceResponse>>> PayInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await billingService.PayInvoiceAsync(GetCurrentUserId(), id, cancellationToken);

        return Ok(new ApiResponse<InvoiceResponse>
        {
            Success = true,
            Message = "Thanh toan hoa don thanh cong.",
            Data = result
        });
    }

    private Guid GetCurrentUserId()
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            throw new UnauthorizedAccessException("Khong tim thay nguoi dung dang dang nhap.");
        }

        return currentUserService.UserId.Value;
    }
}
