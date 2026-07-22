using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Enums.Kyc;

namespace SmartRentalPlatform.Application.RentalContracts;

public sealed class ContractAppendixOccupantAccountResolver
{
    private readonly IAppDbContext context;

    public ContractAppendixOccupantAccountResolver(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<VerifiedOccupantAccount> GetVerifiedOccupantAccountByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(x => x.UserProfile)
            .FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower() && x.DeletedAt == null, cancellationToken);

        if (user is null)
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Người ở có tài khoản không tồn tại.",
                new { email });
        }

        var approvedKyc = await context.KycVerifications
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.Status == KycVerificationStatus.Approved)
            .OrderByDescending(x => x.ReviewedAt ?? x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (approvedKyc is null)
        {
            throw new BadRequestException(
                ErrorCodes.KycRequired,
                "Người ở có tài khoản phải hoàn tất KYC trước khi được thêm vào phụ lục.",
                new { email });
        }

        var fullName = RentalContractTextHelper.NormalizeOptionalText(approvedKyc.OcrFullName)
            ?? RentalContractTextHelper.NormalizeOptionalText(user.UserProfile?.FullName)
            ?? RentalContractTextHelper.NormalizeOptionalText(user.DisplayName);
        var dateOfBirth = approvedKyc.OcrDateOfBirth ?? user.UserProfile?.DateOfBirth;

        if (string.IsNullOrWhiteSpace(fullName) || !dateOfBirth.HasValue)
        {
            throw new BadRequestException(
                ErrorCodes.KycRequired,
                "Thông tin KYC đã duyệt của người ở chưa đủ họ tên hoặc ngày sinh.",
                new { email });
        }

        return new VerifiedOccupantAccount(
            user.Id,
            fullName,
            RentalContractTextHelper.NormalizeOptionalText(user.PhoneNumber),
            dateOfBirth.Value);
    }
}
