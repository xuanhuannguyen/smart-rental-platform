using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Kyc;

namespace SmartRentalPlatform.Application.RentalContracts;

public sealed class RentalContractPreviewBuilder
{
    private readonly IAppDbContext context;
    private readonly ISensitiveDataProtector sensitiveDataProtector;

    public RentalContractPreviewBuilder(
        IAppDbContext context,
        ISensitiveDataProtector sensitiveDataProtector)
    {
        this.context = context;
        this.sensitiveDataProtector = sensitiveDataProtector;
    }

    public async Task<ContractRenderOptions> BuildAsync(
        RentalContract contract,
        ContractPreviewViewerAccess viewerAccess,
        CancellationToken cancellationToken)
    {
        var userDocumentNumbersByUserId = viewerAccess.ShowFullDocumentNumbers
            ? await GetDecryptedUserDocumentNumbersAsync(contract, cancellationToken)
            : new Dictionary<Guid, string?>();
        var occupantDocumentNumbersByDocumentId = viewerAccess.ShowFullDocumentNumbers
            ? GetDecryptedOccupantDocumentNumbers(contract)
            : new Dictionary<Guid, string?>();

        return new ContractRenderOptions
        {
            ViewerMode = viewerAccess.ViewerMode,
            ShowFullDocumentNumbers = viewerAccess.ShowFullDocumentNumbers,
            VisibleOccupantIds = viewerAccess.VisibleOccupantIds,
            UserDocumentNumbersByUserId = userDocumentNumbersByUserId,
            OccupantDocumentNumbersByDocumentId = occupantDocumentNumbersByDocumentId
        };
    }

    public static ContractPreviewViewerAccess? ResolveViewerAccess(Guid userId, RentalContract contract)
    {
        if (contract.Room.RoomingHouse.LandlordUserId == userId ||
            RentalContractResponseMapper.GetCurrentMainTenantUserId(contract) == userId)
        {
            return new ContractPreviewViewerAccess("Full", ShowFullDocumentNumbers: true, null);
        }

        var visibleOccupantIds = contract.Occupants
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToList();
        if (visibleOccupantIds.Count > 0)
        {
            return new ContractPreviewViewerAccess(
                "MaskedLimited",
                ShowFullDocumentNumbers: false,
                visibleOccupantIds);
        }

        return null;
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> GetDecryptedUserDocumentNumbersAsync(
        RentalContract contract,
        CancellationToken cancellationToken)
    {
        var userIds = new HashSet<Guid>
        {
            contract.Room.RoomingHouse.LandlordUserId,
            contract.MainTenantUserId
        };
        foreach (var occupantUserId in contract.Occupants
            .Where(x => x.UserId.HasValue)
            .Select(x => x.UserId!.Value))
        {
            userIds.Add(occupantUserId);
        }

        return (await context.KycVerifications
                .AsNoTracking()
                .Where(x => userIds.Contains(x.UserId) && x.Status == KycVerificationStatus.Approved)
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.UserId)
            .Select(x => x.OrderByDescending(k => k.ReviewedAt ?? k.UpdatedAt).First())
            .Select(x => new
            {
                x.UserId,
                DocumentNumber = DecryptDocumentNumber(x.DocumentNumberEncrypted)
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
                DocumentNumber = DecryptDocumentNumber(x.DocumentNumberEncrypted)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.DocumentNumber))
            .ToDictionary(x => x.Id, x => x.DocumentNumber);
    }

    private string? DecryptDocumentNumber(string? encryptedDocumentNumber)
    {
        if (string.IsNullOrWhiteSpace(encryptedDocumentNumber))
        {
            return null;
        }

        try
        {
            return sensitiveDataProtector.Decrypt(encryptedDocumentNumber);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

public sealed record ContractPreviewViewerAccess(
    string ViewerMode,
    bool ShowFullDocumentNumbers,
    IReadOnlyCollection<Guid>? VisibleOccupantIds);
