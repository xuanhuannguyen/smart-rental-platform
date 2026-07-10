using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
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

        if (!actorUserId.HasValue ||
            !string.Equals(mediaAsset.LinkedEntityType, nameof(ContractFile), StringComparison.Ordinal) ||
            !mediaAsset.LinkedEntityId.HasValue)
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
                x => x.Id == mediaAsset.LinkedEntityId.Value &&
                     x.RentalContractAppendixId == null,
                cancellationToken);

        if (contractFile?.RentalContract is null)
        {
            return false;
        }

        return CanViewContractFile(actorUserId.Value, contractFile.RentalContract, contractFile);
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

    private static bool CanViewContractFile(Guid userId, RentalContract contract, ContractFile file)
    {
        if (file.RentalContractAppendixId is not null)
        {
            return false;
        }

        return file.FileVariant == ContractFileVariant.Raw
            ? CanViewRawContractFile(userId, contract)
            : CanViewMaskedContractFile(userId, contract);
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
