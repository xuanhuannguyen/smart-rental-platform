using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Contracts.Admin;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Application.AdminApproval;

public class AdminUserService : IAdminUserService
{
    private readonly IAppDbContext _context;

    public AdminUserService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<AdminUserListResponse> GetUsersAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            // Chỉ hiển thị user thường, loại trừ Admin
            .Where(x => !x.UserRoles.Any(ur => ur.Role.Name == RoleName.Admin));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminUserListItemResponse
            {
                Id = x.Id,
                Email = x.Email,
                DisplayName = x.DisplayName,
                PhoneNumber = x.PhoneNumber,
                Roles = x.UserRoles.Select(ur => ur.Role.Name.ToString()).ToList(),
                Status = x.Status.ToString(),
                OnboardingStatus = x.OnboardingStatus.ToString(),
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminUserListResponse
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }


    public async Task<AdminUserDetailResponse?> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .Include(x => x.UserProfile)
            .Include(x => x.KycVerifications)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var response = new AdminUserDetailResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            PhoneNumber = user.PhoneNumber,
            Roles = user.UserRoles.Select(ur => ur.Role.Name.ToString()).ToList(),
            Status = user.Status.ToString(),
            OnboardingStatus = user.OnboardingStatus.ToString(),
            CreatedAt = user.CreatedAt,
            
            // Profile fields
            FullName = user.UserProfile?.FullName,
            DateOfBirth = user.UserProfile?.DateOfBirth,
            Gender = user.UserProfile?.Gender,
            AddressLine = user.UserProfile?.AddressLine,
            VerifiedCitizenIdMasked = user.UserProfile?.VerifiedCitizenIdMasked,
            EmergencyContactName = user.UserProfile?.EmergencyContactName,
            EmergencyContactPhone = user.UserProfile?.EmergencyContactPhone
        };

        // Nếu người dùng đã hoàn thành Onboarding (được duyệt eKYC thành công)
        if (user.OnboardingStatus == OnboardingStatus.Completed)
        {
            var approvedKyc = user.KycVerifications
                .Where(x => x.Status == KycVerificationStatus.Approved)
                .OrderByDescending(x => x.SubmittedAt)
                .FirstOrDefault();

            if (approvedKyc is not null)
            {
                response.KycInfo = new AdminKycInfo
                {
                    KycId = approvedKyc.Id,
                    FrontMediaAssetId = approvedKyc.FrontMediaAssetId,
                    BackMediaAssetId = approvedKyc.BackMediaAssetId,
                    SelfieMediaAssetId = approvedKyc.SelfieMediaAssetId,
                    FrontImageUrl = BuildPrivateMediaUrl(approvedKyc.FrontImageObjectKey),
                    BackImageUrl = BuildPrivateMediaUrl(approvedKyc.BackImageObjectKey),
                    SelfieImageUrl = BuildPrivateMediaUrl(approvedKyc.SelfieImageObjectKey),
                    OcrFullName = approvedKyc.OcrFullName,
                    OcrCitizenIdMasked = approvedKyc.OcrCitizenIdMasked,
                    OcrDateOfBirth = approvedKyc.OcrDateOfBirth,
                    OcrGender = approvedKyc.OcrGender,
                    OcrAddress = approvedKyc.OcrAddress,
                    FaceMatchScore = approvedKyc.FaceMatchScore,
                    EkycResult = approvedKyc.EkycResult.ToString(),
                    RiskLevel = approvedKyc.RiskLevel.ToString(),
                    SubmittedAt = approvedKyc.SubmittedAt,
                    ApprovedAt = approvedKyc.ReviewedAt
                };
            }
        }

        return response;
    }

    private static string BuildPrivateMediaUrl(string objectKey)
    {
        return $"/api/admin/media/private?objectKey={Uri.EscapeDataString(objectKey)}";
    }
}
