using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public class ContractFileService : IContractFileService
{
    private const string PdfContentType = "application/pdf";

    private readonly IAppDbContext context;
    private readonly IContractPdfRenderer contractPdfRenderer;
    private readonly IPrivateStorageService privateStorageService;
    private readonly ISensitiveDataProtector sensitiveDataProtector;

    public ContractFileService(
        IAppDbContext context,
        IContractPdfRenderer contractPdfRenderer,
        IPrivateStorageService privateStorageService,
        ISensitiveDataProtector sensitiveDataProtector)
    {
        this.context = context;
        this.contractPdfRenderer = contractPdfRenderer;
        this.privateStorageService = privateStorageService;
        this.sensitiveDataProtector = sensitiveDataProtector;
    }

    public async Task<ContractFileResponse?> GenerateSignedContractFileAsync(
        Guid userId,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        var contract = await BaseContractQuery()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        EnsureCanAccess(userId, contract);
        EnsureCanGenerateSignedFile(contract);

        var now = DateTimeOffset.UtcNow;
        var rawFile = await EnsureContractFileVariantAsync(
            contract,
            ContractFileVariant.Raw,
            now,
            cancellationToken);

        await EnsureContractFileVariantAsync(
            contract,
            ContractFileVariant.Masked,
            now,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return MapToResponse(rawFile);
    }

    public async Task<IReadOnlyCollection<ContractFileResponse>> GetFilesAsync(
        Guid userId,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        var contract = await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
            .Include(x => x.Occupants)
            .Include(x => x.Appendices)
                .ThenInclude(x => x.Changes)
            .Include(x => x.Files)
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return Array.Empty<ContractFileResponse>();
        }

        EnsureCanAccess(userId, contract);

        return contract.Files
            .Where(x => CanViewFile(userId, contract, x))
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<(Stream Content, string ContentType, string FileName)?> OpenFileAsync(
        Guid userId,
        Guid contractId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var contract = await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
            .Include(x => x.Occupants)
            .Include(x => x.Appendices)
                .ThenInclude(x => x.Changes)
            .Include(x => x.Files)
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        EnsureCanAccess(userId, contract);

        var file = contract.Files.FirstOrDefault(x => x.Id == fileId);
        if (file is null)
        {
            return null;
        }

        EnsureCanViewFile(userId, contract, file);

        var stream = await privateStorageService.OpenReadAsync(file.StorageObjectKey, cancellationToken);
        return (stream, GuessContentType(file.StorageObjectKey), Path.GetFileName(file.StorageObjectKey));
    }

    private IQueryable<RentalContract> BaseContractQuery()
    {
        return context.RentalContracts
            .Include(x => x.MainTenantUser)
                .ThenInclude(x => x.UserProfile)
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
                    .ThenInclude(x => x.Landlord)
                        .ThenInclude(x => x.UserProfile)
            .Include(x => x.Occupants)
                .ThenInclude(x => x.Documents)
            .Include(x => x.Occupants)
                .ThenInclude(x => x.User)
                    .ThenInclude(x => x!.UserProfile)
            .Include(x => x.Appendices)
                .ThenInclude(x => x.Changes)
            .Include(x => x.Signatures)
            .Include(x => x.Files);
    }

    private async Task<ContractRenderOptions> BuildSignedFileRenderOptionsAsync(
        RentalContract contract,
        ContractFileVariant fileVariant,
        CancellationToken cancellationToken)
    {
        var showFullDocumentNumbers = fileVariant == ContractFileVariant.Raw;
        return new ContractRenderOptions
        {
            ViewerMode = fileVariant.ToString(),
            ShowFullDocumentNumbers = showFullDocumentNumbers,
            VisibleOccupantIds = null,
            UserDocumentNumbersByUserId = showFullDocumentNumbers
                ? await GetDecryptedUserDocumentNumbersAsync(contract, cancellationToken)
                : new Dictionary<Guid, string?>(),
            OccupantDocumentNumbersByDocumentId = showFullDocumentNumbers
                ? GetDecryptedOccupantDocumentNumbers(contract)
                : new Dictionary<Guid, string?>()
        };
    }

    private async Task<ContractFile> EnsureContractFileVariantAsync(
        RentalContract contract,
        ContractFileVariant fileVariant,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingFile = contract.Files
            .Where(x => x.RentalContractAppendixId == null &&
                        x.FileVariant == fileVariant &&
                        x.StorageObjectKey.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (existingFile is not null)
        {
            return existingFile;
        }

        var renderOptions = await BuildSignedFileRenderOptionsAsync(contract, fileVariant, cancellationToken);
        var pdfBytes = contractPdfRenderer.RenderSignedRentalContract(contract, renderOptions);
        var objectKey = BuildObjectKey(contract, fileVariant, now);

        await using var stream = new MemoryStream(pdfBytes);
        var storageObjectKey = await privateStorageService.UploadAsync(
            stream,
            PdfContentType,
            objectKey,
            cancellationToken);

        var contractFile = new ContractFile
        {
            Id = Guid.NewGuid(),
            RentalContractId = contract.Id,
            StorageObjectKey = storageObjectKey,
            FileVariant = fileVariant,
            FileUrl = null,
            CreatedAt = now
        };

        context.ContractFiles.Add(contractFile);
        contract.Files.Add(contractFile);

        return contractFile;
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

    private static void EnsureCanAccess(Guid userId, RentalContract contract)
    {
        if (contract.Room.RoomingHouse.LandlordUserId == userId ||
            GetMainTenantUserIds(contract).Contains(userId) ||
            contract.Occupants.Any(x => x.UserId == userId))
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "B蘯｡n khﾃｴng cﾃｳ quy盻］ truy c蘯ｭp file h盻｣p ﾄ黛ｻ渡g nﾃy.",
            new { contract.Id });
    }

    private static IReadOnlyCollection<Guid> GetMainTenantUserIds(RentalContract contract)
    {
        var userIds = new HashSet<Guid> { contract.MainTenantUserId };

        foreach (var appendix in contract.Appendices
            .Where(x => x.Status == ContractAppendixStatus.Active)
            .OrderBy(x => x.ActivatedAt ?? x.UpdatedAt)
            .ThenBy(x => x.CreatedAt))
        {
            foreach (var change in appendix.Changes)
            {
                if (change.TargetType != ContractAppendixTargetType.Contract ||
                    change.ChangeType != ContractAppendixChangeType.Update ||
                    NormalizeFieldName(change.FieldName) != "maintenantuserid")
                {
                    continue;
                }

                var userId = ExtractUserId(change.NewValue);
                if (userId.HasValue)
                {
                    userIds.Add(userId.Value);
                }
            }
        }

        return userIds;
    }

    private static void EnsureCanViewFile(Guid userId, RentalContract contract, ContractFile file)
    {
        if (CanViewFile(userId, contract, file))
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "B蘯｡n khﾃｴng cﾃｳ quy盻］ truy c蘯ｭp file h盻｣p ﾄ黛ｻ渡g nﾃy.",
            new { contractId = contract.Id, fileId = file.Id });
    }

    private static bool CanViewFile(Guid userId, RentalContract contract, ContractFile file)
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
        var currentMainTenantUserId = contract.MainTenantUserId;

        foreach (var appendix in GetActiveAppendicesBefore(contract, targetAppendix))
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

        foreach (var appendix in contract.Appendices
            .Where(x => x.Status == ContractAppendixStatus.Active)
            .OrderBy(x => x.ActivatedAt ?? x.UpdatedAt)
            .ThenBy(x => x.CreatedAt))
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

    private static IEnumerable<ContractAppendix> GetActiveAppendicesBefore(
        RentalContract contract,
        ContractAppendix targetAppendix)
    {
        return contract.Appendices
            .Where(x => x.Status == ContractAppendixStatus.Active)
            .OrderBy(x => x.ActivatedAt ?? x.UpdatedAt)
            .ThenBy(x => x.CreatedAt)
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

    private static void EnsureCanGenerateSignedFile(RentalContract contract)
    {
        if (contract.Status != RentalContractStatus.Active)
        {
            throw new ConflictException(
                ErrorCodes.RentalContractInvalidStatus,
                "Ch盻・cﾃｳ th盻・t蘯｡o file PDF khi h盻｣p ﾄ黛ｻ渡g ﾄ妥｣ cﾃｳ hi盻㎡ l盻ｱc.",
                new { contract.Id, currentStatus = contract.Status.ToString() });
        }

        var hasLandlordSignature = contract.Signatures.Any(x => x.SignerRole == ContractSignerRole.Landlord);
        var hasTenantSignature = contract.Signatures.Any(x => x.SignerRole == ContractSignerRole.Tenant);

        if (hasLandlordSignature && hasTenantSignature)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RentalContractInvalidStatus,
            "H盻｣p ﾄ黛ｻ渡g ph蘯｣i cﾃｳ ﾄ黛ｻｧ ch盻ｯ kﾃｽ c盻ｧa ch盻ｧ tr盻・vﾃ ngﾆｰ盻拱 thuﾃｪ trﾆｰ盻嫩 khi t蘯｡o file PDF.",
            new { contract.Id });
    }

    private static string BuildObjectKey(
        RentalContract contract,
        ContractFileVariant fileVariant,
        DateTimeOffset now)
    {
        return $"contracts/{contract.Id:N}/signed-contract-{fileVariant.ToString().ToLowerInvariant()}-{now:yyyyMMddHHmmss}.pdf";
    }

    private static ContractFileResponse MapToResponse(ContractFile file)
    {
        return new ContractFileResponse
        {
            Id = file.Id,
            RentalContractId = file.RentalContractId,
            RentalContractAppendixId = file.RentalContractAppendixId,
            StorageObjectKey = file.StorageObjectKey,
            FileVariant = file.FileVariant.ToString(),
            FileUrl = file.FileUrl,
            CreatedAt = file.CreatedAt
        };
    }

    private static string GuessContentType(string objectKey)
    {
        return Path.GetExtension(objectKey).ToLowerInvariant() switch
        {
            ".pdf" => PdfContentType,
            ".html" => "text/html",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}

