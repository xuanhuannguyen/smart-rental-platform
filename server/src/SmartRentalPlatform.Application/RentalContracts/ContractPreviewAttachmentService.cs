using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Kyc;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public sealed class ContractPreviewAttachmentService : IContractPreviewAttachmentService
{
    private const int MaxImageCount = 30;
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private const int MaxTotalImageBytes = 40 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext context;
    private readonly IPrivateStorageService privateStorageService;
    private readonly ISensitiveDataProtector sensitiveDataProtector;
    private readonly ILogger<ContractPreviewAttachmentService> logger;

    public ContractPreviewAttachmentService(
        IAppDbContext context,
        IPrivateStorageService privateStorageService,
        ISensitiveDataProtector sensitiveDataProtector,
        ILogger<ContractPreviewAttachmentService> logger)
    {
        this.context = context;
        this.privateStorageService = privateStorageService;
        this.sensitiveDataProtector = sensitiveDataProtector;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<ContractReviewAttachment>> LoadForContractAsync(
        Guid viewerUserId,
        RentalContract contract,
        CancellationToken cancellationToken = default)
    {
        EnsureLandlord(viewerUserId, contract);
        logger.LogInformation(
            "Sensitive contract review preview accessed. ViewerUserId={ViewerUserId}, ContractId={ContractId}",
            viewerUserId,
            contract.Id);

        var attachments = new List<ContractReviewAttachment>();
        var budget = new LoadBudget();

        foreach (var occupant in contract.Occupants.OrderBy(x => x.CreatedAt))
        {
            await AddOccupantAttachmentsAsync(
                attachments,
                occupant,
                ResolveOccupantRole(contract, occupant),
                budget,
                cancellationToken);
        }

        if (!contract.Occupants.Any(x => x.UserId == contract.MainTenantUserId))
        {
            await AddKycAttachmentAsync(
                attachments,
                contract.MainTenantUserId,
                contract.MainTenantUser.UserProfile?.FullName ?? contract.MainTenantUser.DisplayName,
                "Người thuê chính",
                budget,
                cancellationToken);
        }

        return attachments;
    }

    public async Task<IReadOnlyList<ContractReviewAttachment>> LoadForAppendixAsync(
        Guid viewerUserId,
        ContractAppendix appendix,
        CancellationToken cancellationToken = default)
    {
        var contract = appendix.RentalContract;
        EnsureLandlord(viewerUserId, contract);
        logger.LogInformation(
            "Sensitive appendix review preview accessed. ViewerUserId={ViewerUserId}, ContractId={ContractId}, AppendixId={AppendixId}",
            viewerUserId,
            contract.Id,
            appendix.Id);

        var attachments = new List<ContractReviewAttachment>();
        var budget = new LoadBudget();
        var processedOccupants = new HashSet<Guid>();

        foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
        {
            if (change.TargetType == ContractAppendixTargetType.ContractOccupant)
            {
                if (change.ChangeType == ContractAppendixChangeType.Add)
                {
                    var request = TryParseOccupantRequest(change.NewValue);
                    if (request is not null)
                    {
                        await AddOccupantRequestAttachmentAsync(
                            attachments,
                            request,
                            budget,
                            cancellationToken);
                    }

                    continue;
                }

                if (change.TargetId.HasValue && processedOccupants.Add(change.TargetId.Value))
                {
                    var occupant = contract.Occupants.FirstOrDefault(x => x.Id == change.TargetId.Value);
                    if (occupant is not null)
                    {
                        await AddOccupantAttachmentsAsync(
                            attachments,
                            occupant,
                            ResolveOccupantRole(contract, occupant),
                            budget,
                            cancellationToken);
                    }
                }

                continue;
            }

            if (IsMainTenantUserIdChange(change))
            {
                var newMainTenantUserId = ExtractUserId(change.NewValue);
                if (!newMainTenantUserId.HasValue)
                {
                    continue;
                }

                var occupant = contract.Occupants.FirstOrDefault(x => x.UserId == newMainTenantUserId.Value);
                if (occupant is not null && processedOccupants.Add(occupant.Id))
                {
                    await AddOccupantAttachmentsAsync(
                        attachments,
                        occupant,
                        "Người thuê chính mới",
                        budget,
                        cancellationToken);
                }
                else
                {
                    var user = await context.Users
                        .AsNoTracking()
                        .Include(x => x.UserProfile)
                        .FirstOrDefaultAsync(x => x.Id == newMainTenantUserId.Value, cancellationToken);

                    if (user is not null)
                    {
                        await AddKycAttachmentAsync(
                            attachments,
                            user.Id,
                            user.UserProfile?.FullName ?? user.DisplayName,
                            "Người thuê chính mới",
                            budget,
                            cancellationToken);
                    }
                }
            }
        }

        return attachments;
    }

    private async Task AddOccupantAttachmentsAsync(
        ICollection<ContractReviewAttachment> attachments,
        ContractOccupant occupant,
        string role,
        LoadBudget budget,
        CancellationToken cancellationToken)
    {
        if (occupant.Documents.Count == 0 && occupant.UserId.HasValue)
        {
            await AddKycAttachmentAsync(
                attachments,
                occupant.UserId.Value,
                occupant.FullName,
                role,
                budget,
                cancellationToken);
            return;
        }

        foreach (var document in occupant.Documents.OrderBy(x => x.UploadedAt))
        {
            attachments.Add(new ContractReviewAttachment(
                occupant.FullName,
                role,
                document.DocumentType,
                DecryptDocumentNumber(document.DocumentNumberEncrypted) ?? document.DocumentNumberMasked,
                await LoadDocumentImagesAsync(
                    document.FrontImageObjectKey,
                    document.BackImageObjectKey,
                    document.ExtraImageObjectKey,
                    "Ảnh bổ sung",
                    budget,
                    cancellationToken)));
        }
    }

    private async Task AddOccupantRequestAttachmentAsync(
        ICollection<ContractReviewAttachment> attachments,
        ContractOccupantRequest request,
        LoadBudget budget,
        CancellationToken cancellationToken)
    {
        if (request.Document is null)
        {
            return;
        }

        attachments.Add(new ContractReviewAttachment(
            request.FullName?.Trim() ?? request.Email?.Trim() ?? "Người ở mới",
            request.RelationshipToMainTenant?.Trim() ?? "Người ở được thêm",
            request.Document.DocumentType,
            request.Document.DocumentNumber,
            await LoadDocumentImagesAsync(
                request.Document.FrontImageObjectKey,
                request.Document.BackImageObjectKey,
                request.Document.ExtraImageObjectKey,
                "Ảnh bổ sung",
                budget,
                cancellationToken)));
    }

    private async Task AddKycAttachmentAsync(
        ICollection<ContractReviewAttachment> attachments,
        Guid userId,
        string personName,
        string role,
        LoadBudget budget,
        CancellationToken cancellationToken)
    {
        var kyc = await context.KycVerifications
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == KycVerificationStatus.Approved)
            .OrderByDescending(x => x.ReviewedAt ?? x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (kyc is null)
        {
            attachments.Add(new ContractReviewAttachment(
                personName,
                role,
                "Hồ sơ định danh",
                null,
                MissingDocumentImages("Chưa có hồ sơ giấy tờ được duyệt.")));
            return;
        }

        attachments.Add(new ContractReviewAttachment(
            personName,
            role,
            kyc.DocumentType.ToString(),
            DecryptDocumentNumber(kyc.DocumentNumberEncrypted) ?? kyc.OcrCitizenIdMasked,
            await LoadDocumentImagesAsync(
                kyc.FrontImageObjectKey,
                kyc.BackImageObjectKey,
                null,
                "Ảnh bổ sung",
                budget,
                cancellationToken)));
    }

    private async Task<IReadOnlyList<ContractReviewImage>> LoadDocumentImagesAsync(
        string? frontObjectKey,
        string? backObjectKey,
        string? extraObjectKey,
        string extraLabel,
        LoadBudget budget,
        CancellationToken cancellationToken)
    {
        return new[]
        {
            await LoadImageAsync("Mặt trước", frontObjectKey, "Chưa cung cấp mặt trước.", budget, cancellationToken),
            await LoadImageAsync("Mặt sau", backObjectKey, "Chưa cung cấp mặt sau.", budget, cancellationToken),
            await LoadImageAsync(extraLabel, extraObjectKey, "Không có ảnh bổ sung.", budget, cancellationToken)
        };
    }

    private async Task<ContractReviewImage> LoadImageAsync(
        string label,
        string? objectKey,
        string missingMessage,
        LoadBudget budget,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return new ContractReviewImage(label, null, missingMessage);
        }

        if (budget.ImageCount >= MaxImageCount || budget.TotalBytes >= MaxTotalImageBytes)
        {
            return new ContractReviewImage(label, null, "Đã vượt giới hạn ảnh của bản xem trước.");
        }

        try
        {
            await using var source = await privateStorageService.OpenReadAsync(objectKey, cancellationToken);
            await using var destination = new MemoryStream();
            var buffer = new byte[81920];
            var imageBytes = 0;

            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                imageBytes += read;
                if (imageBytes > MaxImageBytes || budget.TotalBytes + imageBytes > MaxTotalImageBytes)
                {
                    return new ContractReviewImage(label, null, "Ảnh vượt quá giới hạn dung lượng cho phép.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            var bytes = destination.ToArray();
            if (!IsSupportedImage(bytes))
            {
                return new ContractReviewImage(label, null, "Định dạng ảnh không được hỗ trợ.");
            }

            budget.ImageCount++;
            budget.TotalBytes += bytes.Length;
            return new ContractReviewImage(label, bytes, missingMessage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                ex,
                "Could not load a sensitive document image for contract preview. Label={Label}",
                label);
            return new ContractReviewImage(label, null, "Không thể tải ảnh giấy tờ.");
        }
    }

    private string? DecryptDocumentNumber(string? encryptedValue)
    {
        if (string.IsNullOrWhiteSpace(encryptedValue))
        {
            return null;
        }

        try
        {
            return sensitiveDataProtector.Decrypt(encryptedValue);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static IReadOnlyList<ContractReviewImage> MissingDocumentImages(string message)
    {
        return new[]
        {
            new ContractReviewImage("Mặt trước", null, message),
            new ContractReviewImage("Mặt sau", null, message),
            new ContractReviewImage("Ảnh bổ sung", null, message)
        };
    }

    private static bool IsSupportedImage(byte[] bytes)
    {
        var isJpeg = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        var isPng = bytes.Length >= 8 &&
                    bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                    bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        var isWebP = bytes.Length >= 12 &&
                     bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                     bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P';

        return isJpeg || isPng || isWebP;
    }

    private static string ResolveOccupantRole(RentalContract contract, ContractOccupant occupant)
    {
        return occupant.UserId == contract.MainTenantUserId
            ? "Người thuê chính"
            : occupant.RelationshipToMainTenant ?? "Người ở cùng";
    }

    private static void EnsureLandlord(Guid viewerUserId, RentalContract contract)
    {
        if (contract.Room.RoomingHouse.LandlordUserId != viewerUserId)
        {
            throw new ForbiddenException(
                ErrorCodes.RentalContractForbidden,
                "Chỉ chủ trọ được xem hồ sơ giấy tờ đối chiếu.",
                new { contract.Id });
        }
    }

    private static bool IsMainTenantUserIdChange(ContractAppendixChange change)
    {
        return change.TargetType == ContractAppendixTargetType.Contract &&
               change.ChangeType == ContractAppendixChangeType.Update &&
               string.Equals(
                   change.FieldName?.Replace("_", string.Empty, StringComparison.Ordinal),
                   "maintenantuserid",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static Guid? ExtractUserId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('"');
        return Guid.TryParse(trimmed, out var userId) ? userId : null;
    }

    private static ContractOccupantRequest? TryParseOccupantRequest(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var json = value.Trim();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                json = document.RootElement.GetString() ?? string.Empty;
            }

            return JsonSerializer.Deserialize<ContractOccupantRequest>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class LoadBudget
    {
        public int ImageCount { get; set; }

        public int TotalBytes { get; set; }
    }
}
