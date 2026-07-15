using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public sealed class ContractDocumentModelFactory : IContractDocumentModelFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly IAppDbContext context;
    private readonly ISensitiveDataProtector sensitiveDataProtector;

    public ContractDocumentModelFactory(
        IAppDbContext context,
        ISensitiveDataProtector sensitiveDataProtector)
    {
        this.context = context;
        this.sensitiveDataProtector = sensitiveDataProtector;
    }

    public async Task<ContractDocumentModel> BuildAsync(
        RentalContract contract,
        ContractDocumentBuildMode mode,
        ContractSigningEnvelope? targetEnvelope = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (mode == ContractDocumentBuildMode.ExistingSnapshotOrLive)
        {
            var sourceEnvelope = await FindLatestUsableSnapshotEnvelopeAsync(contract.Id, cancellationToken);
            if (sourceEnvelope is not null)
            {
                return ReadSnapshot(sourceEnvelope);
            }
        }

        var preparedAt = DateTimeOffset.UtcNow.ToOffset(VietnamOffset);
        var model = await BuildLiveAsync(contract, preparedAt, cancellationToken);

        if (mode == ContractDocumentBuildMode.FreezeNewSnapshot)
        {
            if (targetEnvelope is null || targetEnvelope.RentalContractId != contract.Id)
            {
                throw new InvalidOperationException("A matching signing envelope is required to freeze a contract document snapshot.");
            }

            var json = JsonSerializer.Serialize(model, JsonOptions);
            targetEnvelope.DocumentSnapshotEncrypted = sensitiveDataProtector.Encrypt(json);
            targetEnvelope.DocumentSnapshotSha256Hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
            targetEnvelope.DocumentTemplateVersion = model.TemplateVersion;
            targetEnvelope.DocumentPreparedAt = model.PreparedAt.ToUniversalTime();
        }

        return model;
    }

    private async Task<ContractSigningEnvelope?> FindLatestUsableSnapshotEnvelopeAsync(
        Guid contractId,
        CancellationToken cancellationToken)
    {
        return await context.ContractSigningEnvelopes
            .AsNoTracking()
            .Where(x =>
                x.RentalContractId == contractId &&
                x.RentalContractAppendixId == null &&
                x.DocumentSnapshotEncrypted != null &&
                x.Status != SigningEnvelopeStatus.Failed &&
                x.Status != SigningEnvelopeStatus.Cancelled &&
                x.Status != SigningEnvelopeStatus.Expired)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private ContractDocumentModel ReadSnapshot(ContractSigningEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.DocumentSnapshotEncrypted))
        {
            throw new InvalidOperationException("Signing envelope does not contain a contract document snapshot.");
        }

        try
        {
            var json = sensitiveDataProtector.Decrypt(envelope.DocumentSnapshotEncrypted);
            if (!string.IsNullOrWhiteSpace(envelope.DocumentSnapshotSha256Hash))
            {
                var actualHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
                if (!string.Equals(actualHash, envelope.DocumentSnapshotSha256Hash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Contract document snapshot integrity check failed.");
                }
            }

            var snapshot = JsonSerializer.Deserialize<ContractDocumentModel>(json, JsonOptions);
            if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.ContractNumber))
            {
                throw new InvalidOperationException("Contract document snapshot is empty or invalid.");
            }

            return snapshot;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            throw new InvalidOperationException("Contract document snapshot cannot be read.", exception);
        }
    }

    private async Task<ContractDocumentModel> BuildLiveAsync(
        RentalContract contract,
        DateTimeOffset preparedAt,
        CancellationToken cancellationToken)
    {
        var landlord = contract.Room.RoomingHouse.Landlord;
        var tenant = contract.MainTenantUser;
        var userIds = new HashSet<Guid>
        {
            landlord.Id,
            tenant.Id
        };

        foreach (var userId in contract.Occupants.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value))
        {
            userIds.Add(userId);
        }

        var approvedKycs = await context.KycVerifications
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && x.Status == KycVerificationStatus.Approved)
            .ToListAsync(cancellationToken);

        var latestKycByUserId = approvedKycs
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(k => k.ReviewedAt ?? k.UpdatedAt).First());

        var landlordKyc = latestKycByUserId.GetValueOrDefault(landlord.Id);
        var tenantKyc = latestKycByUserId.GetValueOrDefault(tenant.Id);
        var preparedDate = DateOnly.FromDateTime(preparedAt.DateTime);

        var servicePrices = await context.RoomingHouseServicePrices
            .AsNoTracking()
            .Include(x => x.ServiceType)
            .Where(x =>
                x.RoomingHouseId == contract.Room.RoomingHouseId &&
                x.EffectiveFrom <= preparedDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= preparedDate))
            .ToListAsync(cancellationToken);

        var effectiveServicePrices = servicePrices
            .GroupBy(x => x.ServiceTypeId)
            .Select(x => x.OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.UpdatedAt).First())
            .OrderBy(x => x.ServiceType.Name)
            .Select(x => new ContractDocumentServicePrice
            {
                ServiceName = x.ServiceType.Name,
                PricingUnit = FormatPricingUnit(x.PricingUnit, x.ServiceType.MeterUnitName),
                UnitPrice = x.UnitPrice,
                EffectiveFrom = x.EffectiveFrom,
                Note = "Chưa bao gồm trong giá thuê"
            })
            .ToList();

        var houseRule = await context.RoomingHouseRules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoomingHouseId == contract.Room.RoomingHouseId, cancellationToken);

        var deposit = contract.RoomDeposit ?? await context.RoomDeposits
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contract.RoomDepositId, cancellationToken);

        return new ContractDocumentModel
        {
            TemplateVersion = ContractDocumentTemplate.Version,
            PreparedAt = preparedAt,
            ContractNumber = contract.ContractNumber,
            Landlord = BuildParty(landlord.Id, landlord.DisplayName, landlord.Email, landlord.PhoneNumber,
                landlord.UserProfile?.FullName, landlord.UserProfile?.DateOfBirth,
                landlord.UserProfile?.AddressLine, landlord.UserProfile?.VerifiedCitizenIdMasked, landlordKyc),
            Tenant = BuildParty(tenant.Id, tenant.DisplayName, tenant.Email, tenant.PhoneNumber,
                tenant.UserProfile?.FullName, tenant.UserProfile?.DateOfBirth,
                tenant.UserProfile?.AddressLine, tenant.UserProfile?.VerifiedCitizenIdMasked, tenantKyc),
            Property = new ContractDocumentProperty
            {
                RoomId = contract.Room.Id,
                RoomNumber = contract.Room.RoomNumber,
                RoomingHouseName = contract.Room.RoomingHouse.Name,
                Address = contract.Room.RoomingHouse.AddressDisplay,
                Floor = contract.Room.Floor,
                AreaM2 = contract.Room.AreaM2,
                MaxOccupants = contract.Room.MaxOccupants,
                Description = contract.Room.Description ?? string.Empty
            },
            FinancialTerms = new ContractDocumentFinancialTerms
            {
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                MonthlyRent = contract.MonthlyRent,
                DepositAmount = contract.DepositAmount,
                PaymentDay = contract.PaymentDay,
                DepositPaidAt = deposit?.PaidAt?.ToOffset(VietnamOffset)
            },
            ServicePrices = effectiveServicePrices,
            Occupants = BuildOccupants(contract, latestKycByUserId),
            HouseRules = BuildHouseRuleSummary(houseRule)
        };
    }

    private ContractDocumentParty BuildParty(
        Guid userId,
        string displayName,
        string? email,
        string? phoneNumber,
        string? profileName,
        DateOnly? profileDateOfBirth,
        string? profileAddress,
        string? maskedDocumentNumber,
        KycVerification? kyc)
    {
        return new ContractDocumentParty
        {
            UserId = userId,
            FullName = FirstNonEmpty(profileName, kyc?.OcrFullName, displayName),
            DateOfBirth = profileDateOfBirth ?? kyc?.OcrDateOfBirth,
            DocumentNumber = ResolveDocumentNumber(kyc?.DocumentNumberEncrypted, maskedDocumentNumber ?? kyc?.OcrCitizenIdMasked),
            Address = FirstNonEmpty(profileAddress, kyc?.OcrAddress, "-"),
            PhoneNumber = FirstNonEmpty(phoneNumber, "-"),
            Email = FirstNonEmpty(email, "-")
        };
    }

    private IReadOnlyList<ContractDocumentOccupant> BuildOccupants(
        RentalContract contract,
        IReadOnlyDictionary<Guid, KycVerification> kycByUserId)
    {
        var occupants = contract.Occupants
            .OrderBy(x => x.CreatedAt)
            .Select(occupant =>
            {
                var document = occupant.Documents.OrderBy(x => x.UploadedAt).FirstOrDefault();
                var kyc = occupant.UserId.HasValue ? kycByUserId.GetValueOrDefault(occupant.UserId.Value) : null;
                var documentNumber = ResolveDocumentNumber(
                    kyc?.DocumentNumberEncrypted ?? document?.DocumentNumberEncrypted,
                    occupant.User?.UserProfile?.VerifiedCitizenIdMasked ?? document?.DocumentNumberMasked);

                return new ContractDocumentOccupant
                {
                    OccupantId = occupant.Id,
                    UserId = occupant.UserId,
                    FullName = occupant.FullName,
                    DateOfBirth = occupant.DateOfBirth,
                    DocumentNumber = documentNumber,
                    Relationship = NormalizeRelationship(occupant.RelationshipToMainTenant),
                    MoveInDate = occupant.MoveInDate,
                    MoveOutDate = occupant.MoveOutDate
                };
            })
            .ToList();

        if (occupants.All(x => x.UserId != contract.MainTenantUserId))
        {
            var tenant = contract.MainTenantUser;
            var tenantKyc = kycByUserId.GetValueOrDefault(tenant.Id);
            occupants.Insert(0, new ContractDocumentOccupant
            {
                OccupantId = Guid.Empty,
                UserId = tenant.Id,
                FullName = FirstNonEmpty(tenant.UserProfile?.FullName, tenantKyc?.OcrFullName, tenant.DisplayName),
                DateOfBirth = tenant.UserProfile?.DateOfBirth ?? tenantKyc?.OcrDateOfBirth,
                DocumentNumber = ResolveDocumentNumber(
                    tenantKyc?.DocumentNumberEncrypted,
                    tenant.UserProfile?.VerifiedCitizenIdMasked ?? tenantKyc?.OcrCitizenIdMasked),
                Relationship = "Người thuê chính",
                MoveInDate = contract.StartDate
            });
        }

        return occupants;
    }

    private string ResolveDocumentNumber(string? encryptedNumber, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(encryptedNumber))
        {
            try
            {
                return sensitiveDataProtector.Decrypt(encryptedNumber);
            }
            catch (CryptographicException)
            {
                // Fall back to the already masked value when legacy encrypted data cannot be read.
            }
        }

        return FirstNonEmpty(fallback, "-");
    }

    private static IReadOnlyList<string> BuildHouseRuleSummary(RoomingHouseRule? rule)
    {
        if (rule is null)
        {
            return Array.Empty<string>();
        }

        var values = new[]
        {
            rule.GeneralRules,
            Prefix("Giờ yên tĩnh", rule.QuietHours),
            Prefix("An ninh", rule.SecurityPolicy),
            Prefix("Vệ sinh", rule.CleaningPolicy),
            Prefix("Khách lưu trú", rule.GuestPolicy),
            Prefix("Đỗ xe", rule.ParkingPolicy),
            Prefix("Dịch vụ", rule.UtilityPolicy),
            Prefix("Bồi thường", rule.DamageCompensationPolicy),
            rule.AdditionalNotes
        };

        return values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToList();
    }

    private static string FormatPricingUnit(PricingUnit pricingUnit, string? meterUnitName)
    {
        return pricingUnit switch
        {
            PricingUnit.MeterReading => string.IsNullOrWhiteSpace(meterUnitName)
                ? "Theo chỉ số"
                : $"Theo {meterUnitName}",
            PricingUnit.PerPersonPerMonth => "Người/tháng",
            _ => "Theo tháng"
        };
    }

    private static string NormalizeRelationship(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "self" => "Người thuê chính",
            null or "" => "-",
            _ => value!.Trim()
        };
    }

    private static string? Prefix(string label, string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value.Trim()}";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }
}
