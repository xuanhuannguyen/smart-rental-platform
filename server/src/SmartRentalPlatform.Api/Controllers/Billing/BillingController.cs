using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Billing;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Api.Extensions;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api")]
public class BillingController : ControllerBase
{
    private const long MaxMeterImageSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedMeterImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };
    private static readonly HashSet<string> AllowedMeterImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png"
    };
    private readonly IBillingService billingService;
    private readonly IMeterReadingAiService meterReadingAiService;
    private readonly ICurrentUserService currentUserService;

    public BillingController(
        IBillingService billingService,
        IMeterReadingAiService meterReadingAiService,
        ICurrentUserService currentUserService)
    {
        this.billingService = billingService;
        this.meterReadingAiService = meterReadingAiService;
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
            Message = "Tải thông tin phòng và hợp đồng active thành công.",
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
            Message = "Tải thông tin xem trước hóa đơn thành công.",
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
            Message = "Tải danh sách hóa đơn thành công.",
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
            Message = "Tải chi tiết hóa đơn thành công.",
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
            Message = "Phát hành hóa đơn thành công.",
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
            Message = "Hủy hóa đơn thành công.",
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
            Message = "Tải danh sách hóa đơn thành công.",
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
            Message = "Tải chi tiết hóa đơn thành công.",
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
            Message = "Thanh toán hóa đơn thành công.",
            Data = result
        });
    }

    private Guid GetCurrentUserId()
    {
        return currentUserService.GetRequiredUserId("Không tìm thấy người dùng đang đăng nhập.");
    }

    [Authorize]
    [HttpPost("landlord/meter-readings/ai")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxMeterImageSizeBytes)]
    public async Task<ActionResult<ApiResponse<MeterAiResponse>>> ReadMeterImage(
        [FromForm] MeterAiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(BuildValidationError("Vui lòng chọn ảnh đồng hồ.", "file"));
        }

        if (request.File.Length > MaxMeterImageSizeBytes)
        {
            return BadRequest(BuildValidationError("Dung lượng ảnh không được vượt quá 5MB.", "file"));
        }

        var extension = Path.GetExtension(request.File.FileName);
        if (!AllowedMeterImageExtensions.Contains(extension) ||
            !AllowedMeterImageContentTypes.Contains(request.File.ContentType))
        {
            return BadRequest(BuildValidationError("Chỉ hỗ trợ ảnh đồng hồ JPG hoặc PNG.", "file"));
        }

        var result = await meterReadingAiService.ReadAsync(
            GetCurrentUserId(),
            request.ContractId,
            request.ServiceTypeId,
            request.BillingPeriodStart,
            new SmartRentalPlatform.Application.Common.Models.ImageUploadFile
            {
                Content = request.File.OpenReadStream(),
                FileName = request.File.FileName,
                ContentType = request.File.ContentType,
                Length = request.File.Length
            },
            cancellationToken);

        return Ok(new ApiResponse<MeterAiResponse>
        {
            Success = true,
            Message = "Nhận diện chỉ số từ ảnh thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("landlord/invoices/generate-bulk")]
    public async Task<ActionResult<ApiResponse<BulkInvoiceResultResponse>>> GenerateBulkInvoices(
        GenerateBulkInvoicesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingService.GenerateBulkInvoicesAsync(
            GetCurrentUserId(), request, cancellationToken);

        return Ok(new ApiResponse<BulkInvoiceResultResponse>
        {
            Success = true,
            Message = $"Đã tạo {result.CreatedCount} hóa đơn nháp; bỏ qua {result.SkippedCount}; thiếu dữ liệu {result.MissingDataCount}.",
            Data = result
        });
    }

    private static ApiErrorResponse BuildValidationError(string message, string field)
    {
        return new ApiErrorResponse
        {
            Success = false,
            ErrorCode = ErrorCodes.ValidationError,
            Message = message,
            Details = new { field }
        };
    }
}

public sealed class MeterAiRequest
{
    public Guid ContractId { get; set; }
    public Guid ServiceTypeId { get; set; }
    public DateOnly BillingPeriodStart { get; set; }
    public IFormFile File { get; set; } = default!;
}

