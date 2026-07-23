using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;
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

namespace SmartRentalPlatform.Application.RentalContracts;

public class ContractFileService : IContractFileService
{
    private const string PdfContentType = "application/pdf";
    private static readonly TimeSpan ContractFileViewUrlTtl = TimeSpan.FromMinutes(5);

    private readonly IAppDbContext context;
    private readonly IContractPdfRenderer contractPdfRenderer;
    private readonly IMediaObjectKeyFactory mediaObjectKeyFactory;
    private readonly IMediaStorageService mediaStorageService;
    private readonly IMediaAssetService mediaAssetService;
    private readonly IMediaAccessService mediaAccessService;
    private readonly IContractDocumentModelFactory contractDocumentModelFactory;
    private readonly ISensitiveDataProtector sensitiveDataProtector;

    public ContractFileService(
        IAppDbContext context,
        IContractPdfRenderer contractPdfRenderer,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        IMediaStorageService mediaStorageService,
        IMediaAssetService mediaAssetService,
        IMediaAccessService mediaAccessService,
        IContractDocumentModelFactory contractDocumentModelFactory,
        ISensitiveDataProtector sensitiveDataProtector)
    {
        this.context = context;
        this.contractPdfRenderer = contractPdfRenderer;
        this.mediaObjectKeyFactory = mediaObjectKeyFactory;
        this.mediaStorageService = mediaStorageService;
        this.mediaAssetService = mediaAssetService;
        this.mediaAccessService = mediaAccessService;
        this.contractDocumentModelFactory = contractDocumentModelFactory;
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

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var (file, _) = await EnsureContractFilePurposeAsync(
            contract,
            ContractFilePurpose.SignedLegalDocument,
            now,
            cancellationToken);

        await EnsureContractFilePurposeAsync(
            contract,
            ContractFilePurpose.MaskedReference,
            now,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MapToResponse(file);
    }

    public async Task<ContractFileResponse?> EnsureMaskedContractFileAsync(
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
        EnsureCanGenerateReferenceFile(userId, contract);

        var now = DateTimeOffset.UtcNow;
        var (maskedFile, _) = await EnsureContractFilePurposeAsync(
            contract,
            ContractFilePurpose.MaskedReference,
            now,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return MapToResponse(maskedFile);
    }

    public async Task<(ContractFile File, IReadOnlyDictionary<string, SignatureZone> SignatureZones)?> CreateUnsignedContractPdfForESignAsync(
        Guid envelopeId,
        CancellationToken cancellationToken = default)
    {
        var envelope = await context.ContractSigningEnvelopes
            .FirstOrDefaultAsync(x => x.Id == envelopeId, cancellationToken);
        if (envelope?.RentalContractId is not Guid contractId ||
            envelope.RentalContractAppendixId.HasValue ||
            envelope.Status != SigningEnvelopeStatus.Draft)
        {
            return null;
        }

        var contract = await BaseContractQuery()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var (rawFile, signatureZones) = await EnsureContractFilePurposeAsync(
            contract,
            ContractFilePurpose.UnsignedForESign,
            now,
            cancellationToken,
            reuseExisting: false,
            signingEnvelope: envelope);

        await context.SaveChangesAsync(cancellationToken);
        return (rawFile, signatureZones);
    }

    public async Task<(ContractFile File, IReadOnlyDictionary<string, SignatureZone> SignatureZones)?> CreateUnsignedAppendixPdfForESignAsync(
        Guid envelopeId,
        CancellationToken cancellationToken = default)
    {
        var envelope = await context.ContractSigningEnvelopes
            .FirstOrDefaultAsync(x => x.Id == envelopeId, cancellationToken);
        if (envelope?.RentalContractAppendixId is not Guid appendixId ||
            envelope.RentalContractId is not Guid ||
            envelope.Status != SigningEnvelopeStatus.Draft)
        {
            return null;
        }

        var appendix = await BaseAppendixQuery()
            .FirstOrDefaultAsync(x => x.Id == appendixId, cancellationToken);

        if (appendix is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var (rawFile, signatureZones) = await EnsureAppendixFilePurposeAsync(
            appendix,
            ContractFilePurpose.UnsignedForESign,
            now,
            cancellationToken,
            reuseExisting: false,
            signingEnvelope: envelope);

        await context.SaveChangesAsync(cancellationToken);
        return (rawFile, signatureZones);
    }

    public async Task EnsureMaskedReferenceFileAsync(
        Guid contractId,
        Guid? appendixId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        if (appendixId.HasValue)
        {
            var appendix = await BaseAppendixQuery()
                .FirstOrDefaultAsync(
                    x => x.Id == appendixId.Value && x.RentalContractId == contractId,
                    cancellationToken);

            if (appendix is null)
            {
                throw new InvalidOperationException($"Appendix {appendixId.Value} was not found.");
            }

            await EnsureAppendixFilePurposeAsync(
                appendix,
                ContractFilePurpose.MaskedReference,
                now,
                cancellationToken);
        }
        else
        {
            var contract = await BaseContractQuery()
                .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

            if (contract is null)
            {
                throw new InvalidOperationException($"Contract {contractId} was not found.");
            }

            await EnsureContractFilePurposeAsync(
                contract,
                ContractFilePurpose.MaskedReference,
                now,
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ContractFile?> StoreProviderSignedPdfAsync(
        Guid envelopeId,
        Stream pdfStream,
        CancellationToken cancellationToken = default)
    {
        var envelope = await context.ContractSigningEnvelopes
            .FirstOrDefaultAsync(x => x.Id == envelopeId, cancellationToken);

        if (envelope is null)
        {
            return null;
        }

        var existingSignedFile = await context.ContractFiles
            .FirstOrDefaultAsync(
                x => x.ContractSigningEnvelopeId == envelopeId &&
                     x.Purpose == ContractFilePurpose.SignedLegalDocument,
                cancellationToken);

        if (existingSignedFile is not null)
        {
            return existingSignedFile;
        }

        var now = DateTimeOffset.UtcNow;
        var originalFileName = $"provider-signed-{envelope.Id:N}.pdf";

        await using var bufferedStream = new MemoryStream();
        await pdfStream.CopyToAsync(bufferedStream, cancellationToken);
        var pdfBytes = bufferedStream.ToArray();
        var contractFile = await CreateContractFileAsync(
            envelope.RentalContractId!.Value,
            envelope.RentalContractAppendixId,
            originalFileName,
            pdfBytes,
            ContractFilePurpose.SignedLegalDocument,
            envelope.RentalContractAppendixId.HasValue
                ? MediaScope.ContractAppendixPdf
                : MediaScope.ContractPdf,
            ownerUserId: null,
            now,
            envelope.Id,
            isLegallySigned: true,
            cancellationToken);
        
        envelope.SignedFileMediaAssetId = contractFile.MediaAssetId;
        envelope.SignedFileSha256Hash = contractFile.Sha256Hash;
        await context.SaveChangesAsync(cancellationToken);

        return contractFile;
    }

    public async Task<ContractFile?> StoreProviderEvidenceAsync(
        Guid envelopeId,
        Stream evidenceStream,
        CancellationToken cancellationToken = default)
    {
        var envelope = await context.ContractSigningEnvelopes
            .FirstOrDefaultAsync(x => x.Id == envelopeId, cancellationToken);

        if (envelope is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var originalFileName = $"provider-evidence-{envelope.Id:N}.pdf";

        await using var bufferedStream = new MemoryStream();
        await evidenceStream.CopyToAsync(bufferedStream, cancellationToken);
        var pdfBytes = bufferedStream.ToArray();
        var contractFile = await CreateContractFileAsync(
            envelope.RentalContractId!.Value,
            envelope.RentalContractAppendixId,
            originalFileName,
            pdfBytes,
            ContractFilePurpose.ProviderEvidence,
            envelope.RentalContractAppendixId.HasValue
                ? MediaScope.ContractAppendixPdf
                : MediaScope.ContractPdf,
            ownerUserId: null,
            now,
            envelope.Id,
            isLegallySigned: false,
            cancellationToken);

        envelope.EvidenceFileMediaAssetId = contractFile.MediaAssetId;
        await context.SaveChangesAsync(cancellationToken);

        return contractFile;
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
            .Include(x => x.Signatures)
            .Include(x => x.Files)
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return Array.Empty<ContractFileResponse>();
        }

        EnsureCanAccess(userId, contract);

        return contract.Files
            .Where(x => x.Purpose is not (
                ContractFilePurpose.Preview or
                ContractFilePurpose.UnsignedForESign or
                ContractFilePurpose.ProviderEvidence))
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
            .Include(x => x.Signatures)
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

        throw new InvalidOperationException("Contract file must be linked to a media asset.");
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
            .Include(x => x.Signatures)
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

        if (!file.MediaAssetId.HasValue)
        {
            throw new InvalidOperationException("Contract file must be linked to a media asset.");
        }

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
            return new ContractFileViewUrlResponse(BuildBackendDownloadRoute(contract.Id, file.Id), "backend-route");
        }
    }

    private IQueryable<RentalContract> BaseContractQuery()
    {
        return context.RentalContracts
            .Include(x => x.RoomDeposit)
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

    private IQueryable<ContractAppendix> BaseAppendixQuery()
    {
        return context.ContractAppendices
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.MainTenantUser)
                    .ThenInclude(x => x.UserProfile)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Room)
                    .ThenInclude(x => x.RoomingHouse)
                        .ThenInclude(x => x.Landlord)
                            .ThenInclude(x => x.UserProfile)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Occupants)
                    .ThenInclude(x => x.Documents)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Occupants)
                    .ThenInclude(x => x.User)
                        .ThenInclude(x => x!.UserProfile)
            .Include(x => x.Files)
            .Include(x => x.Signatures)
            .Include(x => x.Changes);
    }

    private async Task<ContractRenderOptions> BuildSignedFileRenderOptionsAsync(
        RentalContract contract,
        ContractFilePurpose Purpose,
        CancellationToken cancellationToken)
    {
        var showFullDocumentNumbers = Purpose is
            ContractFilePurpose.Preview or
            ContractFilePurpose.UnsignedForESign;
        return new ContractRenderOptions
        {
            ViewerMode = Purpose.ToString(),
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

    private async Task<(ContractFile File, IReadOnlyDictionary<string, SignatureZone> Zones)> EnsureContractFilePurposeAsync(
        RentalContract contract,
        ContractFilePurpose purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool reuseExisting = true,
        ContractSigningEnvelope? signingEnvelope = null)
    {
        var existingFile = reuseExisting
            ? contract.Files
                .Where(x => x.RentalContractAppendixId == null &&
                            x.Purpose == purpose &&
                            x.MediaAssetId.HasValue)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault()
            : null;

        if (existingFile is not null)
        {
            // Reused files don't carry fresh signature zones
            return (existingFile, new Dictionary<string, SignatureZone>());
        }

        var renderOptions = await BuildSignedFileRenderOptionsAsync(contract, purpose, cancellationToken);
        var buildMode = purpose == ContractFilePurpose.UnsignedForESign
            ? ContractDocumentBuildMode.FreezeNewSnapshot
            : ContractDocumentBuildMode.ExistingSnapshotOrLive;
        var document = await contractDocumentModelFactory.BuildAsync(
            contract,
            buildMode,
            signingEnvelope,
            cancellationToken);

        // Use the ESign-specific renderer (captures signature zone positions) when
        // generating a file for the signing workflow; otherwise use the plain renderer.
        byte[] pdfBytes;
        IReadOnlyDictionary<string, SignatureZone> signatureZones;
        if (purpose == ContractFilePurpose.UnsignedForESign)
        {
            var result = contractPdfRenderer.RenderRentalContractForESign(document, renderOptions);
            pdfBytes       = result.PdfBytes;
            signatureZones = result.SignatureZones;
        }
        else
        {
            pdfBytes       = contractPdfRenderer.RenderSignedRentalContract(document, renderOptions);
            signatureZones = new Dictionary<string, SignatureZone>();
        }
        
        var contractFile = await CreateContractFileAsync(
            contract.Id,
            rentalContractAppendixId: null,
            BuildContractFileName(contract, purpose, now),
            pdfBytes,
            purpose,
            MediaScope.ContractPdf,
            contract.Room.RoomingHouse.LandlordUserId,
            now,
            signingEnvelope?.Id,
            isLegallySigned: false,
            cancellationToken);

        contract.Files.Add(contractFile);

        if (signingEnvelope is not null)
        {
            signingEnvelope.UnsignedFileMediaAssetId = contractFile.MediaAssetId;
            signingEnvelope.UnsignedFileSha256Hash = contractFile.Sha256Hash;
        }

        return (contractFile, signatureZones);
    }

    private async Task<ContractRenderOptions> BuildSignedAppendixRenderOptionsAsync(
        ContractAppendix appendix,
        ContractFilePurpose purpose,
        CancellationToken cancellationToken)
    {
        var showFullDocumentNumbers = purpose is
            ContractFilePurpose.Preview or
            ContractFilePurpose.UnsignedForESign;
        return new ContractRenderOptions
        {
            ViewerMode = purpose.ToString(),
            ShowFullDocumentNumbers = showFullDocumentNumbers,
            VisibleOccupantIds = null,
            UserDocumentNumbersByUserId = showFullDocumentNumbers
                ? await GetDecryptedUserDocumentNumbersAsync(appendix.RentalContract, cancellationToken)
                : new Dictionary<Guid, string?>(),
            OccupantDocumentNumbersByDocumentId = showFullDocumentNumbers
                ? GetDecryptedOccupantDocumentNumbers(appendix.RentalContract)
                : new Dictionary<Guid, string?>()
        };
    }

    private async Task<(ContractFile File, IReadOnlyDictionary<string, SignatureZone> Zones)> EnsureAppendixFilePurposeAsync(
        ContractAppendix appendix,
        ContractFilePurpose purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool reuseExisting = true,
        ContractSigningEnvelope? signingEnvelope = null)
    {
        var existingFile = reuseExisting
            ? appendix.Files
                .Where(x => x.RentalContractAppendixId == appendix.Id &&
                            x.Purpose == purpose &&
                            x.MediaAssetId.HasValue)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault()
            : null;

        if (existingFile is not null)
        {
            return (existingFile, new Dictionary<string, SignatureZone>());
        }

        var renderOptions = await BuildSignedAppendixRenderOptionsAsync(appendix, purpose, cancellationToken);

        byte[] pdfBytes;
        IReadOnlyDictionary<string, SignatureZone> signatureZones;
        if (purpose == ContractFilePurpose.UnsignedForESign)
        {
            var result = contractPdfRenderer.RenderContractAppendixForESign(appendix, renderOptions);
            pdfBytes       = result.PdfBytes;
            signatureZones = result.SignatureZones;
        }
        else
        {
            pdfBytes       = contractPdfRenderer.RenderSignedContractAppendix(appendix, renderOptions);
            signatureZones = new Dictionary<string, SignatureZone>();
        }

        var contractFile = await CreateContractFileAsync(
            appendix.RentalContractId,
            appendix.Id,
            BuildAppendixFileName(appendix, purpose, now),
            pdfBytes,
            purpose,
            MediaScope.ContractAppendixPdf,
            appendix.CreatedByUserId,
            now,
            signingEnvelope?.Id,
            isLegallySigned: false,
            cancellationToken);

        appendix.Files.Add(contractFile);

        if (signingEnvelope is not null)
        {
            signingEnvelope.UnsignedFileMediaAssetId = contractFile.MediaAssetId;
            signingEnvelope.UnsignedFileSha256Hash = contractFile.Sha256Hash;
            signingEnvelope.DocumentTemplateVersion ??= "appendix-v1.0";
            signingEnvelope.DocumentPreparedAt ??= now;
        }

        return (contractFile, signatureZones);
    }

    private async Task<ContractFile> CreateContractFileAsync(
        Guid rentalContractId,
        Guid? rentalContractAppendixId,
        string originalFileName,
        byte[] pdfBytes,
        ContractFilePurpose purpose,
        MediaScope scope,
        Guid? ownerUserId,
        DateTimeOffset now,
        Guid? signingEnvelopeId,
        bool isLegallySigned,
        CancellationToken cancellationToken)
    {
        var contractFileId = Guid.NewGuid();
        var sha256Hash = ComputeSha256Hex(pdfBytes);
        var mediaObjectKey = mediaObjectKeyFactory.Create(scope, MediaVisibility.Private, originalFileName);
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

            var mediaAsset = await mediaAssetService.CreateAsync(
                new CreateMediaAssetRequest
                {
                    OwnerUserId = ownerUserId,
                    BucketName = storedObject.BucketName,
                    ObjectKey = storedObject.ObjectKey,
                    OriginalFileName = originalFileName,
                    StoredFileName = storedObject.StoredFileName,
                    ContentType = PdfContentType,
                    FileSize = pdfBytes.Length,
                    FileHash = sha256Hash,
                    Scope = scope,
                    Visibility = MediaVisibility.Private,
                    Status = MediaStatus.Linked,
                    LinkedEntityType = nameof(ContractFile),
                    LinkedEntityId = contractFileId
                },
                cancellationToken);

            var contractFile = new ContractFile
            {
                Id = contractFileId,
                RentalContractId = rentalContractId,
                RentalContractAppendixId = rentalContractAppendixId,
                MediaAssetId = mediaAsset.Id,
                Purpose = purpose,
                ContentType = PdfContentType,
                Sha256Hash = sha256Hash,
                IsLegallySigned = isLegallySigned,
                ContractSigningEnvelopeId = signingEnvelopeId,
                FileUrl = null,
                CreatedAt = now
            };

            context.ContractFiles.Add(contractFile);
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
        if (ContractDocumentAccessPolicy.HasContractRelationship(userId, contract))
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
        if (file.Purpose == ContractFilePurpose.ProviderEvidence)
        {
            return false;
        }

        if (file.Purpose == ContractFilePurpose.Preview)
        {
            return false;
        }

        if (file.Purpose == ContractFilePurpose.UnsignedForESign)
        {
            return ContractDocumentAccessPolicy.CanOpenUnsignedWorkflowFile(
                userId, 
                contract.Room.RoomingHouse.LandlordUserId, 
                GetCurrentMainTenantUserId(contract));
        }

        if (file.RentalContractAppendixId is null)
        {
            if (file.Purpose == ContractFilePurpose.MaskedReference)
            {
                return ContractDocumentAccessPolicy.CanViewMaskedContract(userId, contract);
            }

            return ContractDocumentAccessPolicy.CanViewFullContract(userId, contract);
        }

        var appendix = contract.Appendices.FirstOrDefault(x => x.Id == file.RentalContractAppendixId);
        if (appendix is null)
        {
            return false;
        }

        if (file.Purpose == ContractFilePurpose.MaskedReference)
        {
            return ContractDocumentAccessPolicy.CanViewMaskedAppendix(userId, contract, appendix);
        }

        return ContractDocumentAccessPolicy.CanViewFullAppendix(userId, appendix);
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
                x.Status == ContractAppendixStatus.Active)
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

    private static void EnsureCanGenerateReferenceFile(Guid userId, RentalContract contract)
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

    private static string BuildContractFileName(
        RentalContract contract,
        ContractFilePurpose purpose,
        DateTimeOffset now)
    {
        return $"contract-{contract.Id:N}-{purpose.ToString().ToLowerInvariant()}-{now:yyyyMMddHHmmss}.pdf";
    }

    private static string BuildAppendixFileName(
        ContractAppendix appendix,
        ContractFilePurpose purpose,
        DateTimeOffset now)
    {
        return $"appendix-{appendix.AppendixNumber}-{purpose.ToString().ToLowerInvariant()}-{now:yyyyMMddHHmmss}.pdf";
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
            Purpose = file.Purpose.ToString(),
            ViewUrl = file.MediaAssetId.HasValue
                ? PrivateMediaPathBuilder.Build(file.MediaAssetId.Value)
                : null,
            CreatedAt = file.CreatedAt
        };
    }

    private static string BuildBackendDownloadRoute(Guid contractId, Guid fileId)
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

}
