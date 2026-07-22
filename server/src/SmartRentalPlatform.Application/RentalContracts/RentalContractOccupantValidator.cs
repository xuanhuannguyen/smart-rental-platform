using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Kyc;

namespace SmartRentalPlatform.Application.RentalContracts;

public sealed class RentalContractOccupantValidator
{
    private readonly IAppDbContext context;

    public RentalContractOccupantValidator(IAppDbContext context)
    {
        this.context = context;
    }

    public static void ValidateOccupantsRequest(
        string tenantEmail,
        SubmitContractOccupantsRequest request,
        int maxOccupants,
        DateOnly startDate,
        DateOnly endDate)
    {
        if (request.Occupants.Count == 0)
        {
            throw new BadRequestException("RENTAL_CONTRACT_OCCUPANTS_REQUIRED", "Danh sách người ở không được để trống.");
        }

        if (request.Occupants.Count > maxOccupants)
        {
            throw new BadRequestException("RENTAL_REQUEST_OCCUPANT_LIMIT_EXCEEDED", "Số người ở vượt quá sức chứa tối đa đã chốt trong hợp đồng.", new
            {
                request.Occupants.Count,
                maxOccupants
            });
        }

        if (!request.Occupants.Any(x => x.Email?.Trim().Equals(tenantEmail.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
        {
            throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Danh sách người ở phải bao gồm người thuê chính.", new { tenantEmail });
        }

        foreach (var occupant in request.Occupants)
        {
            if (occupant.MoveInDate < startDate || occupant.MoveInDate > endDate)
            {
                throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT_DATE", "Ngày chuyển vào phải nằm trong khoảng thời gian có hiệu lực của hợp đồng.");
            }
            ValidateOccupantRequest(occupant);
        }
    }

    public async Task<Dictionary<string, VerifiedOccupantAccount>> ValidateOccupantAccountsAsync(
        SubmitContractOccupantsRequest request,
        CancellationToken cancellationToken)
    {
        var emails = request.Occupants
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .Select(x => x.Email!.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        if (emails.Count == 0)
        {
            return [];
        }

        var users = await context.Users
            .AsNoTracking()
            .Include(x => x.UserProfile)
            .Where(x => emails.Contains(x.Email.ToLower()) && x.DeletedAt == null)
            .ToListAsync(cancellationToken);
        var missingEmails = emails.Except(users.Select(x => x.Email.ToLowerInvariant()).ToList()).ToList();
        if (missingEmails.Count > 0)
        {
            throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở có tài khoản không tồn tại.", new
            {
                emails = missingEmails
            });
        }

        var userIdsForKyc = users.Select(x => x.Id).ToList();
        var latestApprovedKycByUserId = (await context.KycVerifications
                .AsNoTracking()
                .Where(x => userIdsForKyc.Contains(x.UserId) && x.Status == KycVerificationStatus.Approved)
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(k => k.ReviewedAt ?? k.UpdatedAt).First());
        var notApprovedKycUserIds = userIdsForKyc.Except(latestApprovedKycByUserId.Keys).ToList();
        if (notApprovedKycUserIds.Count > 0)
        {
            var notApprovedEmails = users
                .Where(u => notApprovedKycUserIds.Contains(u.Id))
                .Select(u => u.Email)
                .ToList();
            throw new BadRequestException("KYC_REQUIRED", "Người ở có tài khoản phải hoàn tất KYC trước khi được thêm vào hợp đồng.", new
            {
                emails = notApprovedEmails
            });
        }

        var result = new Dictionary<string, VerifiedOccupantAccount>();
        foreach (var user in users)
        {
            var approvedKyc = latestApprovedKycByUserId[user.Id];
            var fullName = NormalizeOptionalText(approvedKyc.OcrFullName) ??
                NormalizeOptionalText(user.UserProfile?.FullName) ??
                NormalizeOptionalText(user.DisplayName);
            var dateOfBirth = approvedKyc.OcrDateOfBirth ?? user.UserProfile?.DateOfBirth;
            if (string.IsNullOrWhiteSpace(fullName) || !dateOfBirth.HasValue)
            {
                throw new BadRequestException("KYC_REQUIRED", "Thông tin KYC đã duyệt của người ở chưa đủ họ tên hoặc ngày sinh.", new
                {
                    email = user.Email
                });
            }

            result[user.Email.ToLowerInvariant()] = new VerifiedOccupantAccount(
                user.Id,
                fullName,
                NormalizeOptionalText(user.PhoneNumber),
                dateOfBirth.Value);
        }

        return result;
    }

    private static void ValidateOccupantRequest(ContractOccupantRequest occupant)
    {
        if (string.IsNullOrWhiteSpace(occupant.RelationshipToMainTenant))
        {
            throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở phải có quan hệ với người thuê chính.");
        }

        if (occupant.MoveInDate == default)
        {
            throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở phải có ngày chuyển vào.");
        }

        if (occupant.MoveOutDate.HasValue && occupant.MoveOutDate.Value <= occupant.MoveInDate)
        {
            throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Ngày rời đi phải lớn hơn ngày chuyển vào.");
        }

        if (!string.IsNullOrWhiteSpace(occupant.Email))
        {
            if (occupant.Document is not null)
            {
                throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở đã có tài khoản và KYC không được gửi giấy tờ trong hợp đồng.", new { occupant.Email });
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(occupant.FullName))
        {
            throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở chưa có tài khoản phải có họ tên.");
        }

        if (string.IsNullOrWhiteSpace(occupant.PhoneNumber))
        {
            throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở chưa có tài khoản phải có số điện thoại.");
        }

        if (!occupant.DateOfBirth.HasValue || occupant.DateOfBirth.Value == default)
        {
            throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở chưa có tài khoản phải có ngày sinh.");
        }

        if (occupant.Document is null)
        {
            throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở chưa có tài khoản phải có giấy tờ.");
        }

        if (string.IsNullOrWhiteSpace(occupant.Document.DocumentType) ||
            string.IsNullOrWhiteSpace(occupant.Document.DocumentNumber) ||
            string.IsNullOrWhiteSpace(occupant.Document.FrontImageObjectKey))
        {
            throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Giấy tờ người ở phải có loại giấy tờ, số giấy tờ và ảnh mặt trước.");
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record VerifiedOccupantAccount(
    Guid UserId,
    string FullName,
    string? PhoneNumber,
    DateOnly DateOfBirth);
