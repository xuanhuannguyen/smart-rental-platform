using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Kyc;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Kyc;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/kyc")]
public class KycController : ControllerBase
{
    private readonly IKycService _kycService;
    private readonly IVnptEkycClient _vnptEkycClient;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWebHostEnvironment _environment;

    public KycController(
        IKycService kycService,
        IVnptEkycClient vnptEkycClient,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment)
    {
        _kycService = kycService;
        _vnptEkycClient = vnptEkycClient;
        _currentUserService = currentUserService;
        _environment = environment;
    }

    [HttpPost("submissions")]
    public async Task<ActionResult<ApiResponse<KycSubmissionResponse>>> Submit(
        [FromBody] SubmitKycRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _kycService.SubmitAsync(userId, request, cancellationToken);

        return Ok(new ApiResponse<KycSubmissionResponse>
        {
            Success = true,
            Message = result.Message,
            Data = result
        });
    }

    [HttpGet("my-status")]
    public async Task<ActionResult<ApiResponse<KycStatusResponse>>> GetMyStatus(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _kycService.GetMyStatusAsync(userId, cancellationToken);

        return Ok(new ApiResponse<KycStatusResponse>
        {
            Success = true,
            Message = "Lấy trạng thái KYC thành công.",
            Data = result
        });
    }

    [HttpGet("my-history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<KycHistoryItemResponse>>>> GetMyHistory(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _kycService.GetMyHistoryAsync(userId, cancellationToken);

        return Ok(new ApiResponse<IReadOnlyList<KycHistoryItemResponse>>
        {
            Success = true,
            Message = "Lấy lịch sử KYC thành công.",
            Data = result.Items
        });
    }

    [HttpPost("vnpt-document-test")]
    public async Task<ActionResult<ApiResponse<VnptDocumentTestResponse>>> TestVnptDocumentOnly(
        [FromForm] VnptDocumentTestRequest request,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (request.FrontImage is null || request.FrontImage.Length == 0)
        {
            return BadRequest(new ApiResponse<VnptDocumentTestResponse>
            {
                Success = false,
                Message = "FrontImage is required."
            });
        }

        if (request.BackImage is null || request.BackImage.Length == 0)
        {
            return BadRequest(new ApiResponse<VnptDocumentTestResponse>
            {
                Success = false,
                Message = "BackImage is required."
            });
        }

        var userId = GetCurrentUserId();
        await using var frontStream = request.FrontImage.OpenReadStream();
        await using var backStream = request.BackImage.OpenReadStream();

        var vnpt = await _vnptEkycClient.VerifyAsync(new VnptEkycVerifyInput
        {
            UserId = userId,
            DocumentType = request.DocumentType,
            FrontImage = new VnptEkycFileInput
            {
                Content = frontStream,
                FileName = request.FrontImage.FileName,
                ContentType = request.FrontImage.ContentType
            },
            BackImage = new VnptEkycFileInput
            {
                Content = backStream,
                FileName = request.BackImage.FileName,
                ContentType = request.BackImage.ContentType
            },
            SelfieCaptureMethod = "Upload",
            DocumentOnly = true
        }, cancellationToken);

        return Ok(new ApiResponse<VnptDocumentTestResponse>
        {
            Success = !vnpt.IsProviderFailure,
            Message = vnpt.IsProviderFailure
                ? "VNPT document-only test failed."
                : "VNPT document-only test completed.",
            Data = new VnptDocumentTestResponse
            {
                SessionId = vnpt.SessionId,
                EkycResult = vnpt.EkycResult,
                OcrFullName = vnpt.OcrFullName,
                OcrCitizenId = vnpt.OcrCitizenId,
                OcrDateOfBirth = vnpt.OcrDateOfBirth,
                OcrGender = vnpt.OcrGender,
                OcrAddress = vnpt.OcrAddress,
                OcrConfidence = vnpt.OcrConfidence,
                DocumentCheckResult = vnpt.DocumentCheckResult,
                FaceMatchResult = vnpt.FaceMatchResult,
                LivenessResult = vnpt.LivenessResult,
                RiskLevel = vnpt.RiskLevel?.ToString(),
                ErrorCode = vnpt.ErrorCode,
                ErrorMessage = vnpt.ErrorMessage,
                IsProviderFailure = vnpt.IsProviderFailure,
                IsDocumentUnreadable = vnpt.IsDocumentUnreadable
            }
        });
    }

    private Guid GetCurrentUserId()
    {
        return _currentUserService.GetRequiredUserIdForAction();
    }
}
