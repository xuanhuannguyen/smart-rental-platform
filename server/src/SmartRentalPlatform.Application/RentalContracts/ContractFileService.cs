using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using System.Security.Cryptography;
using System.Text.Json;

namespace SmartRentalPlatform.Application.RentalContracts;

public class ContractFileService : IContractFileService
{
    private const string PdfContentType = "application/pdf";
    private static readonly TimeSpan ContractFileViewUrlTtl = TimeSpan.FromMinutes(5);

    private readonly IAppDbContext context;
    private readonly IContractPdfRenderer contractPdfRenderer;
    private readonly IPrivateStorageService privateStorageService;
    private readonly IMediaObjectKeyFactory mediaObjectKeyFactory;
    private readonly IMediaStorageService mediaStorageService;
    private readonly IMediaAssetService mediaAssetService;
    private readonly IMediaAccessService mediaAccessService;
    private readonly ISensitiveDataProtector sensitiveDataProtector;

    public ContractFileService(
        IAppDbContext context,
        IContractPdfRenderer contractPdfRenderer,
        IPrivateStorageService privateStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        IMediaStorageService mediaStorageService,
        IMediaAssetService mediaAssetService,
        IMediaAccessService mediaAccessService,
        ISensitiveDataProtector sensitiveDataProtector)
    {
        this.context = context;
        this.contractPdfRenderer = contractPdfRenderer;
        this.privateStorageService = privateStorageService;
        this.mediaObjectKeyFactory = mediaObjectKeyFactory;
        this.mediaStorageService = mediaStorageService;
        this.mediaAssetService = mediaAssetService;
        this.mediaAccessService = mediaAccessService;
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
        EnsureCanGenerateSignedFile(userId, contract);

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var rawFile = await EnsureContractFileVariantAsync(
            contract,
            ContractFileVariant.Raw,
            userId,
            now,
            cancellationToken);

        await EnsureContractFileVariantAsync(
            contract,
            ContractFileVariant.Masked,
            userId,
            now,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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
            .Include(x => x.Appendices)
                .ThenInclude(x => x.Signatures)
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
            .Include(x => x.Appendices)
                .ThenInclude(x => x.Signatures)
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

        if (file.MediaAssetId.HasValue)
        {
            var mediaAccess = await mediaAccessService.OpenReadAsync(file.MediaAssetId.Value, userId, cancellationToken);
            return (mediaAccess.Stream, mediaAccess.ContentType, mediaAccess.DownloadFileName);
        }

        var stream = await privateStorageService.OpenReadAsync(file.StorageObjectKey, cancellationToken);
        return (stream, GuessContentType(file.StorageObjectKey), Path.GetFileName(file.StorageObjectKey));
    }

    public async Task<ContractFileViewUrlResponse?> GetFileViewUrlAsync(
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
            .Include(x => x.Appendices)
                .ThenInclude(x => x.Signatures)
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

        if (file.MediaAssetId.HasValue)
        {
            try
            {
                var url = await mediaAccessService.GetDownloadUrlAsync(
                    file.MediaAssetId.Value,
                    ContractFileViewUrlTtl,
                    userId,
                    cancellationToken,
                    BuildAuditContext(contract.Id, file.Id, "GenerateViewUrl"));

                return new ContractFileViewUrlResponse(url, "signed-url");
            }
            catch (NotSupportedException)
            {
                // Fall back to the authenticated contract file route when presigned access is unavailable.
            }
        }

        return new ContractFileViewUrlResponse(BuildLegacyDownloadRoute(contract.Id, file.Id), "backend-route");
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
        Guid userId,
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
        var originalFileName = BuildLegacyFileName(contract, fileVariant, now);
        var mediaObjectKey = mediaObjectKeyFactory.Create(MediaScope.ContractPdf, MediaVisibility.Private, originalFileName);

        MediaStoredObjectResult? storedObject = null;

        try
        {
            await using var stream = new MemoryStream(pdfBytes);
            storedObject = await mediaStorageService.UploadAsync(
                new MediaUploadRequest
                {
                    Content = stream,
                    OriginalFileName = originalFileName,
                    ContentType = PdfContentType,
                    FileSize = pdfBytes.Length,
                    ObjectKey = mediaObjectKey.ObjectKey,
                    Visibility = MediaVisibility.Private
                },
                cancellationToken);

            var contractFileId = Guid.NewGuid();
            var contractFile = new ContractFile
            {
                Id = contractFileId,
                RentalContractId = contract.Id,
                StorageObjectKey = storedObject.ObjectKey,
                FileVariant = fileVariant,
                FileUrl = null,
                CreatedAt = now
            };

            var mediaAsset = await mediaAssetService.CreateAsync(
                new CreateMediaAssetRequest
                {
                    OwnerUserId = userId,
                    BucketName = storedObject.BucketName,
                    ObjectKey = storedObject.ObjectKey,
                    OriginalFileName = originalFileName,
                    StoredFileName = storedObject.StoredFileName,
                    ContentType = PdfContentType,
                    FileSize = pdfBytes.Length,
                    FileHash = ComputeSha256Hex(pdfBytes),
                    Scope = MediaScope.ContractPdf,
                    Visibility = MediaVisibility.Private,
                    Status = MediaStatus.Linked,
                    LinkedEntityType = nameof(ContractFile),
                    LinkedEntityId = contractFileId
                },
                cancellationToken);

            contractFile.MediaAssetId = mediaAsset.Id;
            context.ContractFiles.Add(contractFile);
            contract.Files.Add(contractFile);

            return contractFile;
        }
        catch
        {
            if (storedObject is not null)
            {
                await mediaStorageService.DeleteAsync(storedObject.ObjectKey, cancellationToken);
            }

            throw;
        }
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
            GetCurrentMainTenantUserId(contract) == userId ||
            GetMainTenantUserIds(contract).Contains(userId) ||
            contract.Occupants.Any(x => x.UserId == userId))
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "Bạn không có quyền truy cập file hợp đồng này.",
            new { contract.Id });
    }

    private static void EnsureCanViewFile(Guid userId, RentalContract contract, ContractFile file)
    {
        if (CanViewFile(userId, contract, file))
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "Bạn không có quyền truy cập file hợp đồng này.",
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

    private static void EnsureCanGenerateSignedFile(Guid userId, RentalContract contract)
    {
        if (contract.Room.RoomingHouse.LandlordUserId != userId &&
            GetCurrentMainTenantUserId(contract) != userId)
        {
            throw new ForbiddenException(
                ErrorCodes.RentalContractForbidden,
                "Chỉ chủ trọ hoặc người thuê chính hiện tại được tạo file PDF hợp đồng.",
                new { contract.Id });
        }

        if (contract.Status != RentalContractStatus.Active)
        {
            throw new ConflictException(
                ErrorCodes.RentalContractInvalidStatus,
                "Chỉ có thể tạo file PDF khi hợp đồng đã có hiệu lực.",
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
            "Hợp đồng phải có đủ chữ ký của chủ trọ và người thuê trước khi tạo file PDF.",
            new { contract.Id });
    }

    private static string BuildLegacyFileName(
        RentalContract contract,
        ContractFileVariant fileVariant,
        DateTimeOffset now)
    {
        return $"signed-contract-{contract.Id:N}-{fileVariant.ToString().ToLowerInvariant()}-{now:yyyyMMddHHmmss}.pdf";
    }

    private static string ComputeSha256Hex(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private static ContractFileResponse MapToResponse(ContractFile file)
    {
        return new ContractFileResponse
        {
            Id = file.Id,
            RentalContractId = file.RentalContractId,
            RentalContractAppendixId = file.RentalContractAppendixId,
            MediaAssetId = file.MediaAssetId,
            StorageObjectKey = file.StorageObjectKey,
            FileVariant = file.FileVariant.ToString(),
            FileUrl = file.FileUrl,
            ViewUrl = file.MediaAssetId.HasValue
                ? PrivateMediaPathBuilder.Build(file.MediaAssetId.Value)
                : null,
            CreatedAt = file.CreatedAt
        };
    }

    private static string BuildLegacyDownloadRoute(Guid contractId, Guid fileId)
    {
        return $"/api/contracts/{contractId:D}/files/{fileId:D}/download";
    }

    private static MediaAuditContext BuildAuditContext(Guid contractId, Guid fileId, string action)
    {
        return new MediaAuditContext
        {
            Action = action,
            MetadataJson = JsonSerializer.Serialize(new
            {
                contractId,
                fileId,
                source = nameof(ContractFileService)
            })
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
