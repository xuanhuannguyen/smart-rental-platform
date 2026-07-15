using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Kyc;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public sealed class ContractAppendixRenderOptionsBuilder
{
    private readonly IAppDbContext context;
    private readonly RentalContractDocumentHelper documentHelper;

    public ContractAppendixRenderOptionsBuilder(
        IAppDbContext context,
        RentalContractDocumentHelper documentHelper)
    {
        this.context = context;
        this.documentHelper = documentHelper;
    }

    public async Task<ContractRenderOptions> BuildAsync(
        ContractAppendix appendix,
        ContractFileVariant fileVariant,
        CancellationToken cancellationToken)
    {
        bool showFullDocumentNumbers = fileVariant == ContractFileVariant.Raw;
        return new ContractRenderOptions
        {
            ViewerMode = fileVariant.ToString(),
            ShowFullDocumentNumbers = showFullDocumentNumbers,
            VisibleOccupantIds = null,
            UserDocumentNumbersByUserId = showFullDocumentNumbers
                ? await GetDecryptedUserDocumentNumbersAsync(appendix.RentalContract, appendix, cancellationToken)
                : new Dictionary<Guid, string?>(),
            OccupantDocumentNumbersByDocumentId = showFullDocumentNumbers
                ? GetDecryptedOccupantDocumentNumbers(appendix.RentalContract)
                : new Dictionary<Guid, string?>()
        };
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> GetDecryptedUserDocumentNumbersAsync(
        RentalContract contract,
        ContractAppendix appendix,
        CancellationToken cancellationToken)
    {
        var userIds = new HashSet<Guid>
        {
            contract.Room.RoomingHouse.LandlordUserId,
            contract.MainTenantUserId
        };

        foreach (Guid occupantUserId in contract.Occupants
                     .Where(x => x.UserId.HasValue)
                     .Select(x => x.UserId!.Value))
        {
            userIds.Add(occupantUserId);
        }

        foreach (Guid addedUserId in GetAddedOccupantUserIds(appendix))
        {
            userIds.Add(addedUserId);
        }

        var approvedKycs = await context.KycVerifications
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && x.Status == KycVerificationStatus.Approved)
            .ToListAsync(cancellationToken);

        return approvedKycs
            .GroupBy(x => x.UserId)
            .Select(x => x.OrderByDescending(k => k.ReviewedAt ?? k.UpdatedAt).First())
            .Select(x => new
            {
                x.UserId,
                DocumentNumber = documentHelper.DecryptDocumentNumber(x.DocumentNumberEncrypted)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.DocumentNumber))
            .ToDictionary(x => x.UserId, x => x.DocumentNumber);
    }

    private IReadOnlyDictionary<Guid, string?> GetDecryptedOccupantDocumentNumbers(RentalContract contract)
    {
        return contract.Occupants
            .SelectMany(x => x.Documents)
            .Select(x => new
            {
                x.Id,
                DocumentNumber = documentHelper.DecryptDocumentNumber(x.DocumentNumberEncrypted)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.DocumentNumber))
            .ToDictionary(x => x.Id, x => x.DocumentNumber);
    }

    private static IEnumerable<Guid> GetAddedOccupantUserIds(ContractAppendix appendix)
    {
        foreach (ContractAppendixChange change in appendix.Changes)
        {
            if (change.TargetType != ContractAppendixTargetType.ContractOccupant ||
                change.ChangeType != ContractAppendixChangeType.Add)
            {
                continue;
            }

            Guid? userId = ExtractUserId(change.NewValue);
            if (userId.HasValue)
            {
                yield return userId.Value;
            }
        }
    }

    private static Guid? ExtractUserId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim().Trim('"');
        if (Guid.TryParse(trimmed, out Guid directGuid))
        {
            return directGuid;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(value);
            JsonElement root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String &&
                Guid.TryParse(root.GetString(), out Guid jsonStringGuid))
            {
                return jsonStringGuid;
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("userId", out JsonElement userIdElement) &&
                userIdElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(userIdElement.GetString(), out Guid objectGuid))
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
}
