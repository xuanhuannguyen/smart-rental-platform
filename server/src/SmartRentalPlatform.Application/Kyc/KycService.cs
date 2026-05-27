using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Kyc;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Kyc;

public class KycService : IKycService
{
    private readonly IAppDbContext _context;
    private readonly IPrivateStorageService _storage;
    private readonly IVnptEkycClient _vnptEkycClient;
    private readonly IHashService _hashService;

    public KycService(
        IAppDbContext context,
        IPrivateStorageService storage,
        IVnptEkycClient vnptEkycClient,
        IHashService hashService)
    {
        _context = context;
        _storage = storage;
        _vnptEkycClient = vnptEkycClient;
        _hashService = hashService;
    }

    public async Task<KycSubmissionResponse> SubmitAsync(
        Guid userId,
        SubmitKycRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateFiles(request);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
            throw new KycBusinessException(ErrorCodes.Unauthorized, "User not found.", 401);

        if (user.Status != UserStatus.Active)
            throw new KycBusinessException(
                ErrorCodes.AccountNotActive,
                "Account is not active.",
                403);

        var blocking = await _context.KycVerifications
            .AsNoTracking()
            .Where(k => k.UserId == userId &&
                        (k.Status == KycVerificationStatus.PendingAdminReview ||
                         k.Status == KycVerificationStatus.Approved))
            .Select(k => k.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (blocking == KycVerificationStatus.Approved)
            throw new KycBusinessException(
                ErrorCodes.KycAlreadyApproved,
                "KYC is already approved.",
                400);

        if (blocking == KycVerificationStatus.PendingAdminReview)
            throw new KycBusinessException(
                ErrorCodes.KycPendingAdminReview,
                "A KYC submission is already pending admin review.",
                400);

        if (!Enum.TryParse<KycDocumentType>(request.DocumentType, ignoreCase: false, out var documentType))
            throw new KycBusinessException(ErrorCodes.EkycDocumentFailed, "Invalid document type.", 400);

        if (!Enum.TryParse<SelfieCaptureMethod>(request.SelfieCaptureMethod, ignoreCase: false, out var selfieMethod))
            throw new KycBusinessException(ErrorCodes.SelfieRequired, "Invalid selfie capture method.", 400);

        var frontKey = await UploadImageAsync(userId, "front", request.FrontImage, cancellationToken);
        var backKey = await UploadImageAsync(userId, "back", request.BackImage, cancellationToken);
        var selfieKey = await UploadImageAsync(userId, "selfie", request.SelfieImage, cancellationToken);

        var ekyc = await _vnptEkycClient.VerifyAsync(new VnptEkycVerifyInput
        {
            UserId = userId,
            DocumentType = request.DocumentType,
            FrontImageObjectKey = frontKey,
            BackImageObjectKey = backKey,
            SelfieImageObjectKey = selfieKey,
            SelfieCaptureMethod = request.SelfieCaptureMethod
        }, cancellationToken);

        var ekycResult = ParseEnum<EkycResult>(ekyc.EkycResult, EkycResult.ProviderError);
        var documentCheck = ParseNullableEnum<DocumentCheckResult>(ekyc.DocumentCheckResult);
        var faceMatch = ParseNullableEnum<FaceMatchResult>(ekyc.FaceMatchResult);
        var liveness = ParseNullableEnum<LivenessResult>(ekyc.LivenessResult);
        var riskLevel = CalculateRiskLevel(ekycResult, documentCheck, faceMatch, liveness, ekyc);

        string? ocrCitizenIdMasked = null;
        string? citizenIdHash = null;
        if (!string.IsNullOrWhiteSpace(ekyc.OcrCitizenId))
        {
            ocrCitizenIdMasked = MaskCitizenId(ekyc.OcrCitizenId);
            citizenIdHash = _hashService.HashSha256Hex(ekyc.OcrCitizenId);

            var duplicate = await _context.KycVerifications
                .AsNoTracking()
                .AnyAsync(k =>
                    k.CitizenIdHash == citizenIdHash &&
                    k.UserId != userId &&
                    k.Status == KycVerificationStatus.Approved,
                    cancellationToken);

            if (duplicate)
                throw new KycBusinessException(
                    ErrorCodes.EkycDocumentFailed,
                    "Citizen ID is already associated with another approved account.",
                    400);
        }

        var finalStatus = ekyc.IsProviderFailure || ekycResult == EkycResult.ProviderError || ekycResult == EkycResult.Failed
            ? KycVerificationStatus.EkycFailed
            : KycVerificationStatus.PendingAdminReview;

        var now = DateTimeOffset.UtcNow;
        var kyc = new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentType = documentType,
            EkycProvider = EkycProvider.VNPT,
            EkycSessionId = ekyc.SessionId,
            FrontImageObjectKey = frontKey,
            BackImageObjectKey = backKey,
            SelfieImageObjectKey = selfieKey,
            SelfieCaptureMethod = selfieMethod,
            OcrFullName = ekyc.OcrFullName,
            OcrCitizenIdMasked = ocrCitizenIdMasked,
            CitizenIdHash = citizenIdHash ?? string.Empty,
            OcrDateOfBirth = ekyc.OcrDateOfBirth.HasValue
                ? DateOnly.FromDateTime(ekyc.OcrDateOfBirth.Value)
                : null,
            OcrGender = ekyc.OcrGender,
            OcrAddress = ekyc.OcrAddress,
            OcrConfidence = ekyc.OcrConfidence,
            DocumentCheckResult = documentCheck,
            FaceMatchScore = ekyc.FaceMatchScore,
            FaceMatchResult = faceMatch,
            LivenessResult = liveness,
            EkycResult = ekycResult,
            EkycErrorCode = ekyc.ErrorCode,
            EkycErrorMessage = ekyc.ErrorMessage,
            RiskLevel = riskLevel,
            Status = finalStatus,
            SubmittedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.KycVerifications.Add(kyc);

        if (finalStatus == KycVerificationStatus.PendingAdminReview)
        {
            user.OnboardingStatus = OnboardingStatus.KycPending;
            user.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new KycSubmissionResponse
        {
            KycId = kyc.Id,
            Status = finalStatus.ToString(),
            EkycResult = ekycResult.ToString(),
            RiskLevel = riskLevel.ToString(),
            DocumentType = documentType.ToString(),
            OcrFullName = kyc.OcrFullName,
            OcrCitizenIdMasked = ocrCitizenIdMasked,
            OcrDateOfBirth = kyc.OcrDateOfBirth?.ToDateTime(TimeOnly.MinValue),
            OcrGender = kyc.OcrGender,
            OcrAddress = kyc.OcrAddress,
            OcrConfidence = kyc.OcrConfidence,
            DocumentCheckResult = documentCheck?.ToString(),
            FaceMatchScore = kyc.FaceMatchScore,
            FaceMatchResult = faceMatch?.ToString(),
            LivenessResult = liveness?.ToString(),
            EkycErrorCode = kyc.EkycErrorCode,
            EkycErrorMessage = kyc.EkycErrorMessage,
            SubmittedAt = now,
            Message = BuildSubmissionMessage(finalStatus, ekyc)
        };
    }

    public async Task<KycStatusResponse> GetMyStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var latest = await _context.KycVerifications
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest == null)
            return new KycStatusResponse { HasSubmission = false };

        return MapStatus(latest);
    }

