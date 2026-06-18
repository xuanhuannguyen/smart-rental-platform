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
    [HttpGet("landlord/rooming-houses/{id:guid}/service-prices")]
    public async Task<ActionResult<ApiResponse<List<ServicePriceResponse>>>> GetServicePrices(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GetServicePricesAsync(GetCurrentUserId(), id, cancellationToken);

        return Ok(new ApiResponse<List<ServicePriceResponse>>
        {
            Success = true,
            Message = "Tai bang gia dich vu thanh cong.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("landlord/rooming-houses/{id:guid}/service-prices")]
    public async Task<ActionResult<ApiResponse<ServicePriceResponse>>> CreateServicePrice(
        Guid id,
        CreateServicePriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingService.CreateServicePriceAsync(GetCurrentUserId(), id, request, cancellationToken);

        return Ok(new ApiResponse<ServicePriceResponse>
        {
            Success = true,
            Message = "Tao bang gia dich vu moi thanh cong.",
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
    [HttpPost("landlord/meter-readings")]
    public async Task<ActionResult<ApiResponse<MeterReadingResponse>>> CreateMeterReading(
        CreateMeterReadingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingService.CreateMeterReadingAsync(GetCurrentUserId(), request, cancellationToken);

        return Ok(new ApiResponse<MeterReadingResponse>
        {
            Success = true,
            Message = "Ghi chi so dich vu thanh cong.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("landlord/invoices/generate-draft")]
    public async Task<ActionResult<ApiResponse<InvoiceResponse>>> GenerateDraftInvoice(
        GenerateInvoiceDraftRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GenerateDraftInvoiceAsync(GetCurrentUserId(), request, cancellationToken);

        return Ok(new ApiResponse<InvoiceResponse>
        {
            Success = true,
            Message = "Tao hoa don nhap thanh cong.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("landlord/invoices")]
    public async Task<ActionResult<ApiResponse<List<InvoiceResponse>>>> GetLandlordInvoices(
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GetLandlordInvoicesAsync(GetCurrentUserId(), status, search, cancellationToken);

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
