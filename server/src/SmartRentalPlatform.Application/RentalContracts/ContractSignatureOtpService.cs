using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Users;

namespace SmartRentalPlatform.Application.RentalContracts;

public class ContractSignatureOtpService : IContractSignatureOtpService
{
    private static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private readonly IAppDbContext context;
    private readonly ITokenService tokenService;
    private readonly IEmailSender emailSender;

    public ContractSignatureOtpService(
        IAppDbContext context,
        ITokenService tokenService,
        IEmailSender emailSender)
    {
        this.context = context;
        this.tokenService = tokenService;
        this.emailSender = emailSender;
    }

    public async Task<RequestContractSignatureOtpResponse?> RequestOtpAsync(
        Guid userId,
        Guid contractId,
        ContractSignerRole signerRole,
        CancellationToken cancellationToken = default)
    {
        var contract = await QueryContractForSigning()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        EnsureCanRequestOtp(userId, contract, signerRole);

        var signer = GetSigner(contract, signerRole);
        var now = DateTimeOffset.UtcNow;

        var latestToken = await context.UserTokens
            .AsNoTracking()
            .Where(x => x.UserId == userId &&
                        x.TokenType == TokenType.ContractSignatureOtp)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestToken is not null && latestToken.CreatedAt > now.Subtract(ResendCooldown))
        {
            throw new TooManyRequestsException(
                ErrorCodes.OtpResendTooSoon,
                "Vui lòng chờ 60 giây trước khi gửi lại OTP ký hợp đồng.");
        }

        await RevokeActiveSignatureOtpTokensAsync(userId, now, cancellationToken);

        var otp = tokenService.GenerateOtp();
        var expiresAt = now.Add(OtpTtl);

