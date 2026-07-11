using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Users;
using System.Text.Json;

namespace SmartRentalPlatform.Infrastructure.Media;

public class DefaultMediaPermissionService : IMediaPermissionService
{
    private readonly IAppDbContext dbContext;

    public DefaultMediaPermissionService(IAppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public Task<bool> CanViewAsync(
        Guid? actorUserId,
        MediaAsset mediaAsset,
        CancellationToken cancellationToken = default)
    {
        return CanAccessAsync(actorUserId, mediaAsset, cancellationToken);
    }

    public Task<bool> CanDownloadAsync(
        Guid? actorUserId,
        MediaAsset mediaAsset,
        CancellationToken cancellationToken = default)
    {
        return CanAccessAsync(actorUserId, mediaAsset, cancellationToken);
    }

    public Task<bool> CanDeleteAsync(
        Guid? actorUserId,
        MediaAsset mediaAsset,
        CancellationToken cancellationToken = default)
    {
        if (mediaAsset.Status == MediaStatus.Deleted)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(
            actorUserId.HasValue &&
            mediaAsset.OwnerUserId.HasValue &&
            actorUserId.Value == mediaAsset.OwnerUserId.Value);
    }

    private async Task<bool> CanAccessAsync(
        Guid? actorUserId,
        MediaAsset mediaAsset,
        CancellationToken cancellationToken)
    {
        if (CanAccessByDefault(actorUserId, mediaAsset))
        {
            return true;
        }

        if (actorUserId.HasValue && await IsAdminAsync(actorUserId.Value, cancellationToken))
        {
            return true;
        }

        if (!actorUserId.HasValue || !mediaAsset.LinkedEntityId.HasValue)
        {
            return false;
        }

        if (string.Equals(mediaAsset.LinkedEntityType, nameof(RoomingHouseLegalDocument), StringComparison.Ordinal))
        {
            return await CanAccessRoomingHouseLegalDocumentAsync(
                actorUserId.Value,
                mediaAsset.LinkedEntityId.Value,
                cancellationToken);
        }

        if (string.Equals(mediaAsset.LinkedEntityType, nameof(MeterReading), StringComparison.Ordinal))
        {
            return await CanAccessMeterReadingAsync(
                actorUserId.Value,
                mediaAsset.LinkedEntityId.Value,
                cancellationToken);
        }

        if (!string.Equals(mediaAsset.LinkedEntityType, nameof(ContractFile), StringComparison.Ordinal))
        {
            return false;
        }

        var contractFile = await dbContext.ContractFiles
            .AsNoTracking()
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Room)
                    .ThenInclude(x => x.RoomingHouse)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Occupants)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Appendices)
                    .ThenInclude(x => x.Changes)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Appendices)
                    .ThenInclude(x => x.Signatures)
            .FirstOrDefaultAsync(
                x => x.Id == mediaAsset.LinkedEntityId.Value,
                cancellationToken);

        if (contractFile?.RentalContract is null)
        {
            return false;
        }

        return CanViewContractFile(actorUserId.Value, contractFile.RentalContract, contractFile);
    }

    private async Task<bool> CanAccessRoomingHouseLegalDocumentAsync(
        Guid actorUserId,
        Guid roomingHouseId,
        CancellationToken cancellationToken)
    {
        var landlordUserId = await dbContext.RoomingHouseLegalDocuments
            .AsNoTracking()
            .Where(x => x.RoomingHouseId == roomingHouseId)
            .Select(x => x.RoomingHouse.LandlordUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return landlordUserId != Guid.Empty && landlordUserId == actorUserId;
    }

    private async Task<bool> CanAccessMeterReadingAsync(
        Guid actorUserId,
        Guid meterReadingId,
        CancellationToken cancellationToken)
    {
        var meterReading = await dbContext.MeterReadings
            .AsNoTracking()
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Room)
                    .ThenInclude(x => x.RoomingHouse)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Occupants)
            .Include(x => x.InvoiceItems)
                .ThenInclude(x => x.Invoice)
            .FirstOrDefaultAsync(x => x.Id == meterReadingId, cancellationToken);

        if (meterReading?.RentalContract is null)
        {
            return false;
        }

        if (meterReading.RentalContract.Room.RoomingHouse.LandlordUserId == actorUserId)
        {
            return true;
        }

        var visibleInvoices = meterReading.InvoiceItems
            .Select(x => x.Invoice)
            .Where(x => x is not null && x.Status != InvoiceStatus.Draft)
            .ToList();

        if (visibleInvoices.Count == 0)
        {
            return false;
        }

        if (visibleInvoices.Any(x => x!.TenantUserId == actorUserId))
        {
            return true;
        }

        return visibleInvoices.Any(invoice => meterReading.RentalContract.Occupants.Any(occupant =>
            occupant.UserId == actorUserId &&
            occupant.Status != ContractOccupantStatus.Voided &&
            occupant.MoveInDate <= invoice!.BillingPeriodEnd &&
            (occupant.MoveOutDate is null || occupant.MoveOutDate >= invoice.BillingPeriodStart)));
    }

    private static bool CanAccessByDefault(Guid? actorUserId, MediaAsset mediaAsset)
    {
        if (mediaAsset.Status == MediaStatus.Deleted)
        {
            return false;
        }

        if (mediaAsset.Visibility == MediaVisibility.Public)
        {
            return true;
        }

        return actorUserId.HasValue &&
            mediaAsset.OwnerUserId.HasValue &&
            actorUserId.Value == mediaAsset.OwnerUserId.Value;
    }

    private Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == userId &&
                     x.UserRoles.Any(ur => ur.RoleId == (int)RoleName.Admin),
                cancellationToken);
    }

    private static bool CanViewContractFile(Guid userId, RentalContract contract, ContractFile file)
    {
        if (file.RentalContractAppendixId is null)
        {
            return file.FileVariant == ContractFileVariant.Raw
                ? CanViewRawContractFile(userId, contract)
                : CanViewMaskedContractFile(userId, contract);
        }

        var appendix = contract.Appendices.FirstOrDefault(x => x.Id == file.RentalContractAppendixId);
        if (appendix is null)
        {
            return false;
        }

        return file.FileVariant == ContractFileVariant.Raw
            ? CanViewRawAppendixFile(userId, contract, appendix)
            : CanViewMaskedAppendixFile(userId, contract, appendix);
    }

    private static bool CanViewRawContractFile(Guid userId, RentalContract contract)
    {
        return contract.Room.RoomingHouse.LandlordUserId == userId ||
               GetMainTenantUserIds(contract).Contains(userId);
    }

    private static bool CanViewMaskedContractFile(Guid userId, RentalContract contract)
    {
        return !CanViewRawContractFile(userId, contract) &&
               contract.Occupants.Any(x => x.UserId == userId);
    }

    private static bool CanViewRawAppendixFile(Guid userId, RentalContract contract, ContractAppendix appendix)
    {
        if (contract.Room.RoomingHouse.LandlordUserId == userId)
        {
            return true;
        }

        if (GetCurrentMainTenantUserId(contract) == userId)
        {
            return true;
        }

        if (GetMainTenantUserIdBeforeAppendix(contract, appendix) == userId)
        {
            return true;
        }

        return IsMainTenantChangedToUser(appendix, userId);
    }

    private static bool CanViewMaskedAppendixFile(Guid userId, RentalContract contract, ContractAppendix appendix)
    {
        if (CanViewRawAppendixFile(userId, contract, appendix))
        {
            return false;
        }

        return contract.Occupants.Any(occupant =>
            occupant.UserId == userId &&
            (occupant.MoveOutDate is null || appendix.EffectiveDate <= occupant.MoveOutDate.Value));
    }

    private static Guid GetMainTenantUserIdBeforeAppendix(
        RentalContract contract,
        ContractAppendix targetAppendix)
    {
        var targetMainTenantChange = targetAppendix.Changes
            .OrderBy(x => x.SortOrder)
            .FirstOrDefault(IsMainTenantUserIdChange);
        var oldMainTenantUserId = ExtractUserId(targetMainTenantChange?.OldValue);
        if (oldMainTenantUserId.HasValue)
        {
            return oldMainTenantUserId.Value;
        }

        var tenantSignerUserId = targetAppendix.Signatures
            .Where(x => x.SignerRole == ContractSignerRole.Tenant)
            .OrderBy(x => x.SignedAt)
            .Select(x => (Guid?)x.SignerUserId)
            .FirstOrDefault();
        if (tenantSignerUserId.HasValue)
        {
            return tenantSignerUserId.Value;
        }

        var currentMainTenantUserId = contract.MainTenantUserId;

        foreach (var appendix in GetAppliedAppendicesBefore(contract, targetAppendix))
        {
            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (!IsMainTenantUserIdChange(change))
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

    private static Guid GetCurrentMainTenantUserId(RentalContract contract)
    {
        var currentMainTenantUserId = contract.MainTenantUserId;

        foreach (var appendix in GetAppliedAppendicesInOrder(contract))
        {
            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (!IsMainTenantUserIdChange(change))
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

    private static IReadOnlyCollection<Guid> GetMainTenantUserIds(RentalContract contract)
    {
        var userIds = new HashSet<Guid> { contract.MainTenantUserId };

        foreach (var appendix in GetAppliedAppendicesInOrder(contract))
        {
            foreach (var tenantSignature in appendix.Signatures
                .Where(x => x.SignerRole == ContractSignerRole.Tenant))
            {
                userIds.Add(tenantSignature.SignerUserId);
            }

            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (!IsMainTenantUserIdChange(change))
                {
                    continue;
                }

                var oldMainTenantUserId = ExtractUserId(change.OldValue);
                if (oldMainTenantUserId.HasValue)
                {
                    userIds.Add(oldMainTenantUserId.Value);
                }

                var newMainTenantUserId = ExtractUserId(change.NewValue);
                if (newMainTenantUserId.HasValue)
                {
                    userIds.Add(newMainTenantUserId.Value);
                }
            }
        }

        return userIds;
    }

    private static bool IsMainTenantChangedToUser(ContractAppendix appendix, Guid userId)
    {
        return appendix.Changes.Any(change =>
            IsMainTenantUserIdChange(change) &&
            ExtractUserId(change.NewValue) == userId);
    }

    private static bool IsMainTenantUserIdChange(ContractAppendixChange change)
    {
        return change.TargetType == ContractAppendixTargetType.Contract &&
               change.ChangeType == ContractAppendixChangeType.Update &&
               NormalizeFieldName(change.FieldName) == "maintenantuserid";
    }

    private static IEnumerable<ContractAppendix> GetAppliedAppendicesInOrder(RentalContract contract)
    {
        return contract.Appendices
            .Where(x =>
                x.AppliedAt.HasValue &&
                x.Status is ContractAppendixStatus.Active or ContractAppendixStatus.Cancelled)
            .OrderBy(x => x.AppliedAt ?? x.ActivatedAt ?? x.UpdatedAt)
            .ThenBy(x => x.CreatedAt);
    }

    private static IEnumerable<ContractAppendix> GetAppliedAppendicesBefore(
        RentalContract contract,
        ContractAppendix targetAppendix)
    {
        return GetAppliedAppendicesInOrder(contract)
            .Where(x => x.Id != targetAppendix.Id && x.CreatedAt <= targetAppendix.CreatedAt);
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
}
