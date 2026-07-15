using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Models.ESign;
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
    private readonly IContractFileService contractFileService;
    private readonly IContractAppendixService contractAppendixService;
    private readonly IContractESignService contractESignService;
    private readonly ICurrentUserService currentUserService;

    public RentalContractsController(
        IRentalContractService rentalContractService,
        IContractFileService contractFileService,
        IContractAppendixService contractAppendixService,
        IContractESignService contractESignService,
        ICurrentUserService currentUserService)
    {
        this.rentalContractService = rentalContractService;
        this.contractFileService = contractFileService;
        this.contractAppendixService = contractAppendixService;
        this.contractESignService = contractESignService;
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

        SetSensitivePreviewHeaders(result.FileName);
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
    public async Task<ActionResult<ApiResponse<ContractFileResponse>>> GenerateContractReferenceFile(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractFileService.EnsureMaskedContractFileAsync(userId, id, cancellationToken);

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
            Message = "Tạo bản tham chiếu đã che thông tin thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("{id:guid}/esign-envelope")]
    public async Task<ActionResult<ApiResponse<StartESignEnvelopeResponse>>> StartESignEnvelope(
        Guid id,
        [FromBody] StartESignEnvelopeRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.AgreedToTerms)
        {
            return BadRequest(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Bạn phải đồng ý với nội dung hợp đồng trước khi ký."
            });
        }

        var userId = GetCurrentUserId();
        var result = await contractESignService.StartContractEnvelopeAsync(userId, id, request.ReturnUrl, cancellationToken);

        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RentalContractNotFound,
                Message = "Không tìm thấy hợp đồng."
            });
        }

        return Ok(new ApiResponse<StartESignEnvelopeResponse>
        {
            Success = true,
            Message = "Khởi tạo trình ký số thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("{id:guid}/esign-envelope/request-otp")]
    public async Task<ActionResult<ApiResponse<RequestESignOtpResponse>>> RequestESignOtp(
        Guid id,
        [FromBody] RequestSignatureOtpRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractESignService.RequestSignatureOtpAsync(
            userId, id, null, (ESignOtpMethod)request.Method, cancellationToken);

        return Ok(new ApiResponse<RequestESignOtpResponse>
        {
            Success = true,
            Message = "VNPT đã gửi mã OTP.",
            Data = result
        });
    }

    [HttpPost("{id:guid}/esign-envelope/submit-otp")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> SubmitESignOtp(
        Guid id,
        [FromBody] SubmitSignatureOtpRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await contractESignService.SubmitSignatureOtpAsync(
            userId, id, null, request.OtpCode, request.SignatureImageBase64, cancellationToken);

        return Ok(new ApiResponse<bool>
        {
            Success = true,
            Message = "Ký số thành công.",
            Data = true
        });
    }

    [Authorize]
    [HttpGet("{id:guid}/esign-envelopes/{envelopeId:guid}")]
    public async Task<ActionResult<ApiResponse<ESignEnvelopeResponse>>> GetESignEnvelope(
        Guid id,
        Guid envelopeId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractESignService.GetEnvelopeAsync(userId, envelopeId, cancellationToken);

        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RentalContractNotFound,
                Message = "Không tìm thấy thông tin ký số."
            });
        }

        return Ok(new ApiResponse<ESignEnvelopeResponse>
        {
            Success = true,
            Message = "Lấy thông tin ký số thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("{id:guid}/appendices/{appendixId:guid}/esign-envelope/request-otp")]
    public async Task<ActionResult<ApiResponse<RequestESignOtpResponse>>> RequestAppendixESignOtp(
        Guid id,
        Guid appendixId,
        [FromBody] RequestSignatureOtpRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await contractESignService.RequestSignatureOtpAsync(
            userId, id, appendixId, (ESignOtpMethod)request.Method, cancellationToken);
        return Ok(new ApiResponse<RequestESignOtpResponse>
        {
            Success = true,
            Message = "VNPT đã gửi mã OTP.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("{id:guid}/appendices/{appendixId:guid}/esign-envelope/submit-otp")]
    public async Task<ActionResult<ApiResponse<bool>>> SubmitAppendixESignOtp(
        Guid id,
        Guid appendixId,
        [FromBody] SubmitSignatureOtpRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await contractESignService.SubmitSignatureOtpAsync(
            userId, id, appendixId, request.OtpCode, request.SignatureImageBase64, cancellationToken);
        return Ok(new ApiResponse<bool>
        {
            Success = true,
            Message = "Ký phụ lục thành công.",
            Data = true
        });
    }

    [Authorize]
    [HttpPost("{id:guid}/appendices/{appendixId:guid}/esign-envelope")]
    public async Task<ActionResult<ApiResponse<StartESignEnvelopeResponse>>> StartAppendixESignEnvelope(
        Guid id,
        Guid appendixId,
        [FromBody] StartESignEnvelopeRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.AgreedToTerms)
        {
            return BadRequest(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Bạn phải đồng ý với nội dung phụ lục trước khi ký."
            });
        }

        var userId = GetCurrentUserId();
        var result = await contractESignService.StartAppendixEnvelopeAsync(userId, id, appendixId, request.ReturnUrl, cancellationToken);

        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RentalContractNotFound,
                Message = "Không tìm thấy phụ lục hợp đồng."
            });
        }

        return Ok(new ApiResponse<StartESignEnvelopeResponse>
        {
            Success = true,
            Message = "Khởi tạo trình ký số cho phụ lục thành công.",
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

        SetSensitivePreviewHeaders(result.FileName);
        return File(result.Content, result.ContentType);
    }

    private void SetSensitivePreviewHeaders(string fileName)
    {
        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
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