        context.UserTokens.Add(new UserToken
        {
            UserId = userId,
            TokenType = TokenType.ContractSignatureOtp,
            TokenHash = HashSignatureOtp(contractId, signerRole, otp),
            TokenFamilyId = Guid.NewGuid(),
            ExpiresAt = expiresAt,
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        await emailSender.SendContractSignatureOtpAsync(
            signer.Email,
            signer.DisplayName,
            contract.ContractNumber,
            signerRole.ToString(),
            otp,
            cancellationToken);

        return new RequestContractSignatureOtpResponse
        {
            ContractId = contract.Id,
            SignerRole = signerRole.ToString(),
            ExpiresAt = expiresAt,
            MaskedEmail = MaskEmail(signer.Email)
        };
    }

    public async Task VerifyAndConsumeOtpAsync(
        Guid userId,
        Guid contractId,
        ContractSignerRole signerRole,
        string? otp,
        CancellationToken cancellationToken = default)
    {
        await VerifyAndConsumeHashAsync(
            userId,
            HashSignatureOtp(contractId, signerRole, NormalizeOtp(otp)),
            cancellationToken);
    }

    public async Task<RequestContractSignatureOtpResponse?> RequestAppendixOtpAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        ContractSignerRole signerRole,
        CancellationToken cancellationToken = default)
    {
        var appendix = await context.ContractAppendices
            .AsNoTracking()
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Room)
                    .ThenInclude(x => x.RoomingHouse)
                        .ThenInclude(x => x.Landlord)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.MainTenantUser)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Appendices)
                    .ThenInclude(x => x.Changes)
            .Include(x => x.Signatures)
            .FirstOrDefaultAsync(
                x => x.Id == appendixId &&
                     x.RentalContractId == contractId &&
                     x.RentalContract.DeletedAt == null,
                cancellationToken);

        if (appendix is null)
        {
            return null;
        }

        EnsureCanRequestAppendixOtp(userId, appendix, signerRole);

        var signer = signerRole == ContractSignerRole.Landlord
            ? GetSigner(appendix.RentalContract, signerRole)
            : await context.Users
                .AsNoTracking()
                .FirstAsync(x => x.Id == userId && x.DeletedAt == null, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        await EnsureCanResendAsync(userId, now, cancellationToken);
        await RevokeActiveSignatureOtpTokensAsync(userId, now, cancellationToken);

        var otp = tokenService.GenerateOtp();
        var expiresAt = now.Add(OtpTtl);

        context.UserTokens.Add(new UserToken
        {
            UserId = userId,
            TokenType = TokenType.ContractSignatureOtp,
            TokenHash = HashAppendixOtp(appendixId, signerRole, otp),
            TokenFamilyId = Guid.NewGuid(),
            ExpiresAt = expiresAt,
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        await emailSender.SendContractSignatureOtpAsync(
            signer.Email,
            signer.DisplayName,
            $"{appendix.RentalContract.ContractNumber} - {appendix.AppendixNumber}",
            signerRole.ToString(),
            otp,
            cancellationToken);

        return new RequestContractSignatureOtpResponse
        {
            ContractId = appendix.RentalContractId,
            SignerRole = signerRole.ToString(),
            ExpiresAt = expiresAt,
            MaskedEmail = MaskEmail(signer.Email)
        };
    }

    public async Task VerifyAndConsumeAppendixOtpAsync(
        Guid userId,
        Guid appendixId,
        ContractSignerRole signerRole,
        string? otp,
        CancellationToken cancellationToken = default)
    {
        await VerifyAndConsumeHashAsync(
            userId,
            HashAppendixOtp(appendixId, signerRole, NormalizeOtp(otp)),
            cancellationToken);
    }

    private async Task VerifyAndConsumeHashAsync(
        Guid userId,
        string tokenHash,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var token = await context.UserTokens
            .Where(x => x.UserId == userId &&
                        x.TokenType == TokenType.ContractSignatureOtp &&
                        x.TokenHash == tokenHash &&
                        x.UsedAt == null &&
                        x.RevokedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null)
        {
            throw new BadRequestException(
                ErrorCodes.OtpInvalid,
                "OTP ký hợp đồng không hợp lệ.");
        }

        if (token.ExpiresAt <= now)
        {
            token.RevokedAt = now;
            token.RevokedReason = TokenRevokedReason.Expired;

            throw new BadRequestException(
                ErrorCodes.OtpExpired,
                "OTP ký hợp đồng đã hết hạn.");
        }

        token.UsedAt = now;
        token.RevokedAt = now;
        token.RevokedReason = TokenRevokedReason.Used;
    }

    private IQueryable<RentalContract> QueryContractForSigning()
    {
        return context.RentalContracts
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
                    .ThenInclude(x => x.Landlord)
            .Include(x => x.MainTenantUser)
            .Include(x => x.Signatures);
    }

    private void EnsureCanRequestOtp(
        Guid userId,
        RentalContract contract,
        ContractSignerRole signerRole)
    {
        if (signerRole == ContractSignerRole.Landlord)
        {
            if (contract.Room.RoomingHouse.LandlordUserId != userId)
            {
                throw new ForbiddenException(
                    ErrorCodes.RentalContractForbidden,
                    "Bạn không có quyền ký hợp đồng này.",
                    new { contract.Id });
            }

            if (contract.Status is not (RentalContractStatus.PendingLandlordSignature or RentalContractStatus.TenantRevisionRequested))
            {
                throw new ConflictException(
                    ErrorCodes.RentalContractInvalidStatus,
                    "Trạng thái hợp đồng không cho phép chủ trọ ký.",
                    new { contract.Id, currentStatus = contract.Status.ToString() });
            }

            EnsureNotSigned(contract, signerRole);
            return;
        }

        if (contract.MainTenantUserId != userId)
        {
            throw new ForbiddenException(
                ErrorCodes.RentalContractForbidden,
                "Bạn không có quyền ký hợp đồng này.",
                new { contract.Id });
        }

        if (contract.Status != RentalContractStatus.PendingTenantSignature)
        {
            throw new ConflictException(
                ErrorCodes.RentalContractInvalidStatus,
                "Trạng thái hợp đồng không cho phép người thuê ký.",
                new { contract.Id, currentStatus = contract.Status.ToString() });
        }

        EnsureNotSigned(contract, signerRole);
    }

    private static void EnsureNotSigned(RentalContract contract, ContractSignerRole signerRole)
    {
        if (!contract.Signatures.Any(x => x.SignerRole == signerRole))
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RentalContractAlreadySigned,
            "Bên này đã ký hợp đồng.",
            new { contract.Id, signerRole = signerRole.ToString() });
    }

    private static User GetSigner(RentalContract contract, ContractSignerRole signerRole)
    {
        return signerRole == ContractSignerRole.Landlord
            ? contract.Room.RoomingHouse.Landlord
            : contract.MainTenantUser;
    }

    private async Task RevokeActiveSignatureOtpTokensAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeTokens = await context.UserTokens
            .Where(x => x.UserId == userId &&
                        x.TokenType == TokenType.ContractSignatureOtp &&
                        x.UsedAt == null &&
                        x.RevokedAt == null &&
                        x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
            token.RevokedReason = TokenRevokedReason.Replaced;
        }
    }

    private string HashSignatureOtp(Guid contractId, ContractSignerRole signerRole, string otp)
    {
        return tokenService.HashToken($"{contractId:N}:{signerRole}:{otp.Trim()}");
    }

    private string HashAppendixOtp(Guid appendixId, ContractSignerRole signerRole, string otp)
    {
        return tokenService.HashToken($"appendix:{appendixId:N}:{signerRole}:{otp.Trim()}");
    }

    private async Task EnsureCanResendAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var latestToken = await context.UserTokens
            .AsNoTracking()
            .Where(x => x.UserId == userId &&
                        x.TokenType == TokenType.ContractSignatureOtp)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestToken is not null && latestToken.CreatedAt > now.Subtract(ResendCooldown))
        {
            throw new TooManyRequestsException(
                ErrorCodes.OtpResendTooSoon,
                "Vui lòng chờ 60 giây trước khi gửi lại OTP ký hợp đồng.");
        }
    }

    private static void EnsureCanRequestAppendixOtp(
        Guid userId,
        ContractAppendix appendix,
        ContractSignerRole signerRole)
    {
        if (appendix.Status != ContractAppendixStatus.PendingSignature)
        {
            throw new ConflictException(
                ErrorCodes.ContractAppendixInvalidStatus,
                "Trạng thái phụ lục không cho phép ký.",
                new { appendix.Id, currentStatus = appendix.Status.ToString() });
        }

        if (signerRole == ContractSignerRole.Landlord)
        {
            if (appendix.RentalContract.Room.RoomingHouse.LandlordUserId != userId)
            {
                throw new ForbiddenException(
                    ErrorCodes.RentalContractForbidden,
                    "Bạn không có quyền ký phụ lục này.",
                    new { appendix.Id });
            }

            EnsureAppendixNotSigned(appendix, signerRole);
            return;
        }

        if (GetCurrentMainTenantUserId(appendix.RentalContract) != userId)
        {
            throw new ForbiddenException(
                ErrorCodes.RentalContractForbidden,
                "Bạn không có quyền ký phụ lục này.",
                new { appendix.Id });
        }

        EnsureAppendixNotSigned(appendix, signerRole);
    }

    private static Guid GetCurrentMainTenantUserId(RentalContract contract)
    {
        var currentMainTenantUserId = contract.MainTenantUserId;

        foreach (var appendix in contract.Appendices
            .Where(x => x.Status == ContractAppendixStatus.Active)
            .OrderBy(x => x.ActivatedAt ?? x.UpdatedAt)
            .ThenBy(x => x.CreatedAt))
        {
            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (change.TargetType != ContractAppendixTargetType.Contract ||
                    change.ChangeType != ContractAppendixChangeType.Update ||
                    NormalizeFieldName(change.FieldName) != "maintenantuserid")
                {
                    continue;
                }

                var newMainTenantUserId = ExtractUserId(change.NewValue);
                if (newMainTenantUserId.HasValue)
                {
                    currentMainTenantUserId = newMainTenantUserId.Value;
                }
            }
        }

        return currentMainTenantUserId;
    }

    private static Guid? ExtractUserId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('"');
        if (Guid.TryParse(trimmed, out var directGuid))
        {
            return directGuid;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String &&
                Guid.TryParse(root.GetString(), out var jsonStringGuid))
            {
                return jsonStringGuid;
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("userId", out var userIdElement) &&
                userIdElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(userIdElement.GetString(), out var objectGuid))
            {
                return objectGuid;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string NormalizeFieldName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
    }

    private static void EnsureAppendixNotSigned(ContractAppendix appendix, ContractSignerRole signerRole)
    {
        if (!appendix.Signatures.Any(x => x.SignerRole == signerRole))
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.ContractAppendixAlreadySigned,
            "Bên này đã ký phụ lục.",
            new { appendix.Id, signerRole = signerRole.ToString() });
    }

    private static string NormalizeOtp(string? otp)
    {
        if (string.IsNullOrWhiteSpace(otp))
        {
            throw new BadRequestException(
                ErrorCodes.OtpInvalid,
                "OTP ký hợp đồng không được để trống.");
        }

        return otp.Trim();
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@', 2);
        if (parts.Length != 2)
        {
            return "***";
        }

        var name = parts[0];
        if (string.IsNullOrEmpty(name))
        {
            return $"***@{parts[1]}";
        }

        var maskedName = name.Length <= 2
            ? $"{name[0]}***"
            : $"{name[0]}***{name[^1]}";

        return $"{maskedName}@{parts[1]}";
    }
}
