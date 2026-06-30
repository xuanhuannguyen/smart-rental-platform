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
    private readonly IPrivateStorageService _storage;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWebHostEnvironment _environment;

    public KycController(
        IKycService kycService,
        IVnptEkycClient vnptEkycClient,
        IPrivateStorageService storage,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment)
    {
        _kycService = kycService;
        _vnptEkycClient = vnptEkycClient;
        _storage = storage;
        _currentUserService = currentUserService;
        _environment = environment;
    }

    [HttpPost("submissions")]
    public async Task<ActionResult<ApiResponse<KycSubmissionResponse>>> Submit(
        [FromForm] SubmitKycRequest request,
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
        var frontKey = await UploadTestImageAsync(userId, "front", request.FrontImage, cancellationToken);
        var backKey = await UploadTestImageAsync(userId, "back", request.BackImage, cancellationToken);

        var vnpt = await _vnptEkycClient.VerifyAsync(new VnptEkycVerifyInput
        {
            UserId = userId,
            DocumentType = request.DocumentType,
            FrontImageObjectKey = frontKey,
            BackImageObjectKey = backKey,
            SelfieImageObjectKey = string.Empty,
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
                IsDocumentUnreadable = vnpt.IsDocumentUnreadable,
                FrontImageObjectKey = frontKey,
                BackImageObjectKey = backKey
            }
        });
    }

    private Guid GetCurrentUserId()
    {
        return _currentUserService.GetRequiredUserIdForAction();
    }

    private async Task<string> UploadTestImageAsync(
        Guid userId,
        string label,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        var objectKey = $"kyc-tests/{userId:N}/{label}-{Guid.NewGuid():N}{extension}";
        await using var stream = file.OpenReadStream();
        return await _storage.UploadAsync(stream, file.ContentType, objectKey, cancellationToken);
    }
}