    public async Task<KycHistoryResponse> GetMyHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var items = await _context.KycVerifications
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.SubmittedAt)
            .Select(k => new KycHistoryItemResponse
            {
                KycId = k.Id,
                Status = k.Status.ToString(),
                EkycResult = k.EkycResult.ToString(),
                RiskLevel = k.RiskLevel.ToString(),
                DocumentType = k.DocumentType.ToString(),
                OcrFullName = k.OcrFullName,
                OcrCitizenIdMasked = k.OcrCitizenIdMasked,
                OcrAddress = k.OcrAddress,
                FaceMatchScore = k.FaceMatchScore,
                LivenessResult = k.LivenessResult != null ? k.LivenessResult.ToString() : null,
                SubmittedAt = k.SubmittedAt,
                ReviewedAt = k.ReviewedAt,
                RejectedReason = k.RejectedReason
            })
            .ToListAsync(cancellationToken);

        return new KycHistoryResponse
        {
            Items = items,
            TotalItems = items.Count
        };
    }

    private static void ValidateFiles(SubmitKycRequest request)
    {
        if (request.FrontImage == null || request.FrontImage.Length == 0)
            throw new KycBusinessException(ErrorCodes.FrontImageRequired, "Front image is required.", 400);

        if (request.BackImage == null || request.BackImage.Length == 0)
            throw new KycBusinessException(ErrorCodes.BackImageRequired, "Back image is required.", 400);

        if (request.SelfieImage == null || request.SelfieImage.Length == 0)
            throw new KycBusinessException(ErrorCodes.SelfieRequired, "Selfie image is required.", 400);
    }

    private async Task<string> UploadImageAsync(
        Guid userId,
        string label,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".jpg";

        var objectKey = $"kyc/{userId:N}/{label}-{Guid.NewGuid():N}{extension}";
        await using var stream = file.OpenReadStream();
        return await _storage.UploadAsync(stream, file.ContentType, objectKey, cancellationToken);
    }

    private static KycStatusResponse MapStatus(KycVerification k) =>
        new()
        {
            HasSubmission = true,
            KycId = k.Id,
            Status = k.Status.ToString(),
            EkycResult = k.EkycResult.ToString(),
            RiskLevel = k.RiskLevel.ToString(),
            DocumentType = k.DocumentType.ToString(),
            OcrFullName = k.OcrFullName,
            OcrCitizenIdMasked = k.OcrCitizenIdMasked,
            OcrDateOfBirth = k.OcrDateOfBirth.HasValue
                ? k.OcrDateOfBirth.Value.ToDateTime(TimeOnly.MinValue)
                : null,
            OcrGender = k.OcrGender,
            OcrAddress = k.OcrAddress,
            FaceMatchScore = k.FaceMatchScore,
            LivenessResult = k.LivenessResult?.ToString(),
            SubmittedAt = k.SubmittedAt,
            ReviewedAt = k.ReviewedAt,
            RejectedReason = k.RejectedReason
        };

    private static string MaskCitizenId(string citizenId)
    {
        var trimmed = citizenId.Trim();
        if (trimmed.Length < 8)
            return new string('x', trimmed.Length);

        var first = trimmed[..4];
        var last = trimmed[^4..];
        var middleLength = trimmed.Length - 8;
        var middle = middleLength >= 4
            ? new string('x', middleLength)
            : "xxxx";

        return first + middle + last;
    }

    private static string BuildSubmissionMessage(
        KycVerificationStatus finalStatus,
        VnptEkycClientResult ekyc)
    {
        if (finalStatus == KycVerificationStatus.PendingAdminReview)
            return "Submission received. Your profile is pending admin review.";

        if (ekyc.ErrorCode == ErrorCodes.EkycDocumentFailed)
            return "Document image quality is not acceptable. Please retake clear, uncropped images and try again.";

        return "eKYC provider could not complete verification. Please try again.";
    }

    private static KycRiskLevel CalculateRiskLevel(
        EkycResult ekycResult,
        DocumentCheckResult? documentCheck,
        FaceMatchResult? faceMatch,
        LivenessResult? liveness,
        VnptEkycClientResult ekyc)
    {
        if (ekycResult is EkycResult.Failed or EkycResult.ProviderError)
            return KycRiskLevel.High;

        if (documentCheck == DocumentCheckResult.Tampered)
            return KycRiskLevel.High;

        if (liveness == LivenessResult.Failed)
            return KycRiskLevel.High;

        if (faceMatch is FaceMatchResult.NotMatched or FaceMatchResult.LowConfidence)
            return KycRiskLevel.High;

        if (ekycResult == EkycResult.NeedReview)
            return KycRiskLevel.Medium;

        if (documentCheck != null && documentCheck != DocumentCheckResult.Valid)
            return KycRiskLevel.Medium;

        if ((ekyc.OcrConfidence ?? 1) < 0.85m || (ekyc.FaceMatchScore ?? 1) < 0.85m)
            return KycRiskLevel.Medium;

        return KycRiskLevel.Low;
    }

    private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: false, out var parsed) ? parsed : fallback;

    private static T? ParseNullableEnum<T>(string? value) where T : struct, Enum =>
        string.IsNullOrWhiteSpace(value) || !Enum.TryParse<T>(value, ignoreCase: false, out var parsed)
            ? null
            : parsed;
}
