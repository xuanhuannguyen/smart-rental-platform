using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Contracts.Kyc;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Application.Kyc;

public class KycService : IKycService
{
    private readonly IAppDbContext _context;
    private readonly IMediaObjectKeyFactory _mediaObjectKeyFactory;
    private readonly IMediaStorageService _mediaStorageService;
    private readonly IMediaAssetService _mediaAssetService;
    private readonly IVnptEkycClient _vnptEkycClient;
    private readonly IHashService _hashService;
    private readonly ISensitiveDataProtector _sensitiveDataProtector;

    public KycService(
        IAppDbContext context,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        IMediaStorageService mediaStorageService,
        IMediaAssetService mediaAssetService,
        IVnptEkycClient vnptEkycClient,
        IHashService hashService,
        ISensitiveDataProtector sensitiveDataProtector)
    {
        _context = context;
        _mediaObjectKeyFactory = mediaObjectKeyFactory;
        _mediaStorageService = mediaStorageService;
        _mediaAssetService = mediaAssetService;
        _vnptEkycClient = vnptEkycClient;
        _hashService = hashService;
        _sensitiveDataProtector = sensitiveDataProtector;
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

        KycStoredUpload? frontUpload = null;
        KycStoredUpload? backUpload = null;
        KycStoredUpload? selfieUpload = null;
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            frontUpload = await UploadImageAsync(userId, request.FrontImage, cancellationToken);
            backUpload = await UploadImageAsync(userId, request.BackImage, cancellationToken);
            selfieUpload = await UploadImageAsync(userId, request.SelfieImage, cancellationToken);

            var ekyc = await _vnptEkycClient.VerifyAsync(new VnptEkycVerifyInput
            {
                UserId = userId,
                DocumentType = request.DocumentType,
                FrontImageObjectKey = frontUpload.StoredObject.ObjectKey,
                BackImageObjectKey = backUpload.StoredObject.ObjectKey,
                SelfieImageObjectKey = selfieUpload.StoredObject.ObjectKey,
                SelfieCaptureMethod = request.SelfieCaptureMethod
            }, cancellationToken);

            var ekycResult = ParseEnum<EkycResult>(ekyc.EkycResult, EkycResult.ProviderError);
            var documentCheck = ParseNullableEnum<DocumentCheckResult>(ekyc.DocumentCheckResult);
            var faceMatch = ParseNullableEnum<FaceMatchResult>(ekyc.FaceMatchResult);
            var liveness = ParseNullableEnum<LivenessResult>(ekyc.LivenessResult);
            var riskLevel = CalculateRiskLevel(ekycResult, documentCheck, faceMatch, liveness, ekyc);

            string? ocrCitizenIdMasked = null;
            string? citizenIdHash = null;
            string? documentNumberEncrypted = null;
            if (!string.IsNullOrWhiteSpace(ekyc.OcrCitizenId))
            {
                ocrCitizenIdMasked = MaskCitizenId(ekyc.OcrCitizenId);
                citizenIdHash = _hashService.HashSha256Hex(ekyc.OcrCitizenId);
                documentNumberEncrypted = _sensitiveDataProtector.Encrypt(ekyc.OcrCitizenId);

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
                FrontImageObjectKey = frontUpload.StoredObject.ObjectKey,
                BackImageObjectKey = backUpload.StoredObject.ObjectKey,
                SelfieImageObjectKey = selfieUpload.StoredObject.ObjectKey,
                SelfieCaptureMethod = selfieMethod,
                OcrFullName = ekyc.OcrFullName,
                OcrCitizenIdMasked = ocrCitizenIdMasked,
                CitizenIdHash = citizenIdHash ?? string.Empty,
                DocumentNumberEncrypted = documentNumberEncrypted,
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

            kyc.FrontMediaAssetId = (await CreateMediaAssetAsync(
                userId,
                kyc.Id,
                frontUpload,
                cancellationToken)).Id;
            kyc.BackMediaAssetId = (await CreateMediaAssetAsync(
                userId,
                kyc.Id,
                backUpload,
                cancellationToken)).Id;
            kyc.SelfieMediaAssetId = (await CreateMediaAssetAsync(
                userId,
                kyc.Id,
                selfieUpload,
                cancellationToken)).Id;

            if (finalStatus == KycVerificationStatus.PendingAdminReview)
            {
                user.OnboardingStatus = OnboardingStatus.KycPending;
                user.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

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
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await CleanupUploadAsync(frontUpload, cancellationToken);
            await CleanupUploadAsync(backUpload, cancellationToken);
            await CleanupUploadAsync(selfieUpload, cancellationToken);
            throw;
        }
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

    private async Task<KycStoredUpload> UploadImageAsync(
        Guid userId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var mediaObjectKey = _mediaObjectKeyFactory.Create(
            MediaScope.KycDocument,
            MediaVisibility.Private,
            file.FileName);

        await using var stream = file.OpenReadStream();
        var storedObject = await _mediaStorageService.UploadAsync(
            new MediaUploadRequest
            {
                Content = stream,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                ObjectKey = mediaObjectKey.ObjectKey,
                Visibility = MediaVisibility.Private
            },
            cancellationToken);

        return new KycStoredUpload(
            userId,
            file.FileName,
            file.ContentType,
            file.Length,
            storedObject);
    }

    private async Task<MediaAsset> CreateMediaAssetAsync(
        Guid userId,
        Guid kycId,
        KycStoredUpload upload,
        CancellationToken cancellationToken)
    {
        return await _mediaAssetService.CreateAsync(
            new CreateMediaAssetRequest
            {
                OwnerUserId = userId,
                BucketName = upload.StoredObject.BucketName,
                ObjectKey = upload.StoredObject.ObjectKey,
                OriginalFileName = upload.OriginalFileName,
                StoredFileName = upload.StoredObject.StoredFileName,
                ContentType = upload.ContentType,
                FileSize = upload.FileSize,
                Scope = MediaScope.KycDocument,
                Visibility = MediaVisibility.Private,
                Status = MediaStatus.Linked,
                LinkedEntityType = nameof(KycVerification),
                LinkedEntityId = kycId
            },
            cancellationToken);
    }

    private async Task CleanupUploadAsync(
        KycStoredUpload? upload,
        CancellationToken cancellationToken)
    {
        if (upload is null)
        {
            return;
        }

        await _mediaStorageService.DeleteAsync(upload.StoredObject.ObjectKey, cancellationToken);
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

    private sealed record KycStoredUpload(
        Guid UserId,
        string OriginalFileName,
        string ContentType,
        long FileSize,
        MediaStoredObjectResult StoredObject);
}
