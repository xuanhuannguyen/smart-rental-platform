using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/contracts")]
public class RentalContractsController : ControllerBase
{
    private readonly IRentalContractService rentalContractService;
    private readonly IContractSignatureOtpService contractSignatureOtpService;
    private readonly IContractFileService contractFileService;
    private readonly IContractAppendixService contractAppendixService;
    private readonly ICurrentUserService currentUserService;

    public RentalContractsController(
        IRentalContractService rentalContractService,
        IContractSignatureOtpService contractSignatureOtpService,
        IContractFileService contractFileService,
        IContractAppendixService contractAppendixService,
        ICurrentUserService currentUserService)
    {
        this.rentalContractService = rentalContractService;
        this.contractSignatureOtpService = contractSignatureOtpService;
        this.contractFileService = contractFileService;
        this.contractAppendixService = contractAppendixService;
        this.currentUserService = currentUserService;
    }

    [Authorize]
    [HttpGet("my-history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ContractHistoryItemResponse>>>> GetMyHistory(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.GetMyHistoryAsync(userId, cancellationToken);

        return Ok(new ApiResponse<IReadOnlyCollection<ContractHistoryItemResponse>>
        {
            Success = true,
            Message = "Tải lịch sử hợp đồng thuê thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("landlord")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ContractHistoryItemResponse>>>> GetLandlordContracts(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.GetLandlordContractsAsync(userId, cancellationToken);

        return Ok(new ApiResponse<IReadOnlyCollection<ContractHistoryItemResponse>>
        {
            Success = true,
            Message = "Tải danh sách hợp đồng cho thuê thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ContractDetailResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.GetByIdAsync(userId, id, cancellationToken);
        return ContractResult(result, "Tải thông tin hợp đồng thành công.");
    }

    [Authorize]
    [HttpGet("{id:guid}/preview/pdf")]
    public async Task<IActionResult> GetPreviewPdf(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.GetPreviewPdfAsync(userId, id, cancellationToken);

        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RentalContractNotFound,
                Message = "Không tìm thấy hợp đồng."
            });
        }

        Response.Headers.ContentDisposition = $"inline; filename=\"{result.FileName}\"";
        return File(result.Content, result.ContentType);
    }

    [Authorize]
    [HttpPost("{id:guid}/occupants/submit")]
    public async Task<ActionResult<ApiResponse<ContractDetailResponse>>> SubmitOccupants(
        Guid id,
        SubmitContractOccupantsRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.SubmitOccupantsAsync(userId, id, request, cancellationToken);
        return ContractResult(result, "Cập nhật thông tin người ở thành công.");
    }

    [Authorize]
    [HttpPut("{id:guid}/terms")]
    public async Task<ActionResult<ApiResponse<ContractDetailResponse>>> UpdateTerms(
        Guid id,
        UpdateContractTermsRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.UpdateTermsAsync(userId, id, request, cancellationToken);
        return ContractResult(result, "Cập nhật điều khoản hợp đồng thành công.");
    }

    [Authorize]
    [HttpPost("{id:guid}/landlord-sign/otp")]
    public async Task<ActionResult<ApiResponse<RequestContractSignatureOtpResponse>>> RequestLandlordSignOtp(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractSignatureOtpService.RequestOtpAsync(
            userId,
            id,
            ContractSignerRole.Landlord,
            cancellationToken);

        return SignatureOtpResult(result, "OTP ký hợp đồng đã được gửi đến email của chủ trọ.");
    }

    [Authorize]
    [HttpPost("{id:guid}/landlord-sign")]
    public async Task<ActionResult<ApiResponse<ContractDetailResponse>>> LandlordSign(
        Guid id,
        SignContractRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.LandlordSignAsync(
            userId,
            id,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return ContractResult(result, "Chủ trọ ký hợp đồng thành công.");
    }

    [Authorize]
    [HttpPost("{id:guid}/tenant-sign/otp")]
    public async Task<ActionResult<ApiResponse<RequestContractSignatureOtpResponse>>> RequestTenantSignOtp(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractSignatureOtpService.RequestOtpAsync(
            userId,
            id,
            ContractSignerRole.Tenant,
            cancellationToken);

        return SignatureOtpResult(result, "OTP ký hợp đồng đã được gửi đến email của người thuê.");
    }

    [Authorize]
    [HttpPost("{id:guid}/tenant-sign")]
    public async Task<ActionResult<ApiResponse<ContractDetailResponse>>> TenantSign(
        Guid id,
        SignContractRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.TenantSignAsync(
            userId,
            id,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return ContractResult(result, "Người thuê ký hợp đồng thành công.");
    }

    [Authorize]
    [HttpPost("{id:guid}/revision-request")]
    public async Task<ActionResult<ApiResponse<ContractDetailResponse>>> RequestRevision(
        Guid id,
        RequestContractRevisionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.RequestRevisionAsync(userId, id, request, cancellationToken);
        return ContractResult(result, "Yêu cầu sửa hợp đồng thành công.");
    }

    [Authorize]
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<ContractDetailResponse>>> Reject(
        Guid id,
        RejectContractRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.RejectAsync(userId, id, request, cancellationToken);
        return ContractResult(result, "Từ chối hợp đồng thành công.");
    }

    [Authorize]
    [HttpPost("{id:guid}/files/generate")]
    public async Task<ActionResult<ApiResponse<ContractFileResponse>>> GenerateSignedContractFile(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractFileService.GenerateSignedContractFileAsync(userId, id, cancellationToken);

        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RentalContractNotFound,
                Message = "Không tìm thấy hợp đồng."
            });
        }

        return Ok(new ApiResponse<ContractFileResponse>
        {
            Success = true,
            Message = "Tạo file PDF hợp đồng thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("{id:guid}/files")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ContractFileResponse>>>> GetContractFiles(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractFileService.GetFilesAsync(userId, id, cancellationToken);

        return Ok(new ApiResponse<IReadOnlyCollection<ContractFileResponse>>
        {
            Success = true,
            Message = "Tải danh sách file hợp đồng thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("{id:guid}/files/{fileId:guid}/download")]
    public async Task<IActionResult> DownloadContractFile(
        Guid id,
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractFileService.OpenFileAsync(userId, id, fileId, cancellationToken);

        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.NotFound,
                Message = "Không tìm thấy file hợp đồng."
            });
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [Authorize]
    [HttpGet("{id:guid}/files/{fileId:guid}/view-url")]
    public async Task<ActionResult<ApiResponse<ContractFileViewUrlResponse>>> GetContractFileViewUrl(
        Guid id,
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractFileService.GetFileViewUrlAsync(userId, id, fileId, cancellationToken);

        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.NotFound,
                Message = "Không tìm thấy file hợp đồng."
            });
        }

        return Ok(new ApiResponse<ContractFileViewUrlResponse>
        {
            Success = true,
            Message = "Lấy đường xem file hợp đồng thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("{id:guid}/appendices")]
    public async Task<ActionResult<ApiResponse<ContractAppendixResponse>>> CreateAppendix(
        Guid id,
        CreateContractAppendixRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractAppendixService.CreateAsync(userId, id, request, cancellationToken);
        return AppendixResult(result, "Tạo phụ lục hợp đồng thành công.");
    }

    [Authorize]
    [HttpGet("{id:guid}/appendices")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ContractAppendixResponse>>>> GetAppendices(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractAppendixService.GetByContractAsync(userId, id, cancellationToken);

        return Ok(new ApiResponse<IReadOnlyCollection<ContractAppendixResponse>>
        {
            Success = true,
            Message = "Tải danh sách phụ lục hợp đồng thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPut("{id:guid}/appendices/{appendixId:guid}")]
    public async Task<ActionResult<ApiResponse<ContractAppendixResponse>>> UpdateAppendix(
        Guid id,
        Guid appendixId,
        CreateContractAppendixRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractAppendixService.UpdateAsync(userId, id, appendixId, request, cancellationToken);
        return AppendixResult(result, "Cập nhật phụ lục hợp đồng thành công.");
    }

    [Authorize]
    [HttpGet("{id:guid}/appendices/{appendixId:guid}")]
    public async Task<ActionResult<ApiResponse<ContractAppendixResponse>>> GetAppendix(
        Guid id,
        Guid appendixId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractAppendixService.GetByIdAsync(userId, id, appendixId, cancellationToken);
        return AppendixResult(result, "Tải thông tin phụ lục hợp đồng thành công.");
    }

    [Authorize]
    [HttpGet("{id:guid}/appendices/{appendixId:guid}/preview/pdf")]
    public async Task<IActionResult> GetAppendixPreviewPdf(
        Guid id,
        Guid appendixId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractAppendixService.GetPreviewPdfAsync(userId, id, appendixId, cancellationToken);

        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.ContractAppendixNotFound,
                Message = "Không tìm thấy phụ lục hợp đồng."
            });
        }

        Response.Headers.ContentDisposition = $"inline; filename=\"{result.FileName}\"";
        return File(result.Content, result.ContentType);
    }

    [Authorize]
    [HttpPost("{id:guid}/appendices/{appendixId:guid}/sign/otp")]
    public async Task<ActionResult<ApiResponse<RequestContractSignatureOtpResponse>>> RequestAppendixSignOtp(
        Guid id,
        Guid appendixId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractAppendixService.RequestSignOtpAsync(userId, id, appendixId, cancellationToken);

        return SignatureOtpResult(result, "OTP ký phụ lục đã được gửi đến email.");
    }

    [Authorize]
    [HttpPost("{id:guid}/appendices/{appendixId:guid}/sign")]
    public async Task<ActionResult<ApiResponse<ContractAppendixResponse>>> SignAppendix(
        Guid id,
        Guid appendixId,
        SignContractRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractAppendixService.SignAsync(
            userId,
            id,
            appendixId,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return AppendixResult(result, "Ký phụ lục hợp đồng thành công.");
    }

    [Authorize]
    [HttpPost("{id:guid}/appendices/{appendixId:guid}/reject")]
    public async Task<ActionResult<ApiResponse<ContractAppendixResponse>>> RejectAppendix(
        Guid id,
        Guid appendixId,
        RejectContractRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractAppendixService.RejectAsync(userId, id, appendixId, request, cancellationToken);
        return AppendixResult(result, "Từ chối phụ lục hợp đồng thành công.");
    }

    [Authorize]
    [HttpPost("{id:guid}/appendices/{appendixId:guid}/revision-request")]
    public async Task<ActionResult<ApiResponse<ContractAppendixResponse>>> RequestAppendixRevision(
        Guid id,
        Guid appendixId,
        RequestContractRevisionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractAppendixService.RequestRevisionAsync(userId, id, appendixId, request, cancellationToken);
        return AppendixResult(result, "Yêu cầu sửa phụ lục hợp đồng thành công.");
    }

    [Authorize]
    [HttpDelete("{id:guid}/appendices/{appendixId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAppendix(
        [FromRoute] Guid id,
        [FromRoute] Guid appendixId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var success = await contractAppendixService.DeleteAsync(userId, id, appendixId, cancellationToken);

        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/terminate")]
    public async Task<ActionResult<ApiResponse<ContractDetailResponse>>> Terminate(
        Guid id,
        TerminateContractRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await rentalContractService.TerminateAsync(userId, id, request, cancellationToken);
        return ContractResult(result, "Thanh lý hợp đồng thành công.");
    }

    private Guid GetCurrentUserId()
    {
        return currentUserService.GetRequiredUserId("Không tìm thấy mã người dùng đã đăng nhập.");
    }

    private ActionResult<ApiResponse<ContractDetailResponse>> ContractResult(
        ContractDetailResponse? result,
        string message)
    {
        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RentalContractNotFound,
                Message = "Không tìm thấy hợp đồng."
            });
        }

        return Ok(new ApiResponse<ContractDetailResponse>
        {
            Success = true,
            Message = message,
            Data = result
        });
    }

    private ActionResult<ApiResponse<RequestContractSignatureOtpResponse>> SignatureOtpResult(
        RequestContractSignatureOtpResponse? result,
        string message)
    {
        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RentalContractNotFound,
                Message = "Không tìm thấy hợp đồng."
            });
        }

        return Ok(new ApiResponse<RequestContractSignatureOtpResponse>
        {
            Success = true,
            Message = message,
            Data = result
        });
    }

    private ActionResult<ApiResponse<ContractAppendixResponse>> AppendixResult(
        ContractAppendixResponse? result,
        string message)
    {
        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.ContractAppendixNotFound,
                Message = "Không tìm thấy phụ lục hợp đồng."
            });
        }

        return Ok(new ApiResponse<ContractAppendixResponse>
        {
            Success = true,
            Message = message,
            Data = result
        });
    }
}

