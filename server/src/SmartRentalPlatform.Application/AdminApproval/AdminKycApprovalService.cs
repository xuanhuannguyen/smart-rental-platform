using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Contracts.Admin;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.AdminApproval;

public class AdminKycApprovalService(IAppDbContext context) : IAdminKycApprovalService
{
    private readonly IAppDbContext _context = context;

    public async Task<AdminKycListResponse> GetPendingAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.KycVerifications
            .AsNoTracking()
            .Where(x => x.Status == KycVerificationStatus.PendingAdminReview);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.SubmittedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminKycListItemResponse
            {
                Id = x.Id,
                UserId = x.UserId,
                UserEmail = x.User.Email,
                UserDisplayName = x.User.DisplayName,
                OcrFullName = x.OcrFullName,
                OcrCitizenIdMasked = x.OcrCitizenIdMasked,
                Status = x.Status.ToString(),
                RiskLevel = x.RiskLevel.ToString(),
                SubmittedAt = x.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminKycListResponse
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AdminKycDetailResponse?> GetDetailAsync(
        Guid kycId,
        CancellationToken cancellationToken = default)
    {
        var kyc = await _context.KycVerifications
            .AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == kycId, cancellationToken);

        if (kyc is null)
        {
            return null;
        }

        return new AdminKycDetailResponse
        {
            Id = kyc.Id,
            UserId = kyc.UserId,
            UserEmail = kyc.User.Email,
            UserDisplayName = kyc.User.DisplayName,
            OcrFullName = kyc.OcrFullName,
            OcrCitizenIdMasked = kyc.OcrCitizenIdMasked,
            Status = kyc.Status.ToString(),
            RiskLevel = kyc.RiskLevel.ToString(),
            SubmittedAt = kyc.SubmittedAt,
            DocumentType = kyc.DocumentType.ToString(),
            EkycProvider = kyc.EkycProvider.ToString(),
            EkycSessionId = kyc.EkycSessionId,
            OcrDateOfBirth = kyc.OcrDateOfBirth?.ToDateTime(TimeOnly.MinValue),
            OcrGender = kyc.OcrGender,
            OcrAddress = kyc.OcrAddress,
            OcrConfidence = kyc.OcrConfidence,
            DocumentCheckResult = kyc.DocumentCheckResult?.ToString(),
            FaceMatchScore = kyc.FaceMatchScore,
            FaceMatchResult = kyc.FaceMatchResult?.ToString(),
            LivenessResult = kyc.LivenessResult?.ToString(),
            EkycResult = kyc.EkycResult.ToString(),
            EkycErrorCode = kyc.EkycErrorCode,
            EkycErrorMessage = kyc.EkycErrorMessage,
            FrontImageUrl = BuildPrivateMediaUrl(kyc.FrontImageObjectKey),
            BackImageUrl = BuildPrivateMediaUrl(kyc.BackImageObjectKey),
            SelfieImageUrl = BuildPrivateMediaUrl(kyc.SelfieImageObjectKey),
            RejectedReason = kyc.RejectedReason,
            ReviewedByAdminId = kyc.ReviewedByAdminId,
            ReviewedAt = kyc.ReviewedAt
        };
    }

    public async Task<bool> ApproveAsync(
        Guid kycId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            var kyc = await _context.KycVerifications
                .FirstOrDefaultAsync(x => x.Id == kycId, cancellationToken);

            if (kyc is null || kyc.Status != KycVerificationStatus.PendingAdminReview)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == kyc.UserId, cancellationToken);

            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            kyc.Status = KycVerificationStatus.Approved;
            kyc.RejectedReason = null;
            kyc.ReviewedByAdminId = adminId;
            kyc.ReviewedAt = now;
            kyc.UpdatedAt = now;

            await SyncProfileAsync(kyc, now, cancellationToken);

            user.OnboardingStatus = OnboardingStatus.Completed;
            user.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> RejectAsync(
        Guid kycId,
        Guid adminId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            var kyc = await _context.KycVerifications
                .FirstOrDefaultAsync(x => x.Id == kycId, cancellationToken);

            if (kyc is null || kyc.Status != KycVerificationStatus.PendingAdminReview)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == kyc.UserId, cancellationToken);

            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            kyc.Status = KycVerificationStatus.Rejected;
            kyc.RejectedReason = reason.Trim();
            kyc.ReviewedByAdminId = adminId;
            kyc.ReviewedAt = now;
            kyc.UpdatedAt = now;

            user.OnboardingStatus = OnboardingStatus.NeedProfileUpdate;
            user.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task SyncProfileAsync(
        KycVerification kyc,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(x => x.UserId == kyc.UserId, cancellationToken);

        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = kyc.UserId,
                CreatedAt = now
            };

            _context.UserProfiles.Add(profile);
        }

        profile.FullName = kyc.OcrFullName;
        profile.DateOfBirth = kyc.OcrDateOfBirth;
        profile.Gender = kyc.OcrGender;
        profile.AddressLine = kyc.OcrAddress;
        profile.VerifiedCitizenIdMasked = kyc.OcrCitizenIdMasked;
        profile.UpdatedAt = now;
    }

    private static string BuildPrivateMediaUrl(string objectKey)
    {
        return $"/api/admin/media/private?objectKey={Uri.EscapeDataString(objectKey)}";
    }

    public async Task<List<AdminKycDetailResponse>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var items = await _context.KycVerifications
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.SubmittedAt)
            .Select(kyc => new AdminKycDetailResponse
            {
                Id = kyc.Id,
                UserId = kyc.UserId,
                UserEmail = kyc.User.Email,
                UserDisplayName = kyc.User.DisplayName,
                OcrFullName = kyc.OcrFullName,
                OcrCitizenIdMasked = kyc.OcrCitizenIdMasked,
                Status = kyc.Status.ToString(),
                RiskLevel = kyc.RiskLevel.ToString(),
                SubmittedAt = kyc.SubmittedAt,
                DocumentType = kyc.DocumentType.ToString(),
                EkycProvider = kyc.EkycProvider.ToString(),
                EkycSessionId = kyc.EkycSessionId,
                OcrDateOfBirth = kyc.OcrDateOfBirth != null ? kyc.OcrDateOfBirth.Value.ToDateTime(TimeOnly.MinValue) : null,
                OcrGender = kyc.OcrGender,
                OcrAddress = kyc.OcrAddress,
                OcrConfidence = kyc.OcrConfidence,
                DocumentCheckResult = kyc.DocumentCheckResult != null ? kyc.DocumentCheckResult.ToString() : null,
                FaceMatchScore = kyc.FaceMatchScore,
                FaceMatchResult = kyc.FaceMatchResult != null ? kyc.FaceMatchResult.ToString() : null,
                LivenessResult = kyc.LivenessResult != null ? kyc.LivenessResult.ToString() : null,
                EkycResult = kyc.EkycResult.ToString(),
                EkycErrorCode = kyc.EkycErrorCode,
                EkycErrorMessage = kyc.EkycErrorMessage,
                FrontImageUrl = BuildPrivateMediaUrl(kyc.FrontImageObjectKey),
                BackImageUrl = BuildPrivateMediaUrl(kyc.BackImageObjectKey),
                SelfieImageUrl = BuildPrivateMediaUrl(kyc.SelfieImageObjectKey),
                RejectedReason = kyc.RejectedReason,
                ReviewedByAdminId = kyc.ReviewedByAdminId,
                ReviewedAt = kyc.ReviewedAt
            })
            .ToListAsync(cancellationToken);

        return items;
    }
}
