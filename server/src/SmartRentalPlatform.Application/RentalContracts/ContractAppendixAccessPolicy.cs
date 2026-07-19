using System.Text.Json;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class ContractAppendixAccessPolicy
{
    public static void EnsureCanUpdateRevision(Guid userId, ContractAppendix appendix)
    {
        if (appendix.Status == ContractAppendixStatus.LandlordRevisionRequested)
        {
            if (GetCurrentMainTenantUserId(appendix.RentalContract) == userId)
            {
                return;
            }
        }

        if (appendix.Status == ContractAppendixStatus.TenantRevisionRequested)
        {
            if (appendix.RentalContract.Room.RoomingHouse.LandlordUserId == userId)
            {
                return;
            }
        }

        throw new ConflictException(
            ErrorCodes.ContractAppendixInvalidStatus,
            "Trạng thái phụ lục không cho phép cập nhật nội dung.",
            new { appendix.Id, currentStatus = appendix.Status.ToString() });
    }

    public static void EnsureCanAccess(Guid userId, RentalContract contract)
    {
        if (ContractDocumentAccessPolicy.HasContractRelationship(userId, contract))
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "Bạn không có quyền thao tác với hợp đồng này.",
            new { contract.Id });
    }

    public static void EnsureCanViewAppendix(Guid userId, ContractAppendix appendix)
    {
        if (CanViewAppendix(userId, appendix))
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "Bạn không có quyền xem phụ lục này.",
            new { appendix.Id });
    }

    public static bool CanViewAppendix(Guid userId, ContractAppendix appendix)
    {
        var contract = appendix.RentalContract;

        if (appendix.Status is ContractAppendixStatus.Active or ContractAppendixStatus.Cancelled)
        {
            return ContractDocumentAccessPolicy.CanViewFullAppendix(userId, appendix) ||
                   ContractDocumentAccessPolicy.CanViewMaskedAppendix(userId, contract, appendix);
        }

        if (appendix.Status == ContractAppendixStatus.PendingSignature)
        {
            if (appendix.CreatedByUserId == userId)
            {
                return true;
            }

            var creatorSignature = appendix.Signatures.FirstOrDefault(x => x.SignerUserId == appendix.CreatedByUserId);
            var hasCreatorSigned = creatorSignature != null && creatorSignature.Status == ContractSignatureStatus.Signed;

            if (!hasCreatorSigned)
            {
                return false;
            }
        }

        return appendix.CreatedByUserId == userId ||
               contract.Room.RoomingHouse.LandlordUserId == userId ||
               GetCurrentMainTenantUserId(contract) == userId ||
               appendix.Signatures.Any(x => x.SignerUserId == userId);
    }

    public static ContractSignerRole GetSignerRole(Guid userId, RentalContract contract)
    {
        if (contract.Room.RoomingHouse.LandlordUserId == userId)
        {
            return ContractSignerRole.Landlord;
        }

        if (GetCurrentMainTenantUserId(contract) == userId)
        {
            return ContractSignerRole.Tenant;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "Bạn không có quyền thao tác với phụ lục này.",
            new { contract.Id });
    }

    public static Guid GetCurrentMainTenantUserId(RentalContract contract)
    {
        Guid currentMainTenantUserId = contract.MainTenantUserId;

        foreach (ContractAppendix appendix in GetAppliedAppendicesInOrder(contract))
        {
            foreach (ContractAppendixChange change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (!IsMainTenantUserIdChange(change))
                {
                    continue;
                }

                Guid? newMainTenantUserId = ExtractUserId(change.NewValue);
                if (newMainTenantUserId.HasValue)
                {
                    currentMainTenantUserId = newMainTenantUserId.Value;
                }
            }
        }

        return currentMainTenantUserId;
    }

    public static DateOnly GetCurrentContractEndDate(RentalContract contract)
    {
        DateOnly currentEndDate = contract.EndDate;

        foreach (ContractAppendix appendix in GetAppliedAppendicesInOrder(contract))
        {
            foreach (ContractAppendixChange change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (change.TargetType != ContractAppendixTargetType.Contract ||
                    change.ChangeType != ContractAppendixChangeType.Update ||
                    NormalizeFieldName(change.FieldName) != "enddate")
                {
                    continue;
                }

                if (DateOnly.TryParse(change.NewValue, out DateOnly endDate))
                {
                    currentEndDate = endDate;
                }
            }
        }

        return currentEndDate;
    }

    public static void EnsureAppendixNotSigned(ContractAppendix appendix, ContractSignerRole signerRole)
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

    public static bool HasBothSignatures(ContractAppendix appendix, ContractSignerRole newSignerRole)
    {
        return appendix.Signatures.Any(x => x.SignerRole == ContractSignerRole.Landlord) ||
               newSignerRole == ContractSignerRole.Landlord
            ? appendix.Signatures.Any(x => x.SignerRole == ContractSignerRole.Tenant) ||
              newSignerRole == ContractSignerRole.Tenant
            : false;
    }

    private static IReadOnlyCollection<Guid> GetMainTenantUserIds(RentalContract contract)
    {
        var userIds = new HashSet<Guid> { contract.MainTenantUserId };

        foreach (ContractAppendix appendix in GetAppliedAppendicesInOrder(contract))
        {
            foreach (ContractSignature tenantSignature in appendix.Signatures
                .Where(x => x.SignerRole == ContractSignerRole.Tenant))
            {
                userIds.Add(tenantSignature.SignerUserId);
            }

            foreach (ContractAppendixChange change in appendix.Changes)
            {
                if (!IsMainTenantUserIdChange(change))
                {
                    continue;
                }

                Guid? oldUserId = ExtractUserId(change.OldValue);
                if (oldUserId.HasValue)
                {
                    userIds.Add(oldUserId.Value);
                }

                Guid? newUserId = ExtractUserId(change.NewValue);
                if (newUserId.HasValue)
                {
                    userIds.Add(newUserId.Value);
                }
            }
        }

        return userIds;
    }

    private static Guid GetMainTenantUserIdBeforeAppendix(
        RentalContract contract,
        ContractAppendix targetAppendix)
    {
        ContractAppendixChange? targetMainTenantChange = targetAppendix.Changes
            .OrderBy(x => x.SortOrder)
            .FirstOrDefault(IsMainTenantUserIdChange);
        Guid? oldMainTenantUserId = ExtractUserId(targetMainTenantChange?.OldValue);
        if (oldMainTenantUserId.HasValue)
        {
            return oldMainTenantUserId.Value;
        }

        Guid? tenantSignerUserId = targetAppendix.Signatures
            .Where(x => x.SignerRole == ContractSignerRole.Tenant)
            .OrderBy(x => x.SignedAt)
            .Select(x => (Guid?)x.SignerUserId)
            .FirstOrDefault();
        if (tenantSignerUserId.HasValue)
        {
            return tenantSignerUserId.Value;
        }

        Guid currentMainTenantUserId = contract.MainTenantUserId;

        foreach (ContractAppendix appendix in GetAppliedAppendicesBefore(contract, targetAppendix))
        {
            foreach (ContractAppendixChange change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (!IsMainTenantUserIdChange(change))
                {
                    continue;
                }

                Guid? newMainTenantUserId = ExtractUserId(change.NewValue);
                if (newMainTenantUserId.HasValue)
                {
                    currentMainTenantUserId = newMainTenantUserId.Value;
                }
            }
        }

        return currentMainTenantUserId;
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

    private static IEnumerable<ContractAppendix> GetAppliedAppendicesBefore(
        RentalContract contract,
        ContractAppendix targetAppendix)
    {
        return GetAppliedAppendicesInOrder(contract)
            .Where(x => x.Id != targetAppendix.Id && x.CreatedAt <= targetAppendix.CreatedAt);
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

    private static Guid? ExtractUserId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (Guid.TryParse(trimmed, out Guid directGuid))
        {
            return directGuid;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(document.RootElement.GetString(), out Guid jsonStringGuid))
            {
                return jsonStringGuid;
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (string propertyName in new[] { "userId", "id", "mainTenantUserId" })
                {
                    if (document.RootElement.TryGetProperty(propertyName, out JsonElement property) &&
                        property.ValueKind == JsonValueKind.String &&
                        Guid.TryParse(property.GetString(), out Guid objectGuid))
                    {
                        return objectGuid;
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static string NormalizeFieldName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }
}
