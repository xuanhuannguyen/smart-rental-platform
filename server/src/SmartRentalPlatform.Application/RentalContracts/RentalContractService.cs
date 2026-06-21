using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using SmartRentalPlatform.Application.Billing;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Payments;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public class RentalContractService : IRentalContractService
{
	private sealed record ContractPreviewViewerAccess(string ViewerMode, bool ShowFullDocumentNumbers, IReadOnlyCollection<Guid>? VisibleOccupantIds);

	private sealed record VerifiedOccupantAccount(Guid UserId, string FullName, string? PhoneNumber, DateOnly DateOfBirth);

	private static readonly TimeSpan TenantSignatureTtl = TimeSpan.FromHours(48);

	private const int LandlordSignatureMinimumStartOffsetDays = 2;

	private readonly IAppDbContext context;

	private readonly IHashService hashService;

	private readonly IContractPdfRenderer contractPdfRenderer;

	private readonly IContractSignatureOtpService contractSignatureOtpService;

	private readonly IContractFileService contractFileService;

	private readonly ISensitiveDataProtector sensitiveDataProtector;

	private readonly IBillingService billingService;

	private readonly IWalletService walletService;

	private readonly IPaymentRowLockService rowLockService;

	public RentalContractService(IAppDbContext context, IHashService hashService, IContractPdfRenderer contractPdfRenderer, IContractSignatureOtpService contractSignatureOtpService, IContractFileService contractFileService, ISensitiveDataProtector sensitiveDataProtector, IBillingService billingService, IWalletService walletService, IPaymentRowLockService rowLockService)
	{
		this.context = context;
		this.hashService = hashService;
		this.contractPdfRenderer = contractPdfRenderer;
		this.contractSignatureOtpService = contractSignatureOtpService;
		this.contractFileService = contractFileService;
		this.sensitiveDataProtector = sensitiveDataProtector;
		this.billingService = billingService;
		this.walletService = walletService;
		this.rowLockService = rowLockService;
	}

	public async Task<ContractDetailResponse?> GetByIdAsync(Guid userId, Guid contractId, CancellationToken cancellationToken = default(CancellationToken))
	{
		RentalContract contract = await BaseQuery().AsNoTracking().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		EnsureCanView(userId, contract);
		ContractDetailResponse response = MapToDetailResponse(contract);
		response.IsAwaitingFinalInvoice = await IsAwaitingFinalInvoiceAsync(contract, cancellationToken);
		return response;
	}

	public async Task<ContractDetailResponse?> GetActiveContractByRoomIdAsync(Guid landlordUserId, Guid roomId, CancellationToken cancellationToken = default(CancellationToken))
	{
		RentalContract contract = await BaseQuery().AsNoTracking().FirstOrDefaultAsync((RentalContract x) => x.RoomId == roomId && x.DeletedAt == null && (int)x.Status == 9 && x.Room.RoomingHouse.LandlordUserId == landlordUserId, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		EnsureLandlord(landlordUserId, contract);
		return MapToDetailResponse(contract);
	}

	public async Task<IReadOnlyCollection<ContractOccupantResponse>?> GetActiveTenantsByRoomIdAsync(Guid landlordUserId, Guid roomId, CancellationToken cancellationToken = default(CancellationToken))
	{
		RentalContract contract = await BaseQuery().AsNoTracking().FirstOrDefaultAsync((RentalContract x) => x.RoomId == roomId && x.DeletedAt == null && (int)x.Status == 9 && x.Room.RoomingHouse.LandlordUserId == landlordUserId, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		EnsureLandlord(landlordUserId, contract);
		ResolveCurrentContractTerms(contract);
		return contract.Occupants.OrderBy((ContractOccupant x) => x.CreatedAt).Select(MapOccupantToResponse).ToList();
	}

	public async Task<IReadOnlyCollection<ContractHistoryItemResponse>> GetMyHistoryAsync(Guid tenantUserId, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<RentalContract> contracts = await (from x in BaseQuery().AsNoTracking()
			where x.DeletedAt == null && x.ActivatedAt != null && ((int)x.Status == 9 || (int)x.Status == 8 || (int)x.Status == 7) && (x.MainTenantUserId == tenantUserId || x.Occupants.Any((ContractOccupant occupant) => occupant.UserId == tenantUserId))
			orderby x.ActivatedAt ?? x.UpdatedAt descending, x.CreatedAt descending
			select x).ToListAsync(cancellationToken);
		HashSet<Guid> awaitingIds = await GetAwaitingFinalInvoiceContractIdsAsync(contracts, cancellationToken);
		return contracts.Select(contract =>
		{
			ContractHistoryItemResponse response = MapToHistoryItemResponse(contract, tenantUserId);
			response.IsAwaitingFinalInvoice = awaitingIds.Contains(contract.Id);
			return response;
		}).ToList();
	}

	public async Task<IReadOnlyCollection<ContractHistoryItemResponse>> GetLandlordContractsAsync(Guid landlordUserId, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<RentalContract> contracts = await (from x in BaseQuery().AsNoTracking()
			where x.DeletedAt == null && x.ActivatedAt != null && ((int)x.Status == 9 || (int)x.Status == 8 || (int)x.Status == 7) && x.Room.RoomingHouse.LandlordUserId == landlordUserId
			orderby x.ActivatedAt ?? x.UpdatedAt descending, x.CreatedAt descending
			select x).ToListAsync(cancellationToken);
		HashSet<Guid> awaitingIds = await GetAwaitingFinalInvoiceContractIdsAsync(contracts, cancellationToken);
		return contracts.Select(contract =>
		{
			ContractHistoryItemResponse response = MapToHistoryItemResponse(contract, landlordUserId);
			response.IsAwaitingFinalInvoice = awaitingIds.Contains(contract.Id);
			return response;
		}).ToList();
	}

	public async Task<ContractPreviewPdfResult?> GetPreviewPdfAsync(Guid userId, Guid contractId, CancellationToken cancellationToken = default(CancellationToken))
	{
		RentalContract contract = await BaseQuery().AsNoTracking().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		EnsureContractCanPreview(contract);
		ContractPreviewViewerAccess viewerAccess = ResolvePreviewViewerAccess(userId, contract);
		if ((object)viewerAccess == null)
		{
			throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Bạn không có quyền xem bản xem trước hợp đồng này.", new { contract.Id });
		}
		ContractRenderOptions renderOptions = await BuildPreviewRenderOptionsAsync(contract, viewerAccess, cancellationToken);
		byte[] pdfBytes = contractPdfRenderer.RenderSignedRentalContract(contract, renderOptions);
		string fileName = "contract-preview-" + contract.ContractNumber + ".pdf";
		return new ContractPreviewPdfResult(pdfBytes, "application/pdf", fileName);
	}

	public async Task<ContractDetailResponse?> SubmitOccupantsAsync(Guid tenantUserId, Guid contractId, SubmitContractOccupantsRequest request, CancellationToken cancellationToken = default(CancellationToken))
	{
		RentalContract contract = await BaseQuery().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		EnsureMainTenant(tenantUserId, contract);
		EnsureCanSubmitOccupants(contract);
		ValidateOccupantsRequest(contract.MainTenantUser.Email, request, GetSnapshotMaxOccupants(contract));
		Dictionary<string, VerifiedOccupantAccount> verifiedAccounts = await ValidateOccupantAccountsAsync(request, cancellationToken);
		ContractDetailResponse result;
		await using (IAppDbContextTransaction transaction = await context.BeginTransactionAsync(cancellationToken))
		{
			try
			{
				DateTimeOffset now = DateTimeOffset.UtcNow;
				DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
				context.ContractOccupantDocuments.RemoveRange(contract.Occupants.SelectMany((ContractOccupant x) => x.Documents));
				context.ContractOccupants.RemoveRange(contract.Occupants);
				Dictionary<string, ContractOccupant> createdByClientReference = new Dictionary<string, ContractOccupant>(StringComparer.OrdinalIgnoreCase);
				List<(ContractOccupant Occupant, string GuardianClientReferenceId)> pendingGuardianLinks = new List<(ContractOccupant, string)>();
				foreach (ContractOccupantRequest occupantRequest in request.Occupants)
				{
					string emailKey = occupantRequest.Email?.Trim().ToLowerInvariant();
					VerifiedOccupantAccount account;
					VerifiedOccupantAccount verifiedAccount = ((!string.IsNullOrEmpty(emailKey) && verifiedAccounts.TryGetValue(emailKey, out account)) ? account : null);
					ContractOccupant occupant = new ContractOccupant
					{
						Id = Guid.NewGuid(),
						RentalContractId = contract.Id,
						UserId = verifiedAccount?.UserId,
						FullName = (verifiedAccount?.FullName ?? occupantRequest.FullName.Trim()),
						PhoneNumber = (verifiedAccount?.PhoneNumber ?? NormalizeOptionalText(occupantRequest.PhoneNumber)),
						DateOfBirth = (verifiedAccount?.DateOfBirth ?? occupantRequest.DateOfBirth.Value),
						RelationshipToMainTenant = NormalizeOptionalText(occupantRequest.RelationshipToMainTenant),
						MoveInDate = occupantRequest.MoveInDate,
						MoveOutDate = occupantRequest.MoveOutDate,
						Status = ResolveMoveInStatus(occupantRequest.MoveInDate, today),
						CreatedAt = now,
						UpdatedAt = now
					};
					if (occupantRequest.Document != null)
					{
						ContractOccupantDocumentRequest documentRequest = occupantRequest.Document;
						var newDoc = new ContractOccupantDocument
						{
							Id = Guid.NewGuid(),
							RentalContractOccupantId = occupant.Id,
							DocumentType = documentRequest.DocumentType.Trim(),
							DocumentNumberMasked = MaskDocumentNumber(documentRequest.DocumentNumber),
							DocumentNumberHash = HashDocumentNumber(documentRequest.DocumentNumber),
							DocumentNumberEncrypted = EncryptDocumentNumber(documentRequest.DocumentNumber),
							FrontImageObjectKey = documentRequest.FrontImageObjectKey.Trim(),
							BackImageObjectKey = NormalizeOptionalText(documentRequest.BackImageObjectKey),
							ExtraImageObjectKey = NormalizeOptionalText(documentRequest.ExtraImageObjectKey),
							UploadedAt = now,
							CreatedAt = now,
							UpdatedAt = now
						};
						occupant.Documents.Add(newDoc);
						context.ContractOccupantDocuments.Add(newDoc);
					}
					if (!string.IsNullOrWhiteSpace(occupantRequest.ClientReferenceId))
					{
						createdByClientReference[occupantRequest.ClientReferenceId.Trim()] = occupant;
					}
					if (!string.IsNullOrWhiteSpace(occupantRequest.GuardianClientReferenceId))
					{
						pendingGuardianLinks.Add((occupant, occupantRequest.GuardianClientReferenceId.Trim()));
					}
					context.ContractOccupants.Add(occupant);
					account = null;
				}
				foreach (var (occupant2, guardianClientReferenceId) in pendingGuardianLinks)
				{
					if (!createdByClientReference.TryGetValue(guardianClientReferenceId, out ContractOccupant guardian))
					{
						throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Không tìm thấy người bảo hộ trong danh sách người ở.", new { guardianClientReferenceId });
					}
					occupant2.GuardianOccupantId = guardian.Id;
					guardian = null;
				}
				contract.Status = RentalContractStatus.PendingLandlordSignature;
				contract.StatusReason = null;
				contract.UpdatedAt = now;
				await context.SaveChangesAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
				result = await GetByIdAsync(tenantUserId, contract.Id, cancellationToken);
			}
			catch
			{
				await transaction.RollbackAsync(cancellationToken);
				throw;
			}
		}
		return result;
	}

	public async Task<ContractDetailResponse?> UpdateTermsAsync(Guid landlordUserId, Guid contractId, UpdateContractTermsRequest request, CancellationToken cancellationToken = default(CancellationToken))
	{
		ValidateTermsRequest(request);
		RentalContract contract = await BaseQuery().AsNoTracking().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		EnsureLandlord(landlordUserId, contract);
		EnsureStatus(contract, RentalContractStatus.TenantRevisionRequested);
		RentalPolicy rentalPolicy = await context.RentalPolicies.AsNoTracking().FirstOrDefaultAsync((RentalPolicy x) => x.RoomingHouseId == contract.Room.RoomingHouseId && x.IsActive, cancellationToken);
		if (rentalPolicy == null)
		{
			throw new ConflictException("RENTAL_POLICY_REQUIRED", "Khu trọ chưa cấu hình chính sách thuê.", new { contract.Room.RoomingHouseId });
		}
		ValidateTermsDuration(request, rentalPolicy.MinRentalMonths, rentalPolicy.MaxRentalMonths);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		if (await context.RentalContracts.Where((RentalContract x) => x.Id == contractId && x.DeletedAt == null && (int)x.Status == 5).ExecuteUpdateAsync(delegate(UpdateSettersBuilder<RentalContract> setters)
		{
			setters.SetProperty((RentalContract x) => x.StartDate, request.StartDate)
				.SetProperty((RentalContract x) => x.EndDate, request.EndDate)
				.SetProperty((RentalContract x) => x.PaymentDay, request.PaymentDay)
				.SetProperty((RentalContract x) => x.Status, RentalContractStatus.PendingLandlordSignature)
				.SetProperty((RentalContract x) => x.StatusReason, (string?)null)
				.SetProperty((RentalContract x) => x.UpdatedAt, now);
		}, cancellationToken) == 0)
		{
			throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Trạng thái hợp đồng đã thay đổi, vui lòng tải lại dữ liệu.", new { contractId });
		}
		return await GetByIdAsync(landlordUserId, contract.Id, cancellationToken);
	}

	public async Task<ContractDetailResponse?> LandlordSignAsync(Guid landlordUserId, Guid contractId, SignContractRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default(CancellationToken))
	{
		RentalContract contract = await BaseQuery().AsNoTracking().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		EnsureLandlord(landlordUserId, contract);
		EnsureCanLandlordSign(contract);
		EnsureNotSigned(contract, ContractSignerRole.Landlord);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		EnsureContractStartDateAllowsLandlordSignature(contract.StartDate, now);
		ContractDetailResponse result;
		await using (IAppDbContextTransaction transaction = await context.BeginTransactionAsync(cancellationToken))
		{
			try
			{
				await contractSignatureOtpService.VerifyAndConsumeOtpAsync(landlordUserId, contractId, ContractSignerRole.Landlord, request.Otp, cancellationToken);
				context.ContractSignatures.Add(CreateSignature(contract.Id, landlordUserId, ContractSignerRole.Landlord, request, ipAddress, userAgent, now));
				if (await context.RentalContracts.Where((RentalContract x) => x.Id == contractId && x.DeletedAt == null && ((int)x.Status == 2 || (int)x.Status == 5)).ExecuteUpdateAsync(delegate(UpdateSettersBuilder<RentalContract> setters)
				{
					setters.SetProperty((RentalContract x) => x.Status, RentalContractStatus.PendingTenantSignature).SetProperty((Expression<Func<RentalContract, string>>)((RentalContract x) => x.StatusReason), (string)null).SetProperty((RentalContract x) => x.SignatureDeadlineAt, now.Add(TenantSignatureTtl))
						.SetProperty((RentalContract x) => x.UpdatedAt, now);
				}, cancellationToken) == 0)
				{
					throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Trạng thái hợp đồng đã thay đổi, vui lòng tải lại dữ liệu.", new { contractId });
				}
				await context.SaveChangesAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
				result = await GetByIdAsync(landlordUserId, contract.Id, cancellationToken);
			}
			catch
			{
				await transaction.RollbackAsync(cancellationToken);
				throw;
			}
		}
		return result;
	}

	public async Task<ContractDetailResponse?> TenantSignAsync(Guid tenantUserId, Guid contractId, SignContractRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default(CancellationToken))
	{
		RentalContract contract = await BaseQuery().AsNoTracking().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		EnsureMainTenant(tenantUserId, contract);
		EnsureStatus(contract, RentalContractStatus.PendingTenantSignature);
		EnsureNotSigned(contract, ContractSignerRole.Tenant);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
		EnsureTenantCanSignBeforeStartDate(contract.StartDate, today);
		EnsureTenantSignatureDeadlineNotExpired(contract, now);
		RoomStatus roomStatusAfterSigning = contract.StartDate <= today ? RoomStatus.Occupied : RoomStatus.Reserved;
		ContractDetailResponse result;
		await using (IAppDbContextTransaction transaction = await context.BeginTransactionAsync(cancellationToken))
		{
			try
			{
				await contractSignatureOtpService.VerifyAndConsumeOtpAsync(tenantUserId, contractId, ContractSignerRole.Tenant, request.Otp, cancellationToken);
				context.ContractSignatures.Add(CreateSignature(contract.Id, tenantUserId, ContractSignerRole.Tenant, request, ipAddress, userAgent, now));
				if (await context.RentalContracts.Where((RentalContract x) => x.Id == contractId && x.DeletedAt == null && (int)x.Status == 4).ExecuteUpdateAsync(delegate(UpdateSettersBuilder<RentalContract> setters)
				{
					setters.SetProperty((RentalContract x) => x.Status, RentalContractStatus.Active).SetProperty((Expression<Func<RentalContract, string>>)((RentalContract x) => x.StatusReason), (string)null).SetProperty((Expression<Func<RentalContract, DateTimeOffset?>>)((RentalContract x) => x.SignatureDeadlineAt), (DateTimeOffset?)null)
						.SetProperty((RentalContract x) => x.ActivatedAt, now)
						.SetProperty((RentalContract x) => x.UpdatedAt, now);
				}, cancellationToken) == 0)
				{
					throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Trạng thái hợp đồng đã thay đổi, vui lòng tải lại dữ liệu.", new { contractId });
				}
				await context.Rooms.Where((Room x) => x.Id == contract.RoomId).ExecuteUpdateAsync(delegate(UpdateSettersBuilder<Room> setters)
				{
					setters.SetProperty((Room x) => x.Status, roomStatusAfterSigning).SetProperty((Room x) => x.UpdatedAt, now);
				}, cancellationToken);
				await context.ContractOccupants
					.Where((ContractOccupant x) => x.RentalContractId == contractId && x.Status != ContractOccupantStatus.MoveOut && x.Status != ContractOccupantStatus.Voided && x.MoveInDate <= today)
					.ExecuteUpdateAsync(delegate(UpdateSettersBuilder<ContractOccupant> setters)
					{
						setters.SetProperty((ContractOccupant x) => x.Status, ContractOccupantStatus.Active)
							.SetProperty((ContractOccupant x) => x.UpdatedAt, now);
					}, cancellationToken);
				await context.ContractOccupants
					.Where((ContractOccupant x) => x.RentalContractId == contractId && x.Status != ContractOccupantStatus.MoveOut && x.Status != ContractOccupantStatus.Voided && x.MoveInDate > today)
					.ExecuteUpdateAsync(delegate(UpdateSettersBuilder<ContractOccupant> setters)
					{
						setters.SetProperty((ContractOccupant x) => x.Status, ContractOccupantStatus.PendingMoveIn)
							.SetProperty((ContractOccupant x) => x.UpdatedAt, now);
					}, cancellationToken);
				await context.SaveChangesAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
				await contractFileService.GenerateSignedContractFileAsync(tenantUserId, contract.Id, cancellationToken);
				result = await GetByIdAsync(tenantUserId, contract.Id, cancellationToken);
			}
			catch
			{
				await transaction.RollbackAsync(cancellationToken);
				throw;
			}
		}
		return result;
	}

	public async Task<ContractDetailResponse?> RejectAsync(Guid userId, Guid contractId, RejectContractRequest request, CancellationToken cancellationToken = default(CancellationToken))
	{
		string reason = NormalizeRequiredReason(request.Reason);
		RentalContract contract = await BaseQuery().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		DateTimeOffset now = DateTimeOffset.UtcNow;
		bool isLandlord = contract.Room.RoomingHouse.LandlordUserId == userId;
		if (isLandlord)
		{
			EnsureLandlordCanReject(contract);
		}
		else
		{
			EnsureMainTenant(userId, contract);
			EnsureStatus(contract, RentalContractStatus.PendingTenantSignature);
		}

		RoomDeposit deposit = contract.RoomDeposit;
		EnsureDepositReadyForSettlement(deposit);
		var landlordWallet = await walletService.GetOrCreateWalletAsync(deposit.LandlordUserId, cancellationToken);
		var tenantWallet = await walletService.GetOrCreateWalletAsync(deposit.TenantUserId, cancellationToken);

		await using IAppDbContextTransaction transaction = await context.BeginTransactionAsync(cancellationToken);
		try
		{
			await rowLockService.LockRentalContractAsync(contractId, cancellationToken);
			await rowLockService.LockRoomDepositAsync(deposit.Id, cancellationToken);
			contract = await BaseQuery().FirstAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
			deposit = contract.RoomDeposit;
			isLandlord = contract.Room.RoomingHouse.LandlordUserId == userId;
			if (isLandlord)
			{
				EnsureLandlordCanReject(contract);
				EnsureDepositReadyForSettlement(deposit);

				var settlementGroupId = Guid.NewGuid();
				await walletService.TransferFromReservedWithinTransactionAsync(
					landlordWallet.Id,
					tenantWallet.Id,
					deposit.DepositAmount,
					deposit.DepositAmount,
					WalletTransactionType.DepositRefundDebit,
					WalletTransactionType.DepositRefundCredit,
					CreateDepositSettlementMetadata(deposit, settlementGroupId, "Landlord contract rejection deposit refund."),
					cancellationToken);

				contract.RoomDeposit.Status = RoomDepositStatus.Refunded;
				contract.RoomDeposit.RefundedAt = now;
				contract.RoomDeposit.RefundAmount = contract.RoomDeposit.DepositAmount;
				contract.RoomDeposit.ForfeitedAt = null;
				contract.RoomDeposit.ForfeitedAmount = default(decimal);
				contract.RoomDeposit.RefundTransferGroupId = settlementGroupId;
				contract.RentalRequest.Status = RentalRequestStatus.Rejected;
			}
			else
			{
				EnsureMainTenant(userId, contract);
				EnsureStatus(contract, RentalContractStatus.PendingTenantSignature);
				EnsureDepositReadyForSettlement(deposit);

				var settlementGroupId = Guid.NewGuid();
				await walletService.ReleaseReservedWithinTransactionAsync(
					landlordWallet.Id,
					deposit.DepositAmount,
					WalletTransactionType.DepositForfeitRelease,
					CreateDepositSettlementMetadata(deposit, settlementGroupId, "Tenant contract rejection deposit forfeiture."),
					cancellationToken);

				contract.RoomDeposit.Status = RoomDepositStatus.Forfeited;
				contract.RoomDeposit.RefundedAt = null;
				contract.RoomDeposit.RefundAmount = default(decimal);
				contract.RoomDeposit.ForfeitedAt = now;
				contract.RoomDeposit.ForfeitedAmount = contract.RoomDeposit.DepositAmount;
				contract.RoomDeposit.RefundTransferGroupId = null;
				contract.RentalRequest.Status = RentalRequestStatus.Cancelled;
			}

			contract.Status = RentalContractStatus.Rejected;
			contract.StatusReason = reason;
			contract.SignatureDeadlineAt = null;
			contract.UpdatedAt = now;
			contract.RoomDeposit.UpdatedAt = now;
			contract.RentalRequest.RejectedReason = reason;
			contract.RentalRequest.UpdatedAt = now;
			if (contract.Room.Status == RoomStatus.Reserved)
			{
				contract.Room.Status = RoomStatus.Available;
				contract.Room.UpdatedAt = now;
			}
			await context.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken);
			throw;
		}
		return await GetByIdAsync(userId, contract.Id, cancellationToken);
	}

	public async Task<ContractDetailResponse?> RequestRevisionAsync(Guid userId, Guid contractId, RequestContractRevisionRequest request, CancellationToken cancellationToken = default(CancellationToken))
	{
		string reason = NormalizeRequiredReason(request.Reason);
		RentalContract contract = await BaseQuery().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		DateTimeOffset now = DateTimeOffset.UtcNow;
		if (contract.Room.RoomingHouse.LandlordUserId == userId)
		{
			if (request.RevisionType != ContractRevisionType.Occupants)
			{
				throw new BadRequestException("VALIDATION_ERROR", "Chủ trọ chỉ có thể yêu cầu người thuê sửa thông tin người ở.");
			}
			EnsureStatus(contract, RentalContractStatus.PendingLandlordSignature);
			contract.Status = RentalContractStatus.LandlordRevisionRequested;
			contract.StatusReason = reason;
			contract.SignatureDeadlineAt = null;
			contract.UpdatedAt = now;
			await context.SaveChangesAsync(cancellationToken);
			return await GetByIdAsync(userId, contract.Id, cancellationToken);
		}
		EnsureMainTenant(userId, contract);
		EnsureStatus(contract, RentalContractStatus.PendingTenantSignature);
		List<ContractSignature> existingSignatures = contract.Signatures.ToList();
		context.ContractSignatures.RemoveRange(existingSignatures);
		RentalContract rentalContract = contract;
		ContractRevisionType revisionType = request.RevisionType;
		if (1 == 0)
		{
		}
		RentalContractStatus status = revisionType switch
		{
			ContractRevisionType.Occupants => RentalContractStatus.WaitingTenantOccupants, 
			ContractRevisionType.ContractTerms => RentalContractStatus.TenantRevisionRequested, 
			_ => throw new BadRequestException("VALIDATION_ERROR", "Loại yêu cầu sửa hợp đồng không hợp lệ."), 
		};
		if (1 == 0)
		{
		}
		rentalContract.Status = status;
		contract.StatusReason = reason;
		contract.SignatureDeadlineAt = null;
		contract.UpdatedAt = now;
		await context.SaveChangesAsync(cancellationToken);
		return await GetByIdAsync(userId, contract.Id, cancellationToken);
	}

	public async Task<ContractDetailResponse?> TerminateAsync(Guid userId, Guid contractId, TerminateContractRequest request, CancellationToken cancellationToken = default(CancellationToken))
	{
		RentalContract contract = await BaseQuery().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		bool isLandlord = contract.Room.RoomingHouse.LandlordUserId == userId;
		bool isTenant = GetCurrentMainTenantUserId(contract) == userId;
		if (!isLandlord && !isTenant)
		{
			throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Bạn không có quyền thao tác với hợp đồng này.", new { contractId });
		}
		EnsureStatus(contract, RentalContractStatus.Active);
		if (isTenant && request.TerminationType != ContractTerminationType.TenantUnilateral)
		{
			throw new ForbiddenException(
				ErrorCodes.RentalContractForbidden,
				"Người thuê chỉ có thể đơn phương chấm dứt hợp đồng.");
		}
		if (isLandlord && request.TerminationType == ContractTerminationType.TenantUnilateral)
		{
			throw new ForbiddenException(
				ErrorCodes.RentalContractForbidden,
				"Chủ trọ không thể chấm dứt hợp đồng thay cho người thuê.");
		}
		if (isTenant)
		{
			bool hasOutstandingInvoices = await context.Invoices.AsNoTracking().AnyAsync(
				x => x.ContractId == contract.Id &&
					 (x.Status == InvoiceStatus.Issued || x.Status == InvoiceStatus.Overdue),
				cancellationToken);
			if (hasOutstandingInvoices)
			{
				throw new ConflictException(
					ErrorCodes.TenantOutstandingInvoice,
					"Bạn phải thanh toán tất cả hóa đơn đã phát hành hoặc quá hạn trước khi chấm dứt hợp đồng.");
			}
		}
		DateTimeOffset now = DateTimeOffset.UtcNow;
		DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
		var currentTerms = ResolveCurrentContractTermValues(contract);
		DateOnly terminationDate = request.TerminationType == ContractTerminationType.NormalExpiration
			? currentTerms.EndDate
			: today;
		bool isBeforeContractStart = today < contract.StartDate;
		if (request.TerminationType == ContractTerminationType.NormalExpiration && today < currentTerms.EndDate)
		{
			throw new BadRequestException(
				"VALIDATION_ERROR",
				$"Hợp đồng còn hạn đến ngày {currentTerms.EndDate:dd/MM/yyyy} nên không thể đáo hạn.");
		}
		if (request.CreateFinalInvoice && !isLandlord)
		{
			throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Chỉ chủ trọ mới có quyền tạo hóa đơn kỳ cuối khi chấm dứt hợp đồng.");
		}
		if (request.CreateFinalInvoice && isBeforeContractStart)
		{
			throw new BadRequestException(
				"VALIDATION_ERROR",
				"Hợp đồng chưa đến ngày bắt đầu thuê nên không thể tạo hóa đơn kỳ cuối.");
		}
		RoomDeposit deposit = contract.RoomDeposit;
		EnsureDepositReadyForSettlement(deposit);
		var landlordWallet = await walletService.GetOrCreateWalletAsync(deposit.LandlordUserId, cancellationToken);
		var tenantWallet = await walletService.GetOrCreateWalletAsync(deposit.TenantUserId, cancellationToken);
		string reasonText = (string.IsNullOrWhiteSpace(request.Reason) ? string.Empty : request.Reason.Trim());
		await using IAppDbContextTransaction transaction = await context.BeginTransactionAsync(cancellationToken);

		await rowLockService.LockRentalContractAsync(contractId, cancellationToken);
		await rowLockService.LockRoomDepositAsync(deposit.Id, cancellationToken);
		contract = await BaseQuery().FirstAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		deposit = contract.RoomDeposit;
		isLandlord = contract.Room.RoomingHouse.LandlordUserId == userId;
		isTenant = GetCurrentMainTenantUserId(contract) == userId;
		if (!isLandlord && !isTenant)
		{
			throw new ForbiddenException(ErrorCodes.RentalContractForbidden, "Bạn không có quyền thao tác với hợp đồng này.", new { contractId });
		}
		EnsureStatus(contract, RentalContractStatus.Active);
		EnsureDepositReadyForSettlement(deposit);

		if (request.CreateFinalInvoice)
		{
			await billingService.CreateFinalInvoiceForTerminationAsync(
				userId,
				contract.Id,
				terminationDate,
				request.FinalInvoiceDiscountAmount,
				request.FinalInvoiceNote,
				request.FinalInvoiceMeterReadings,
				cancellationToken);
		}
		switch (request.TerminationType)
		{
		case ContractTerminationType.LandlordUnilateral:
		{
			if (!isLandlord)
			{
				throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Chỉ chủ trọ mới có quyền dùng thao tác này.");
			}
			var settlementGroupId = Guid.NewGuid();
			var totalRefundAmount = deposit.DepositAmount * 2m;
			await walletService.TransferFromReservedWithinTransactionAsync(
				landlordWallet.Id,
				tenantWallet.Id,
				totalRefundAmount,
				deposit.DepositAmount,
				WalletTransactionType.DepositRefundDebit,
				WalletTransactionType.DepositRefundCredit,
				CreateDepositSettlementMetadata(deposit, settlementGroupId, "Landlord unilateral termination deposit refund."),
				cancellationToken);
			contract.Status = RentalContractStatus.Cancelled;
			deposit.Status = RoomDepositStatus.Refunded;
			deposit.RefundAmount = totalRefundAmount;
			deposit.ForfeitedAmount = default(decimal);
			deposit.RefundedAt = now;
			deposit.ForfeitedAt = null;
			deposit.RefundTransferGroupId = settlementGroupId;
			contract.StatusReason = $"Chủ trọ đơn phương chấm dứt. Hoàn cọc gốc: {deposit.DepositAmount:N0} VNĐ, đền bù: {deposit.DepositAmount:N0} VNĐ. Tổng hoàn: {deposit.RefundAmount:N0} VNĐ. Lý do: {reasonText}";
			break;
		}
		case ContractTerminationType.TenantUnilateral:
		{
			if (!isTenant && !isLandlord)
			{
				throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Không có quyền thao tác.");
			}
			var settlementGroupId = Guid.NewGuid();
			await walletService.ReleaseReservedWithinTransactionAsync(
				landlordWallet.Id,
				deposit.DepositAmount,
				WalletTransactionType.DepositForfeitRelease,
				CreateDepositSettlementMetadata(deposit, settlementGroupId, "Tenant unilateral termination deposit forfeiture."),
				cancellationToken);
			contract.Status = RentalContractStatus.Cancelled;
			deposit.Status = RoomDepositStatus.Forfeited;
			deposit.RefundAmount = default(decimal);
			deposit.ForfeitedAmount = deposit.DepositAmount;
			deposit.ForfeitedAt = now;
			deposit.RefundedAt = null;
			deposit.RefundTransferGroupId = null;
			contract.StatusReason = $"Người thuê đơn phương chấm dứt. Tịch thu cọc: {deposit.DepositAmount:N0} VNĐ. Lý do: {reasonText}";
			break;
		}
		case ContractTerminationType.NormalExpiration:
		case ContractTerminationType.MutualAgreement:
		{
			if (!isLandlord)
			{
				throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Chỉ chủ trọ mới có quyền xác nhận thanh lý.");
			}
			contract.Status = ((request.TerminationType == ContractTerminationType.NormalExpiration) ? RentalContractStatus.Expired : RentalContractStatus.Cancelled);
			deposit.Status = RoomDepositStatus.Refunded;
			decimal damageFee = ((request.DamageFee >= 0m) ? request.DamageFee : 0m);
			if (damageFee > deposit.DepositAmount)
			{
				damageFee = deposit.DepositAmount;
			}
			deposit.ForfeitedAmount = damageFee;
			deposit.RefundAmount = deposit.DepositAmount - damageFee;
			var settlementGroupId = Guid.NewGuid();
			if (deposit.RefundAmount > 0m)
			{
				await walletService.TransferFromReservedWithinTransactionAsync(
					landlordWallet.Id,
					tenantWallet.Id,
					deposit.RefundAmount.Value,
					deposit.RefundAmount.Value,
					WalletTransactionType.DepositRefundDebit,
					WalletTransactionType.DepositRefundCredit,
					CreateDepositSettlementMetadata(deposit, settlementGroupId, "Contract termination deposit refund."),
					cancellationToken);
			}
			if (damageFee > 0m)
			{
				await walletService.ReleaseReservedWithinTransactionAsync(
					landlordWallet.Id,
					damageFee,
					WalletTransactionType.DepositForfeitRelease,
					CreateDepositSettlementMetadata(deposit, settlementGroupId, "Contract termination retained deposit release."),
					cancellationToken);
			}
			deposit.RefundedAt = now;
			deposit.ForfeitedAt = damageFee > 0m ? now : null;
			deposit.RefundTransferGroupId = settlementGroupId;
			string typeStr = ((request.TerminationType == ContractTerminationType.NormalExpiration) ? "Đáo hạn hợp đồng" : "Hai bên thỏa thuận chấm dứt");
			contract.StatusReason = $"{typeStr}. Trừ hư hỏng/dọn dẹp: {damageFee:N0} VNĐ. Hoàn cọc thực tế: {deposit.RefundAmount:N0} VNĐ. Lý do bổ sung: {reasonText}";
			break;
		}
		default:
			throw new BadRequestException("VALIDATION_ERROR", "Loại thanh lý không hợp lệ.");
		}
		contract.UpdatedAt = now;
		contract.TerminationDate = terminationDate;
		contract.TerminationType = request.TerminationType;
		deposit.UpdatedAt = now;
		CancelOpenAppendices(contract, now);
		CloseContractOccupants(contract, terminationDate, today, now);
		if (contract.Room.Status is RoomStatus.Occupied or RoomStatus.Reserved)
		{
			contract.Room.Status = RoomStatus.Available;
			contract.Room.UpdatedAt = now;
		}
		await context.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return await GetByIdAsync(userId, contract.Id, cancellationToken);
	}

	private static void EnsureDepositReadyForSettlement(RoomDeposit deposit)
	{
		if (deposit.Status != RoomDepositStatus.Paid ||
			!deposit.PaymentTransferGroupId.HasValue ||
			deposit.RefundTransferGroupId.HasValue)
		{
			throw new ConflictException(
				ErrorCodes.RoomDepositInvalidStatus,
				"Khoản cọc không ở trạng thái sẵn sàng để tất toán.",
				new
				{
					deposit.Id,
					currentStatus = deposit.Status.ToString(),
					deposit.PaymentTransferGroupId,
					deposit.RefundTransferGroupId
				});
		}
	}

	private static WalletTransactionMetadata CreateDepositSettlementMetadata(
		RoomDeposit deposit,
		Guid transferGroupId,
		string description)
	{
		return new WalletTransactionMetadata
		{
			TransferGroupId = transferGroupId,
			RelatedEntityType = "RoomDeposit",
			RelatedEntityId = deposit.Id,
			Description = description
		};
	}

	private static void CancelOpenAppendices(RentalContract contract, DateTimeOffset now)
	{
		foreach (ContractAppendix appendix in contract.Appendices.Where((ContractAppendix x) => x.Status is ContractAppendixStatus.Draft or ContractAppendixStatus.PendingSignature or ContractAppendixStatus.Active or ContractAppendixStatus.LandlordRevisionRequested or ContractAppendixStatus.TenantRevisionRequested))
		{
			appendix.Status = ContractAppendixStatus.Cancelled;
			appendix.StatusReason = "Hợp đồng đã chấm dứt.";
			appendix.UpdatedAt = now;
		}
	}

	private static void CloseContractOccupants(RentalContract contract, DateOnly terminationDate, DateOnly today, DateTimeOffset now)
	{
		bool isBeforeContractStart = today < contract.StartDate;
		foreach (ContractOccupant occupant in contract.Occupants.Where((ContractOccupant x) => x.Status is ContractOccupantStatus.Active or ContractOccupantStatus.PendingMoveIn))
		{
			if (isBeforeContractStart || occupant.Status == ContractOccupantStatus.PendingMoveIn)
			{
				occupant.Status = ContractOccupantStatus.Voided;
				occupant.MoveOutDate = null;
			}
			else
			{
				occupant.Status = ContractOccupantStatus.MoveOut;
				occupant.MoveOutDate = terminationDate;
			}
			occupant.UpdatedAt = now;
		}
	}

	public async Task<int> ExpireOverdueTenantSignaturesAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		var overdueContractSnapshots = await BaseQuery()
			.AsNoTracking()
			.Where((RentalContract x) => x.Status == RentalContractStatus.PendingTenantSignature &&
				x.SignatureDeadlineAt.HasValue &&
				x.SignatureDeadlineAt.Value <= now &&
				x.DeletedAt == null)
			.Select((RentalContract x) => new
			{
				x.Id,
				x.RoomDeposit.LandlordUserId
			})
			.ToListAsync(cancellationToken);

		if (overdueContractSnapshots.Count == 0)
		{
			return 0;
		}

		Dictionary<Guid, Guid> landlordWalletIdsByUserId = new Dictionary<Guid, Guid>();
		foreach (Guid landlordUserId in overdueContractSnapshots
			.Select(x => x.LandlordUserId)
			.Distinct())
		{
			var landlordWallet = await walletService.GetOrCreateWalletAsync(landlordUserId, cancellationToken);
			landlordWalletIdsByUserId[landlordUserId] = landlordWallet.Id;
		}

		int count = 0;
		await using (IAppDbContextTransaction transaction = await context.BeginTransactionAsync(cancellationToken))
		{
			try
			{
				foreach (Guid contractId in overdueContractSnapshots
					.Select(x => x.Id)
					.Distinct()
					.OrderBy(x => x))
				{
					await rowLockService.LockRentalContractAsync(contractId, cancellationToken);
					RentalContract? contract = await BaseQuery()
						.FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
					if (contract is null)
					{
						continue;
					}

					await rowLockService.LockRoomDepositAsync(contract.RoomDepositId, cancellationToken);
					contract = await BaseQuery()
						.FirstAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);

					if (contract.Status != RentalContractStatus.PendingTenantSignature ||
						!contract.SignatureDeadlineAt.HasValue ||
						contract.SignatureDeadlineAt.Value > now)
					{
						continue;
					}

					EnsureDepositReadyForSettlement(contract.RoomDeposit);
					if (!landlordWalletIdsByUserId.TryGetValue(contract.RoomDeposit.LandlordUserId, out Guid landlordWalletId))
					{
						throw new NotFoundException(
							ErrorCodes.NotFound,
							"Không tìm thấy ví chủ trọ để tất toán khoản cọc quá hạn ký hợp đồng.",
							new
							{
								contract.Id,
								contract.RoomDepositId,
								contract.RoomDeposit.LandlordUserId
							});
					}

					var settlementGroupId = Guid.NewGuid();
					await walletService.ReleaseReservedWithinTransactionAsync(
						landlordWalletId,
						contract.RoomDeposit.DepositAmount,
						WalletTransactionType.DepositForfeitRelease,
						CreateDepositSettlementMetadata(contract.RoomDeposit, settlementGroupId, "Tenant signature deadline deposit forfeiture."),
						cancellationToken);

					contract.Status = RentalContractStatus.Expired;
					contract.StatusReason = "Người thuê không ký hợp đồng trong thời hạn quy định.";
					contract.SignatureDeadlineAt = null;
					contract.UpdatedAt = now;
					contract.RoomDeposit.Status = RoomDepositStatus.Forfeited;
					contract.RoomDeposit.RefundAmount = default(decimal);
					contract.RoomDeposit.ForfeitedAt = now;
					contract.RoomDeposit.ForfeitedAmount = contract.RoomDeposit.DepositAmount;
					contract.RoomDeposit.RefundedAt = null;
					contract.RoomDeposit.RefundTransferGroupId = null;
					contract.RoomDeposit.UpdatedAt = now;
					contract.RentalRequest.Status = RentalRequestStatus.Expired;
					contract.RentalRequest.RejectedReason = "Người thuê không ký hợp đồng trong thời hạn quy định.";
					contract.RentalRequest.UpdatedAt = now;
					if (contract.Room.Status == RoomStatus.Reserved)
					{
						contract.Room.Status = RoomStatus.Available;
						contract.Room.UpdatedAt = now;
					}

					count++;
				}
				await context.SaveChangesAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
			}
			catch
			{
				await transaction.RollbackAsync(cancellationToken);
				throw;
			}
		}
		return count;
	}

	public async Task<int> ActivatePendingMoveInsAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
		int count;
		await using (IAppDbContextTransaction transaction = await context.BeginTransactionAsync(cancellationToken))
		{
			try
			{
				List<RentalContract> contracts = await BaseQuery()
					.Where((RentalContract x) => x.DeletedAt == null &&
						x.Status == RentalContractStatus.Active &&
						x.StartDate <= today &&
						(x.Room.Status == RoomStatus.Reserved ||
							x.Occupants.Any((ContractOccupant occupant) =>
								occupant.Status == ContractOccupantStatus.PendingMoveIn &&
								occupant.MoveInDate <= today)))
					.ToListAsync(cancellationToken);

				foreach (RentalContract contract in contracts)
				{
					if (contract.Room.Status == RoomStatus.Reserved)
					{
						contract.Room.Status = RoomStatus.Occupied;
						contract.Room.UpdatedAt = now;
					}

					foreach (ContractOccupant occupant in contract.Occupants.Where((ContractOccupant x) =>
						x.Status == ContractOccupantStatus.PendingMoveIn &&
						x.MoveInDate <= today))
					{
						occupant.Status = ContractOccupantStatus.Active;
						occupant.UpdatedAt = now;
					}

					contract.UpdatedAt = now;
				}

				await context.SaveChangesAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
				count = contracts.Count;
			}
			catch
			{
				await transaction.RollbackAsync(cancellationToken);
				throw;
			}
		}

		return count;
	}

	private IQueryable<RentalContract> BaseQuery()
	{
		return context.RentalContracts.Include((RentalContract x) => x.RentalRequest).Include((RentalContract x) => x.RoomDeposit).Include((RentalContract x) => x.MainTenantUser)
			.ThenInclude((User x) => x.UserProfile)
			.Include((RentalContract x) => x.Room)
			.ThenInclude((Room x) => x.RoomingHouse)
			.ThenInclude((RoomingHouse x) => x.Landlord)
			.ThenInclude((User x) => x.UserProfile)
			.Include((RentalContract x) => x.Occupants)
			.ThenInclude((ContractOccupant x) => x.Documents)
			.Include((RentalContract x) => x.Occupants)
			.ThenInclude((ContractOccupant x) => x.User)
			.ThenInclude((User x) => x.UserProfile)
			.Include((RentalContract x) => x.Appendices)
			.ThenInclude((ContractAppendix x) => x.Changes)
			.Include((RentalContract x) => x.Signatures);
	}

	private async Task<ContractRenderOptions> BuildPreviewRenderOptionsAsync(RentalContract contract, ContractPreviewViewerAccess viewerAccess, CancellationToken cancellationToken)
	{
		IReadOnlyDictionary<Guid, string?> readOnlyDictionary = ((!viewerAccess.ShowFullDocumentNumbers) ? new Dictionary<Guid, string>() : (await GetDecryptedUserDocumentNumbersAsync(contract, cancellationToken)));
		IReadOnlyDictionary<Guid, string?> userDocumentNumbersByUserId = readOnlyDictionary;
		IReadOnlyDictionary<Guid, string?> readOnlyDictionary3;
		if (!viewerAccess.ShowFullDocumentNumbers)
		{
			IReadOnlyDictionary<Guid, string> readOnlyDictionary2 = new Dictionary<Guid, string>();
			readOnlyDictionary3 = readOnlyDictionary2;
		}
		else
		{
			readOnlyDictionary3 = GetDecryptedOccupantDocumentNumbers(contract);
		}
		IReadOnlyDictionary<Guid, string?> occupantDocumentNumbersByDocumentId = readOnlyDictionary3;
		return new ContractRenderOptions
		{
			ViewerMode = viewerAccess.ViewerMode,
			ShowFullDocumentNumbers = viewerAccess.ShowFullDocumentNumbers,
			VisibleOccupantIds = viewerAccess.VisibleOccupantIds,
			UserDocumentNumbersByUserId = userDocumentNumbersByUserId,
			OccupantDocumentNumbersByDocumentId = occupantDocumentNumbersByDocumentId
		};
	}

	private async Task<IReadOnlyDictionary<Guid, string?>> GetDecryptedUserDocumentNumbersAsync(RentalContract contract, CancellationToken cancellationToken)
	{
		HashSet<Guid> userIds = new HashSet<Guid>
		{
			contract.Room.RoomingHouse.LandlordUserId,
			contract.MainTenantUserId
		};
		foreach (Guid occupantUserId in from x in contract.Occupants
			where x.UserId.HasValue
			select x.UserId.Value)
		{
			userIds.Add(occupantUserId);
		}
		return (from x in await (from x in context.KycVerifications.AsNoTracking()
				where userIds.Contains(x.UserId) && (int)x.Status == 4
				select x).ToListAsync(cancellationToken)
			group x by x.UserId into x
			select x.OrderByDescending((KycVerification k) => k.ReviewedAt ?? k.UpdatedAt).First() into x
			select new
			{
				UserId = x.UserId,
				DocumentNumber = DecryptDocumentNumber(x.DocumentNumberEncrypted)
			} into x
			where !string.IsNullOrWhiteSpace(x.DocumentNumber)
			select x).ToDictionary(x => x.UserId, x => x.DocumentNumber);
	}

	private IReadOnlyDictionary<Guid, string?> GetDecryptedOccupantDocumentNumbers(RentalContract contract)
	{
		return (from x in contract.Occupants.SelectMany((ContractOccupant x) => x.Documents)
			select new
			{
				Id = x.Id,
				DocumentNumber = DecryptDocumentNumber(x.DocumentNumberEncrypted)
			} into x
			where !string.IsNullOrWhiteSpace(x.DocumentNumber)
			select x).ToDictionary(x => x.Id, x => x.DocumentNumber);
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

	private static ContractPreviewViewerAccess? ResolvePreviewViewerAccess(Guid userId, RentalContract contract)
	{
		if (contract.Room.RoomingHouse.LandlordUserId == userId || GetCurrentMainTenantUserId(contract) == userId)
		{
			return new ContractPreviewViewerAccess("Full", ShowFullDocumentNumbers: true, null);
		}
		List<Guid> list = (from x in contract.Occupants
			where x.UserId == userId
			select x.Id).ToList();
		if (list.Count > 0)
		{
			return new ContractPreviewViewerAccess("MaskedLimited", ShowFullDocumentNumbers: false, list);
		}
		return null;
	}

	private async Task<bool> IsAwaitingFinalInvoiceAsync(
		RentalContract contract,
		CancellationToken cancellationToken)
	{
		if (!TryResolveRequiredFinalInvoicePeriod(contract, out DateOnly periodStart, out DateOnly periodEnd))
		{
			return false;
		}

		return !await context.Invoices.AsNoTracking().AnyAsync(
			x => x.ContractId == contract.Id &&
				x.BillingPeriodStart == periodStart &&
				x.BillingPeriodEnd == periodEnd &&
				x.Status != InvoiceStatus.Cancelled,
			cancellationToken);
	}

	private async Task<HashSet<Guid>> GetAwaitingFinalInvoiceContractIdsAsync(
		IReadOnlyCollection<RentalContract> contracts,
		CancellationToken cancellationToken)
	{
		var candidates = contracts
			.Select(contract => TryResolveRequiredFinalInvoicePeriod(contract, out DateOnly start, out DateOnly end)
				? new { contract.Id, Start = start, End = end }
				: null)
			.Where(x => x is not null)
			.ToList();

		if (candidates.Count == 0)
		{
			return [];
		}

		Guid[] contractIds = candidates.Select(x => x!.Id).ToArray();
		var invoices = await context.Invoices.AsNoTracking()
			.Where(x => contractIds.Contains(x.ContractId) && x.Status != InvoiceStatus.Cancelled)
			.Select(x => new { x.ContractId, x.BillingPeriodStart, x.BillingPeriodEnd })
			.ToListAsync(cancellationToken);

		return candidates
			.Where(candidate => !invoices.Any(invoice =>
				invoice.ContractId == candidate!.Id &&
				invoice.BillingPeriodStart == candidate.Start &&
				invoice.BillingPeriodEnd == candidate.End))
			.Select(candidate => candidate!.Id)
			.ToHashSet();
	}

	private static bool TryResolveRequiredFinalInvoicePeriod(
		RentalContract contract,
		out DateOnly periodStart,
		out DateOnly periodEnd)
	{
		periodStart = default;
		periodEnd = default;
		if (contract.Status != RentalContractStatus.Cancelled ||
			contract.TerminationType != ContractTerminationType.TenantUnilateral ||
			!contract.TerminationDate.HasValue ||
			contract.TerminationDate.Value < contract.StartDate)
		{
			return false;
		}

		periodEnd = contract.TerminationDate.Value;
		DateOnly monthStart = new(periodEnd.Year, periodEnd.Month, 1);
		periodStart = contract.StartDate > monthStart ? contract.StartDate : monthStart;
		return true;
	}

	private static ContractSignature CreateSignature(Guid contractId, Guid signerUserId, ContractSignerRole signerRole, SignContractRequest request, string? ipAddress, string? userAgent, DateTimeOffset now)
	{
		return new ContractSignature
		{
			Id = Guid.NewGuid(),
			RentalContractId = contractId,
			SignerUserId = signerUserId,
			SignerRole = signerRole,
			SignatureMethod = ContractSignatureMethod.EmailOtp,
			SignatureText = NormalizeOptionalText(request.SignatureText),
			IpAddress = NormalizeOptionalText(ipAddress),
			UserAgent = NormalizeOptionalText(userAgent),
			SignedAt = now,
			CreatedAt = now
		};
	}

	private static ContractDetailResponse MapToDetailResponse(RentalContract contract)
	{
		ResolveCurrentContractTerms(contract);
		ContractDetailResponse contractDetailResponse = new ContractDetailResponse();
		contractDetailResponse.Id = contract.Id;
		contractDetailResponse.RentalRequestId = contract.RentalRequestId;
		contractDetailResponse.RoomDepositId = contract.RoomDepositId;
		contractDetailResponse.RoomId = contract.RoomId;
		contractDetailResponse.RoomNumber = contract.Room.RoomNumber;
		contractDetailResponse.RoomingHouseId = contract.Room.RoomingHouseId;
		contractDetailResponse.RoomingHouseName = contract.Room.RoomingHouse.Name;
		contractDetailResponse.MainTenantUserId = contract.MainTenantUserId;
		contractDetailResponse.MainTenantName = contract.MainTenantUser.DisplayName;
		contractDetailResponse.ContractNumber = contract.ContractNumber;
		contractDetailResponse.StartDate = contract.StartDate;
		contractDetailResponse.EndDate = contract.EndDate;
		contractDetailResponse.MonthlyRent = contract.MonthlyRent;
		contractDetailResponse.DepositAmount = contract.DepositAmount;
		contractDetailResponse.PaymentDay = contract.PaymentDay;
		contractDetailResponse.Status = contract.Status.ToString();
		contractDetailResponse.RoomSnapshot = contract.RoomSnapshot;
		contractDetailResponse.SignatureDeadlineAt = contract.SignatureDeadlineAt;
		contractDetailResponse.ActivatedAt = contract.ActivatedAt;
		contractDetailResponse.TerminationDate = contract.TerminationDate;
		contractDetailResponse.TerminationType = contract.TerminationType?.ToString();
		contractDetailResponse.StatusReason = contract.StatusReason;
		contractDetailResponse.Occupants = contract.Occupants.OrderBy((ContractOccupant x) => x.CreatedAt).Select(MapOccupantToResponse).ToList();
		contractDetailResponse.Signatures = contract.Signatures.OrderBy((ContractSignature x) => x.SignedAt).Select(MapSignatureToResponse).ToList();
		contractDetailResponse.CreatedAt = contract.CreatedAt;
		contractDetailResponse.UpdatedAt = contract.UpdatedAt;
		return contractDetailResponse;
	}

	private static ContractHistoryItemResponse MapToHistoryItemResponse(RentalContract contract, Guid userId)
	{
		Guid currentMainTenantUserId = GetCurrentMainTenantUserId(contract);
		IReadOnlyCollection<Guid> mainTenantUserIds = GetMainTenantUserIds(contract);
		ContractAppendix contractAppendix = ResolveHistorySnapshotBoundaryAppendix(contract, userId);
		ResolveContractTerms(contract, contractAppendix);
		List<ContractOccupant> source = ResolveOccupantsForHistorySnapshot(contract, contractAppendix).ToList();
		bool flag = contract.Room.RoomingHouse.LandlordUserId == userId;
		bool flag2 = currentMainTenantUserId == userId;
		bool flag3 = mainTenantUserIds.Contains(userId);
		ContractOccupant contractOccupant = (from x in source
			where x.UserId == userId
			orderby x.Status == ContractOccupantStatus.Active descending, x.UpdatedAt descending
			select x).FirstOrDefault();
		bool isFormerMainTenant = flag3 && !flag2;
		bool isCoTenant = contractOccupant != null && contractOccupant.Status == ContractOccupantStatus.Active && !flag2;
		bool isFormerCoTenant = contractOccupant != null && contractOccupant.Status != ContractOccupantStatus.Active && !flag3;
		bool flag4 = flag || flag3;
		bool canViewMaskedContract = !flag4 && contractOccupant != null;
		bool flag5 = flag2 && contract.Status == RentalContractStatus.Active;
		string currentUserRelation = ResolveCurrentUserRelation(flag2, isFormerMainTenant, isCoTenant, isFormerCoTenant, contractOccupant != null);
		ContractHistoryItemResponse contractHistoryItemResponse = new ContractHistoryItemResponse();
		contractHistoryItemResponse.Id = contract.Id;
		contractHistoryItemResponse.RentalRequestId = contract.RentalRequestId;
		contractHistoryItemResponse.RoomId = contract.RoomId;
		contractHistoryItemResponse.RoomNumber = contract.Room.RoomNumber;
		contractHistoryItemResponse.RoomingHouseId = contract.Room.RoomingHouseId;
		contractHistoryItemResponse.RoomingHouseName = contract.Room.RoomingHouse.Name;
		contractHistoryItemResponse.MainTenantUserId = contract.MainTenantUserId;
		contractHistoryItemResponse.MainTenantName = contract.MainTenantUser.DisplayName;
		contractHistoryItemResponse.ContractNumber = contract.ContractNumber;
		contractHistoryItemResponse.StartDate = contract.StartDate;
		contractHistoryItemResponse.EndDate = contract.EndDate;
		contractHistoryItemResponse.MonthlyRent = contract.MonthlyRent;
		contractHistoryItemResponse.DepositAmount = contract.DepositAmount;
		contractHistoryItemResponse.PaymentDay = contract.PaymentDay;
		contractHistoryItemResponse.MaxOccupants = GetSnapshotMaxOccupants(contract);
		contractHistoryItemResponse.Status = contract.Status.ToString();
		contractHistoryItemResponse.StatusReason = contract.StatusReason;
		contractHistoryItemResponse.SignatureDeadlineAt = contract.SignatureDeadlineAt;
		contractHistoryItemResponse.ActivatedAt = contract.ActivatedAt;
		contractHistoryItemResponse.TerminationDate = contract.TerminationDate;
		contractHistoryItemResponse.TerminationType = contract.TerminationType?.ToString();
		contractHistoryItemResponse.IsMainTenant = flag2;
		contractHistoryItemResponse.WasMainTenant = flag3;
		contractHistoryItemResponse.IsFormerMainTenant = isFormerMainTenant;
		contractHistoryItemResponse.IsCoTenant = isCoTenant;
		contractHistoryItemResponse.IsFormerCoTenant = isFormerCoTenant;
		contractHistoryItemResponse.CurrentUserRelation = currentUserRelation;
		contractHistoryItemResponse.CurrentUserOccupantId = contractOccupant?.Id;
		contractHistoryItemResponse.CurrentUserOccupantStatus = contractOccupant?.Status.ToString();
		contractHistoryItemResponse.CurrentUserMoveInDate = contractOccupant?.MoveInDate;
		contractHistoryItemResponse.CurrentUserMoveOutDate = contractOccupant?.MoveOutDate;
		contractHistoryItemResponse.SnapshotAtAppendixId = contractAppendix?.Id;
		contractHistoryItemResponse.SnapshotAtDate = contractAppendix?.EffectiveDate;
		contractHistoryItemResponse.Occupants = (from x in source
			orderby x.MoveInDate, x.CreatedAt
			select x).Select(MapOccupantToResponse).ToList();
		contractHistoryItemResponse.CanViewRawContract = flag4;
		contractHistoryItemResponse.CanViewMaskedContract = canViewMaskedContract;
		contractHistoryItemResponse.CanCreateAppendix = flag5;
		contractHistoryItemResponse.CanTerminateContract = flag5;
		contractHistoryItemResponse.CreatedAt = contract.CreatedAt;
		contractHistoryItemResponse.UpdatedAt = contract.UpdatedAt;
		return contractHistoryItemResponse;
	}

	private static void ResolveCurrentContractTerms(RentalContract contract)
	{
		ResolveContractTerms(contract, null);
	}

	private static (DateOnly StartDate, DateOnly EndDate) ResolveCurrentContractTermValues(RentalContract contract)
	{
		var startDate = contract.StartDate;
		var endDate = contract.EndDate;
		foreach (ContractAppendix appendix in GetActiveAppendicesInOrder(contract))
		{
			foreach (ContractAppendixChange change in appendix.Changes.OrderBy((ContractAppendixChange x) => x.SortOrder))
			{
				if (change.TargetType != ContractAppendixTargetType.Contract ||
					change.ChangeType != ContractAppendixChangeType.Update ||
					string.IsNullOrWhiteSpace(change.NewValue))
				{
					continue;
				}
				switch (NormalizeFieldName(change.FieldName))
				{
				case "startdate":
					if (DateOnly.TryParse(change.NewValue, out var parsedStartDate))
					{
						startDate = parsedStartDate;
					}
					break;
				case "enddate":
					if (DateOnly.TryParse(change.NewValue, out var parsedEndDate))
					{
						endDate = parsedEndDate;
					}
					break;
				}
			}
		}

		return (startDate, endDate);
	}

	private static void ResolveContractTerms(RentalContract contract, ContractAppendix? boundaryAppendix)
	{
		foreach (ContractAppendix item in GetActiveAppendicesInOrder(contract))
		{
			foreach (ContractAppendixChange item2 in item.Changes.OrderBy((ContractAppendixChange x) => x.SortOrder))
			{
				if (item2.TargetType != ContractAppendixTargetType.Contract || item2.ChangeType != ContractAppendixChangeType.Update)
				{
					continue;
				}
				string text = NormalizeFieldName(item2.FieldName);
				if (string.IsNullOrWhiteSpace(item2.NewValue))
				{
					continue;
				}
				switch (text)
				{
				case "monthlyrent":
				{
					if (decimal.TryParse(item2.NewValue, out var result4))
					{
						contract.MonthlyRent = result4;
					}
					break;
				}
				case "depositamount":
				{
					if (decimal.TryParse(item2.NewValue, out var result3))
					{
						contract.DepositAmount = result3;
					}
					break;
				}
				case "paymentday":
				{
					if (int.TryParse(item2.NewValue, out var result5))
					{
						contract.PaymentDay = result5;
					}
					break;
				}
				case "startdate":
				{
					if (DateOnly.TryParse(item2.NewValue, out var result2))
					{
						contract.StartDate = result2;
					}
					break;
				}
				case "enddate":
				{
					if (DateOnly.TryParse(item2.NewValue, out var result))
					{
						contract.EndDate = result;
					}
					break;
				}
				case "maintenantuserid":
				{
					Guid? userId = ExtractUserId(item2.NewValue);
					if (userId.HasValue)
					{
						contract.MainTenantUserId = userId.Value;
						User user = contract.Occupants.FirstOrDefault((ContractOccupant x) => x.UserId == userId.Value)?.User;
						if (user != null)
						{
							contract.MainTenantUser = user;
						}
					}
					break;
				}
				}
			}
			if (boundaryAppendix != null && item.Id == boundaryAppendix.Id)
			{
				break;
			}
		}
	}

	private static ContractOccupantResponse MapOccupantToResponse(ContractOccupant occupant)
	{
		ContractOccupantResponse contractOccupantResponse = new ContractOccupantResponse();
		contractOccupantResponse.Id = occupant.Id;
		contractOccupantResponse.UserId = occupant.UserId;
		contractOccupantResponse.Email = occupant.User?.Email;
		contractOccupantResponse.GuardianOccupantId = occupant.GuardianOccupantId;
		contractOccupantResponse.FullName = occupant.FullName;
		contractOccupantResponse.PhoneNumber = occupant.PhoneNumber;
		contractOccupantResponse.DateOfBirth = occupant.DateOfBirth;
		contractOccupantResponse.RelationshipToMainTenant = occupant.RelationshipToMainTenant;
		contractOccupantResponse.MoveInDate = occupant.MoveInDate;
		contractOccupantResponse.MoveOutDate = occupant.MoveOutDate;
		contractOccupantResponse.Status = occupant.Status.ToString();
		contractOccupantResponse.Document = occupant.Documents.OrderBy((ContractOccupantDocument x) => x.CreatedAt).Select(MapDocumentToResponse).FirstOrDefault();
		return contractOccupantResponse;
	}

	private static ContractOccupantDocumentResponse MapDocumentToResponse(ContractOccupantDocument document)
	{
		return new ContractOccupantDocumentResponse
		{
			Id = document.Id,
			ContractOccupantId = document.RentalContractOccupantId,
			DocumentType = document.DocumentType,
			DocumentNumberMasked = document.DocumentNumberMasked,
			FrontImageObjectKey = document.FrontImageObjectKey,
			BackImageObjectKey = document.BackImageObjectKey,
			ExtraImageObjectKey = document.ExtraImageObjectKey,
			UploadedAt = document.UploadedAt
		};
	}

	private static ContractSignatureResponse MapSignatureToResponse(ContractSignature signature)
	{
		return new ContractSignatureResponse
		{
			Id = signature.Id,
			SignerUserId = signature.SignerUserId,
			SignerRole = signature.SignerRole.ToString(),
			SignatureMethod = signature.SignatureMethod.ToString(),
			SignedAt = signature.SignedAt
		};
	}

	private static void ValidateOccupantsRequest(string tenantEmail, SubmitContractOccupantsRequest request, int maxOccupants)
	{
		if (request.Occupants.Count == 0)
		{
			throw new BadRequestException("RENTAL_CONTRACT_OCCUPANTS_REQUIRED", "Danh sách người ở không được để trống.");
		}
		if (request.Occupants.Count > maxOccupants)
		{
			throw new BadRequestException("RENTAL_REQUEST_OCCUPANT_LIMIT_EXCEEDED", "Số người ở vượt quá sức chứa tối đa đã chốt trong hợp đồng.", new
			{
				request.Occupants.Count,
				maxOccupants
			});
		}
		if (!request.Occupants.Any((ContractOccupantRequest x) => x.Email?.Trim().Equals(tenantEmail.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
		{
			throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Danh sách người ở phải bao gồm người thuê chính.", new { tenantEmail });
		}
		foreach (ContractOccupantRequest occupant in request.Occupants)
		{
			if (string.IsNullOrWhiteSpace(occupant.RelationshipToMainTenant))
			{
				throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở phải có quan hệ với người thuê chính.");
			}
			if (occupant.MoveInDate == default(DateOnly))
			{
				throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở phải có ngày chuyển vào.");
			}
			if (occupant.MoveOutDate.HasValue && occupant.MoveOutDate.Value <= occupant.MoveInDate)
			{
				throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Ngày rời đi phải lớn hơn ngày chuyển vào.");
			}
			if (!string.IsNullOrWhiteSpace(occupant.Email))
			{
				if (occupant.Document != null)
				{
					throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở đã có tài khoản và KYC không được gửi giấy tờ trong hợp đồng.", new { occupant.Email });
				}
				continue;
			}
			if (string.IsNullOrWhiteSpace(occupant.FullName))
			{
				throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở chưa có tài khoản phải có họ tên.");
			}
			if (string.IsNullOrWhiteSpace(occupant.PhoneNumber))
			{
				throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở chưa có tài khoản phải có số điện thoại.");
			}
			if (!occupant.DateOfBirth.HasValue || occupant.DateOfBirth.Value == default(DateOnly))
			{
				throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở chưa có tài khoản phải có ngày sinh.");
			}
			if (occupant.Document == null)
			{
				throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở chưa có tài khoản phải có giấy tờ.");
			}
			if (!string.IsNullOrWhiteSpace(occupant.Document.DocumentType) && !string.IsNullOrWhiteSpace(occupant.Document.DocumentNumber) && !string.IsNullOrWhiteSpace(occupant.Document.FrontImageObjectKey))
			{
				continue;
			}
			throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Giấy tờ người ở phải có loại giấy tờ, số giấy tờ và ảnh mặt trước.");
		}
	}

	private static void ValidateTermsRequest(UpdateContractTermsRequest request)
	{
		if (request.StartDate == default(DateOnly))
		{
			throw new BadRequestException("VALIDATION_ERROR", "Ngày bắt đầu hợp đồng không được để trống.");
		}
		if (request.EndDate == default(DateOnly))
		{
			throw new BadRequestException("VALIDATION_ERROR", "Ngày kết thúc hợp đồng không được để trống.");
		}
		DateOnly dateOnly = DateOnly.FromDateTime(DateTime.UtcNow);
		if (request.StartDate < dateOnly)
		{
			throw new BadRequestException("RENTAL_REQUEST_INVALID_DURATION", "Ngày bắt đầu hợp đồng không được nằm trong quá khứ.");
		}
		if (request.EndDate <= request.StartDate)
		{
			throw new BadRequestException("RENTAL_REQUEST_INVALID_DURATION", "Ngày kết thúc hợp đồng phải lớn hơn ngày bắt đầu.");
		}
		int paymentDay = request.PaymentDay;
		if ((paymentDay < 1 || paymentDay > 28) ? true : false)
		{
			throw new BadRequestException("VALIDATION_ERROR", "Ngày thanh toán phải nằm trong khoảng từ 1 đến 28.");
		}
	}

	private static void ValidateTermsDuration(UpdateContractTermsRequest request, int minRentalMonths, int maxRentalMonths)
	{
		DateOnly dateOnly = request.StartDate.AddMonths(minRentalMonths);
		DateOnly dateOnly2 = request.StartDate.AddMonths(maxRentalMonths);
		if (request.EndDate < dateOnly || request.EndDate > dateOnly2)
		{
			throw new BadRequestException("RENTAL_REQUEST_INVALID_DURATION", "Thời hạn hợp đồng không nằm trong chính sách thuê của khu trọ.", new { request.StartDate, request.EndDate, minRentalMonths, maxRentalMonths });
		}
	}

	private async Task<Dictionary<string, VerifiedOccupantAccount>> ValidateOccupantAccountsAsync(SubmitContractOccupantsRequest request, CancellationToken cancellationToken)
	{
		List<string> emails = (from x in request.Occupants
			where !string.IsNullOrWhiteSpace(x.Email)
			select x.Email.Trim().ToLowerInvariant()).Distinct().ToList();
		if (emails.Count == 0)
		{
			return new Dictionary<string, VerifiedOccupantAccount>();
		}
		List<User> users = await (from x in context.Users.AsNoTracking().Include((User x) => x.UserProfile)
			where emails.Contains(x.Email.ToLower()) && x.DeletedAt == null
			select x).ToListAsync(cancellationToken);
		List<string> missingEmails = Enumerable.Except(second: users.Select((User x) => x.Email.ToLowerInvariant()).ToList(), first: emails).ToList();
		if (missingEmails.Count > 0)
		{
			throw new BadRequestException("RENTAL_CONTRACT_INVALID_OCCUPANT", "Người ở có tài khoản không tồn tại.", new
			{
				emails = missingEmails
			});
		}
		List<Guid> userIdsForKyc = users.Select((User x) => x.Id).ToList();
		Dictionary<Guid, KycVerification> latestApprovedKycByUserId = (from x in await (from x in context.KycVerifications.AsNoTracking()
				where userIdsForKyc.Contains(x.UserId) && (int)x.Status == 4
				select x).ToListAsync(cancellationToken)
			group x by x.UserId).ToDictionary((IGrouping<Guid, KycVerification> x) => x.Key, (IGrouping<Guid, KycVerification> x) => x.OrderByDescending((KycVerification k) => k.ReviewedAt ?? k.UpdatedAt).First());
		List<Guid> notApprovedKycUserIds = userIdsForKyc.Except(latestApprovedKycByUserId.Keys).ToList();
		if (notApprovedKycUserIds.Count > 0)
		{
			List<string> notApprovedEmails = (from u in users
				where notApprovedKycUserIds.Contains(u.Id)
				select u.Email).ToList();
			throw new BadRequestException("KYC_REQUIRED", "Người ở có tài khoản phải hoàn tất KYC trước khi được thêm vào hợp đồng.", new
			{
				emails = notApprovedEmails
			});
		}
		Dictionary<string, VerifiedOccupantAccount> result = new Dictionary<string, VerifiedOccupantAccount>();
		foreach (User user in users)
		{
			KycVerification approvedKyc = latestApprovedKycByUserId[user.Id];
			string fullName = NormalizeOptionalText(approvedKyc.OcrFullName) ?? NormalizeOptionalText(user.UserProfile?.FullName) ?? NormalizeOptionalText(user.DisplayName);
			DateOnly? dateOfBirth = approvedKyc.OcrDateOfBirth ?? user.UserProfile?.DateOfBirth;
			if (string.IsNullOrWhiteSpace(fullName) || !dateOfBirth.HasValue)
			{
				throw new BadRequestException("KYC_REQUIRED", "Thông tin KYC đã duyệt của người ở chưa đủ họ tên hoặc ngày sinh.", new
				{
					email = user.Email
				});
			}
			result[user.Email.ToLowerInvariant()] = new VerifiedOccupantAccount(user.Id, fullName, NormalizeOptionalText(user.PhoneNumber), dateOfBirth.Value);
		}
		return result;
	}

	private static void EnsureCanSubmitOccupants(RentalContract contract)
	{
		RentalContractStatus status = contract.Status;
		if ((status == RentalContractStatus.WaitingTenantOccupants || status == RentalContractStatus.LandlordRevisionRequested) ? true : false)
		{
			return;
		}
		throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Trạng thái hợp đồng không cho phép cập nhật thông tin người ở.", new
		{
			Id = contract.Id,
			currentStatus = contract.Status.ToString()
		});
	}

	private static void EnsureCanLandlordSign(RentalContract contract)
	{
		RentalContractStatus status = contract.Status;
		if ((status == RentalContractStatus.PendingLandlordSignature || status == RentalContractStatus.TenantRevisionRequested) ? true : false)
		{
			return;
		}
		throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Trạng thái hợp đồng không cho phép chủ trọ ký.", new
		{
			Id = contract.Id,
			currentStatus = contract.Status.ToString()
		});
	}

	private static void EnsureContractCanPreview(RentalContract contract)
	{
		if (contract.Status != RentalContractStatus.Active)
		{
			return;
		}
		throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Hợp đồng đã có hiệu lực, vui lòng xem file hợp đồng đã ký.", new
		{
			Id = contract.Id,
			currentStatus = contract.Status.ToString()
		});
	}

	private static void EnsureLandlordCanReject(RentalContract contract)
	{
		RentalContractStatus status = contract.Status;
		if ((status == RentalContractStatus.PendingLandlordSignature || status == RentalContractStatus.TenantRevisionRequested) ? true : false)
		{
			return;
		}
		throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Trạng thái hợp đồng không cho phép chủ trọ từ chối.", new
		{
			Id = contract.Id,
			currentStatus = contract.Status.ToString()
		});
	}

	private static void EnsureCanView(Guid userId, RentalContract contract)
	{
		if (contract.Room.RoomingHouse.LandlordUserId == userId || GetCurrentMainTenantUserId(contract) == userId)
		{
			return;
		}
		throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Bạn không có quyền xem hợp đồng này.", new { contract.Id });
	}

	private static void EnsureMainTenant(Guid userId, RentalContract contract)
	{
		if (GetCurrentMainTenantUserId(contract) == userId)
		{
			return;
		}
		throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Bạn không có quyền thao tác trên hợp đồng này.", new { contract.Id });
	}

	private static void EnsureLandlord(Guid landlordUserId, RentalContract contract)
	{
		if (contract.Room.RoomingHouse.LandlordUserId == landlordUserId)
		{
			return;
		}
		throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Bạn không có quyền thao tác trên hợp đồng này.", new { contract.Id });
	}

	private static void EnsureStatus(RentalContract contract, RentalContractStatus expectedStatus)
	{
		if (contract.Status == expectedStatus)
		{
			return;
		}
		throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Trạng thái hợp đồng không hợp lệ cho thao tác này.", new
		{
			Id = contract.Id,
			currentStatus = contract.Status.ToString(),
			expectedStatus = expectedStatus.ToString()
		});
	}

	private static void EnsureNotSigned(RentalContract contract, ContractSignerRole signerRole)
	{
		if (!contract.Signatures.Any((ContractSignature x) => x.SignerRole == signerRole))
		{
			return;
		}
		throw new ConflictException("RENTAL_CONTRACT_ALREADY_SIGNED", "Bên này đã ký hợp đồng.", new
		{
			Id = contract.Id,
			signerRole = signerRole.ToString()
		});
	}

	private string? HashDocumentNumber(string? documentNumber)
	{
		return string.IsNullOrWhiteSpace(documentNumber) ? null : hashService.HashSha256Hex(documentNumber.Trim());
	}

	private string? EncryptDocumentNumber(string? documentNumber)
	{
		return string.IsNullOrWhiteSpace(documentNumber) ? null : sensitiveDataProtector.Encrypt(documentNumber);
	}

	private static ContractAppendix? ResolveHistorySnapshotBoundaryAppendix(RentalContract contract, Guid userId)
	{
		Guid guid = contract.MainTenantUserId;
		foreach (ContractAppendix item in GetActiveAppendicesInOrder(contract))
		{
			Guid guid2 = guid;
			bool flag = item.Changes.Any((ContractAppendixChange change) => change.TargetType == ContractAppendixTargetType.ContractOccupant && change.ChangeType == ContractAppendixChangeType.Remove && contract.Occupants.Any(delegate(ContractOccupant occupant)
			{
				Guid id = occupant.Id;
				Guid? targetId = change.TargetId;
				return id == targetId && occupant.UserId == userId;
			}));
			foreach (ContractAppendixChange item2 in item.Changes.OrderBy((ContractAppendixChange x) => x.SortOrder))
			{
				if (!IsMainTenantUserIdChange(item2))
				{
					continue;
				}
				Guid? guid3 = ExtractUserId(item2.NewValue);
				if (guid3.HasValue)
				{
					if (guid2 == userId && guid3.Value != userId)
					{
						return item;
					}
					guid = guid3.Value;
				}
			}
			if (flag)
			{
				return item;
			}
		}
		return null;
	}

	private static IEnumerable<ContractOccupant> ResolveOccupantsForHistorySnapshot(RentalContract contract, ContractAppendix? boundaryAppendix)
	{
		if (boundaryAppendix == null)
		{
			return contract.Occupants;
		}
		HashSet<Guid> excludedOccupantIds = new HashSet<Guid>();
		foreach (ContractAppendix item in GetActiveAppendicesAfter(contract, boundaryAppendix))
		{
			foreach (ContractAppendixChange change in item.Changes.OrderBy((ContractAppendixChange x) => x.SortOrder))
			{
				if (change.TargetType != ContractAppendixTargetType.ContractOccupant)
				{
					continue;
				}
				if (change.ChangeType == ContractAppendixChangeType.Add && change.TargetId.HasValue)
				{
					excludedOccupantIds.Add(change.TargetId.Value);
				}
				else if (change.ChangeType == ContractAppendixChangeType.Remove && change.TargetId.HasValue)
				{
					ContractOccupant contractOccupant = contract.Occupants.FirstOrDefault((ContractOccupant x) => x.Id == change.TargetId.Value);
					if (contractOccupant != null)
					{
						contractOccupant.Status = ContractOccupantStatus.Active;
						contractOccupant.MoveOutDate = null;
					}
				}
			}
		}
		return contract.Occupants.Where((ContractOccupant x) => !excludedOccupantIds.Contains(x.Id));
	}

	private static string ResolveCurrentUserRelation(bool isMainTenant, bool isFormerMainTenant, bool isCoTenant, bool isFormerCoTenant, bool hasOccupantRecord)
	{
		if (isMainTenant)
		{
			return "CurrentMainTenant";
		}
		if (isFormerMainTenant)
		{
			return "FormerMainTenant";
		}
		if (isCoTenant)
		{
			return "CoTenant";
		}
		if (isFormerCoTenant)
		{
			return "FormerCoTenant";
		}
		return hasOccupantRecord ? "FormerOccupant" : "HistoricalParticipant";
	}

	private static string NormalizeRequiredReason(string? reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new BadRequestException("VALIDATION_ERROR", "Lý do không được để trống.");
		}
		return reason.Trim();
	}

	private static void EnsureContractStartDateAllowsLandlordSignature(DateOnly startDate, DateTimeOffset now)
	{
		var today = DateOnly.FromDateTime(now.UtcDateTime);
		var minimumStartDate = today.AddDays(LandlordSignatureMinimumStartOffsetDays);
		if (startDate < minimumStartDate)
		{
			throw new BadRequestException(
				"VALIDATION_ERROR",
				"Ngày bắt đầu hợp đồng phải còn cách hôm nay ít nhất 2 ngày để người thuê có thời gian ký hợp đồng.");
		}
	}

	private static void EnsureTenantCanSignBeforeStartDate(DateOnly startDate, DateOnly today)
	{
		if (today > startDate)
		{
			throw new BadRequestException(
				"VALIDATION_ERROR",
				"Hợp đồng đã quá ngày bắt đầu thuê nên không thể ký.");
		}
	}

	private static void EnsureTenantSignatureDeadlineNotExpired(RentalContract contract, DateTimeOffset now)
	{
		if (contract.SignatureDeadlineAt.HasValue && contract.SignatureDeadlineAt.Value <= now)
		{
			throw new BadRequestException(
				"VALIDATION_ERROR",
				"Hợp đồng đã quá hạn ký. Vui lòng liên hệ chủ trọ để xử lý.");
		}
	}

	private static string? MaskDocumentNumber(string? documentNumber)
	{
		if (string.IsNullOrWhiteSpace(documentNumber))
		{
			return null;
		}
		string text = documentNumber.Trim();
		if (text.Length <= 4)
		{
			return new string('*', text.Length);
		}
		return new string('*', text.Length - 4) + text.Substring(text.Length - 4);
	}

	private static string? NormalizeOptionalText(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}

	private static ContractOccupantStatus ResolveMoveInStatus(DateOnly moveInDate, DateOnly today)
	{
		return moveInDate <= today ? ContractOccupantStatus.Active : ContractOccupantStatus.PendingMoveIn;
	}

	private static Guid GetCurrentMainTenantUserId(RentalContract contract)
	{
		Guid result = contract.MainTenantUserId;
		foreach (ContractAppendix item in GetActiveAppendicesInOrder(contract))
		{
			foreach (ContractAppendixChange item2 in item.Changes.OrderBy((ContractAppendixChange x) => x.SortOrder))
			{
				if (IsMainTenantUserIdChange(item2))
				{
					Guid? guid = ExtractUserId(item2.NewValue);
					if (guid.HasValue)
					{
						result = guid.Value;
					}
				}
			}
		}
		return result;
	}

	private static IReadOnlyCollection<Guid> GetMainTenantUserIds(RentalContract contract)
	{
		HashSet<Guid> hashSet = new HashSet<Guid> { contract.MainTenantUserId };
		foreach (ContractAppendix item in GetActiveAppendicesInOrder(contract))
		{
			foreach (ContractAppendixChange item2 in item.Changes.OrderBy((ContractAppendixChange x) => x.SortOrder))
			{
				if (IsMainTenantUserIdChange(item2))
				{
					Guid? guid = ExtractUserId(item2.NewValue);
					if (guid.HasValue)
					{
						hashSet.Add(guid.Value);
					}
				}
			}
		}
		return hashSet;
	}

	private static bool IsMainTenantUserIdChange(ContractAppendixChange change)
	{
		return change.TargetType == ContractAppendixTargetType.Contract && change.ChangeType == ContractAppendixChangeType.Update && NormalizeFieldName(change.FieldName) == "maintenantuserid";
	}

	private static IEnumerable<ContractAppendix> GetActiveAppendicesInOrder(RentalContract contract)
	{
		return from x in contract.Appendices
			where x.AppliedAt.HasValue && (x.Status == ContractAppendixStatus.Active || x.Status == ContractAppendixStatus.Cancelled)
			orderby x.AppliedAt ?? x.ActivatedAt ?? x.UpdatedAt, x.CreatedAt
			select x;
	}

	private static IEnumerable<ContractAppendix> GetActiveAppendicesAfter(RentalContract contract, ContractAppendix boundaryAppendix)
	{
		bool foundBoundary = false;
		foreach (ContractAppendix appendix in GetActiveAppendicesInOrder(contract))
		{
			if (foundBoundary)
			{
				yield return appendix;
			}
			else
			{
				foundBoundary = appendix.Id == boundaryAppendix.Id;
			}
		}
	}

	private static Guid? ExtractUserId(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		string input = value.Trim().Trim('"');
		if (Guid.TryParse(input, out var result))
		{
			return result;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(value);
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.ValueKind == JsonValueKind.String && Guid.TryParse(rootElement.GetString(), out var result2))
			{
				return result2;
			}
			if (rootElement.ValueKind == JsonValueKind.Object && rootElement.TryGetProperty("userId", out var value2) && value2.ValueKind == JsonValueKind.String && Guid.TryParse(value2.GetString(), out var result3))
			{
				return result3;
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
		return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
	}

	private static int GetSnapshotMaxOccupants(RentalContract contract)
	{
		if (string.IsNullOrWhiteSpace(contract.RoomSnapshot))
		{
			return contract.Room.MaxOccupants;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(contract.RoomSnapshot);
			if (jsonDocument.RootElement.TryGetProperty("MaxOccupants", out var value) && value.TryGetInt32(out var value2) && value2 > 0)
			{
				return value2;
			}
		}
		catch (JsonException)
		{
			return contract.Room.MaxOccupants;
		}
		return contract.Room.MaxOccupants;
	}
}
