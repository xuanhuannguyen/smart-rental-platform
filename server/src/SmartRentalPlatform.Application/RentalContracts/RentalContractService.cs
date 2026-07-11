using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
	private static readonly TimeSpan TenantSignatureTtl = TimeSpan.FromHours(48);

	private readonly IAppDbContext context;

	private readonly IContractPdfRenderer contractPdfRenderer;

	private readonly IContractSignatureOtpService contractSignatureOtpService;

	private readonly IContractFileService contractFileService;

	private readonly RentalContractPreviewBuilder previewBuilder;

	private readonly RentalContractOccupantValidator occupantValidator;

	private readonly RentalContractDocumentHelper documentHelper;

	private readonly RentalContractFinalInvoiceStatusResolver finalInvoiceStatusResolver;

	private readonly IBillingService billingService;

	private readonly IWalletService walletService;

	private readonly IPaymentRowLockService rowLockService;

	private readonly IContractDocumentModelFactory contractDocumentModelFactory;

	public RentalContractService(IAppDbContext context, IContractPdfRenderer contractPdfRenderer, IContractSignatureOtpService contractSignatureOtpService, IContractFileService contractFileService, RentalContractPreviewBuilder previewBuilder, RentalContractOccupantValidator occupantValidator, RentalContractDocumentHelper documentHelper, RentalContractFinalInvoiceStatusResolver finalInvoiceStatusResolver, IBillingService billingService, IWalletService walletService, IPaymentRowLockService rowLockService, IContractDocumentModelFactory contractDocumentModelFactory)
	{
		this.context = context;
		this.contractPdfRenderer = contractPdfRenderer;
		this.contractSignatureOtpService = contractSignatureOtpService;
		this.contractFileService = contractFileService;
		this.previewBuilder = previewBuilder;
		this.occupantValidator = occupantValidator;
		this.documentHelper = documentHelper;
		this.finalInvoiceStatusResolver = finalInvoiceStatusResolver;
		this.billingService = billingService;
		this.walletService = walletService;
		this.rowLockService = rowLockService;
		this.contractDocumentModelFactory = contractDocumentModelFactory;
	}

	public async Task<ContractDetailResponse?> GetByIdAsync(Guid userId, Guid contractId, CancellationToken cancellationToken = default(CancellationToken))
	{
		RentalContract contract = await BaseQuery().AsNoTracking().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		RentalContractStateGuard.EnsureCanView(userId, contract, RentalContractResponseMapper.GetCurrentMainTenantUserId(contract));
		ContractDetailResponse response = RentalContractResponseMapper.ToDetailResponse(contract);
		response.IsAwaitingFinalInvoice = await finalInvoiceStatusResolver.IsAwaitingFinalInvoiceAsync(contract, cancellationToken);
		return response;
	}

	public async Task<ContractDetailResponse?> GetActiveContractByRoomIdAsync(Guid landlordUserId, Guid roomId, CancellationToken cancellationToken = default(CancellationToken))
	{
		RentalContract contract = await BaseQuery().AsNoTracking().FirstOrDefaultAsync((RentalContract x) => x.RoomId == roomId && x.DeletedAt == null && x.Status == RentalContractStatus.Active && x.Room.RoomingHouse.LandlordUserId == landlordUserId, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		RentalContractStateGuard.EnsureLandlord(landlordUserId, contract);
		return RentalContractResponseMapper.ToDetailResponse(contract);
	}

	public async Task<IReadOnlyCollection<ContractOccupantResponse>?> GetActiveTenantsByRoomIdAsync(Guid landlordUserId, Guid roomId, CancellationToken cancellationToken = default(CancellationToken))
	{
		RentalContract contract = await BaseQuery().AsNoTracking().FirstOrDefaultAsync((RentalContract x) => x.RoomId == roomId && x.DeletedAt == null && x.Status == RentalContractStatus.Active && x.Room.RoomingHouse.LandlordUserId == landlordUserId, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		RentalContractStateGuard.EnsureLandlord(landlordUserId, contract);
		RentalContractResponseMapper.ApplyCurrentContractTerms(contract);
		return contract.Occupants.OrderBy((ContractOccupant x) => x.CreatedAt).Select(RentalContractResponseMapper.ToOccupantResponse).ToList();
	}

	public async Task<IReadOnlyCollection<ContractHistoryItemResponse>> GetMyHistoryAsync(Guid tenantUserId, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<RentalContract> contracts = await (from x in BaseQuery().AsNoTracking()
			where x.DeletedAt == null && x.ActivatedAt != null && (x.Status == RentalContractStatus.Active || x.Status == RentalContractStatus.Expired || x.Status == RentalContractStatus.Cancelled) && (x.MainTenantUserId == tenantUserId || x.Occupants.Any((ContractOccupant occupant) => occupant.UserId == tenantUserId))
			orderby x.ActivatedAt ?? x.UpdatedAt descending, x.CreatedAt descending
			select x).ToListAsync(cancellationToken);
		HashSet<Guid> awaitingIds = await finalInvoiceStatusResolver.GetAwaitingFinalInvoiceContractIdsAsync(contracts, cancellationToken);
		return contracts.Select(contract =>
		{
			ContractHistoryItemResponse response = RentalContractResponseMapper.ToHistoryItemResponse(contract, tenantUserId);
			response.IsAwaitingFinalInvoice = awaitingIds.Contains(contract.Id);
			return response;
		}).ToList();
	}

	public async Task<IReadOnlyCollection<ContractHistoryItemResponse>> GetLandlordContractsAsync(Guid landlordUserId, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<RentalContract> contracts = await (from x in BaseQuery().AsNoTracking()
			where x.DeletedAt == null && x.ActivatedAt != null && (x.Status == RentalContractStatus.Active || x.Status == RentalContractStatus.Expired || x.Status == RentalContractStatus.Cancelled) && x.Room.RoomingHouse.LandlordUserId == landlordUserId
			orderby x.ActivatedAt ?? x.UpdatedAt descending, x.CreatedAt descending
			select x).ToListAsync(cancellationToken);
		HashSet<Guid> awaitingIds = await finalInvoiceStatusResolver.GetAwaitingFinalInvoiceContractIdsAsync(contracts, cancellationToken);
		return contracts.Select(contract =>
		{
			ContractHistoryItemResponse response = RentalContractResponseMapper.ToHistoryItemResponse(contract, landlordUserId);
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
		RentalContractStateGuard.EnsureContractCanPreview(contract);
		ContractPreviewViewerAccess viewerAccess = RentalContractPreviewBuilder.ResolveViewerAccess(userId, contract);
		if ((object)viewerAccess == null)
		{
			throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Bạn không có quyền xem bản xem trước hợp đồng này.", new { contract.Id });
		}
		ContractRenderOptions renderOptions = await previewBuilder.BuildAsync(contract, viewerAccess, cancellationToken);
		ContractDocumentModel documentModel = await contractDocumentModelFactory.BuildAsync(
			contract,
			ContractDocumentBuildMode.ExistingSnapshotOrLive,
			null,
			cancellationToken);
		byte[] pdfBytes = contractPdfRenderer.RenderSignedRentalContract(documentModel, renderOptions);
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
		RentalContractStateGuard.EnsureMainTenant(tenantUserId, contract, RentalContractResponseMapper.GetCurrentMainTenantUserId(contract));
		RentalContractStateGuard.EnsureCanSubmitOccupants(contract);
		var (resolvedStartDate, resolvedEndDate) = RentalContractResponseMapper.ResolveCurrentContractTermValues(contract);
		RentalContractOccupantValidator.ValidateOccupantsRequest(contract.MainTenantUser.Email, request, RentalContractResponseMapper.GetSnapshotMaxOccupants(contract), resolvedStartDate, resolvedEndDate);
		Dictionary<string, VerifiedOccupantAccount> verifiedAccounts = await occupantValidator.ValidateOccupantAccountsAsync(request, cancellationToken);
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
						PhoneNumber = (verifiedAccount?.PhoneNumber ?? RentalContractTextHelper.NormalizeOptionalText(occupantRequest.PhoneNumber)),
						DateOfBirth = (verifiedAccount?.DateOfBirth ?? occupantRequest.DateOfBirth.Value),
						RelationshipToMainTenant = RentalContractTextHelper.NormalizeOptionalText(occupantRequest.RelationshipToMainTenant),
						MoveInDate = occupantRequest.MoveInDate,
						MoveOutDate = occupantRequest.MoveOutDate,
						Status = RentalContractLifecycleHelper.ResolveMoveInStatus(occupantRequest.MoveInDate, today),
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
							DocumentNumberMasked = RentalContractDocumentHelper.MaskDocumentNumber(documentRequest.DocumentNumber),
							DocumentNumberHash = documentHelper.HashDocumentNumber(documentRequest.DocumentNumber),
							DocumentNumberEncrypted = documentHelper.EncryptDocumentNumber(documentRequest.DocumentNumber),
							FrontImageObjectKey = documentRequest.FrontImageObjectKey.Trim(),
							BackImageObjectKey = RentalContractTextHelper.NormalizeOptionalText(documentRequest.BackImageObjectKey),
							ExtraImageObjectKey = RentalContractTextHelper.NormalizeOptionalText(documentRequest.ExtraImageObjectKey),
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
		RentalContractTermsValidator.ValidateRequest(request);
		RentalContract contract = await BaseQuery().AsNoTracking().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		RentalContractStateGuard.EnsureLandlord(landlordUserId, contract);
		RentalContractStateGuard.EnsureStatus(contract, RentalContractStatus.TenantRevisionRequested);
		RentalPolicy rentalPolicy = await context.RentalPolicies.AsNoTracking().FirstOrDefaultAsync((RentalPolicy x) => x.RoomingHouseId == contract.Room.RoomingHouseId && x.IsActive, cancellationToken);
		if (rentalPolicy == null)
		{
			throw new ConflictException("RENTAL_POLICY_REQUIRED", "Khu trọ chưa cấu hình chính sách thuê.", new { contract.Room.RoomingHouseId });
		}
		RentalContractTermsValidator.ValidateDuration(request, rentalPolicy.MinRentalMonths, rentalPolicy.MaxRentalMonths);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		if (await context.RentalContracts.Where((RentalContract x) => x.Id == contractId && x.DeletedAt == null && x.Status == RentalContractStatus.TenantRevisionRequested).ExecuteUpdateAsync(delegate(UpdateSettersBuilder<RentalContract> setters)
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
		RentalContractStateGuard.EnsureLandlord(landlordUserId, contract);
		RentalContractStateGuard.EnsureCanLandlordSign(contract);
		RentalContractStateGuard.EnsureNotSigned(contract, ContractSignerRole.Landlord);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		RentalContractStateGuard.EnsureContractStartDateAllowsLandlordSignature(contract.StartDate, now);
		ContractDetailResponse result;
		await using (IAppDbContextTransaction transaction = await context.BeginTransactionAsync(cancellationToken))
		{
			try
			{
				await contractSignatureOtpService.VerifyAndConsumeOtpAsync(landlordUserId, contractId, ContractSignerRole.Landlord, request.Otp, cancellationToken);
				context.ContractSignatures.Add(RentalContractSignatureFactory.Create(contract.Id, landlordUserId, ContractSignerRole.Landlord, request, ipAddress, userAgent, now));
				if (await context.RentalContracts.Where((RentalContract x) => x.Id == contractId && x.DeletedAt == null && (x.Status == RentalContractStatus.PendingLandlordSignature || x.Status == RentalContractStatus.TenantRevisionRequested)).ExecuteUpdateAsync(delegate(UpdateSettersBuilder<RentalContract> setters)
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
		RentalContractStateGuard.EnsureMainTenant(tenantUserId, contract, RentalContractResponseMapper.GetCurrentMainTenantUserId(contract));
		RentalContractStateGuard.EnsureStatus(contract, RentalContractStatus.PendingTenantSignature);
		RentalContractStateGuard.EnsureNotSigned(contract, ContractSignerRole.Tenant);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
		RentalContractStateGuard.EnsureTenantCanSignBeforeStartDate(contract.StartDate, today);
		RentalContractStateGuard.EnsureTenantSignatureDeadlineNotExpired(contract, now);
		RoomStatus roomStatusAfterSigning = contract.StartDate <= today ? RoomStatus.Occupied : RoomStatus.Reserved;
		ContractDetailResponse result;
		await using (IAppDbContextTransaction transaction = await context.BeginTransactionAsync(cancellationToken))
		{
			try
			{
				await contractSignatureOtpService.VerifyAndConsumeOtpAsync(tenantUserId, contractId, ContractSignerRole.Tenant, request.Otp, cancellationToken);
				context.ContractSignatures.Add(RentalContractSignatureFactory.Create(contract.Id, tenantUserId, ContractSignerRole.Tenant, request, ipAddress, userAgent, now));
				if (await context.RentalContracts.Where((RentalContract x) => x.Id == contractId && x.DeletedAt == null && x.Status == RentalContractStatus.PendingTenantSignature).ExecuteUpdateAsync(delegate(UpdateSettersBuilder<RentalContract> setters)
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
		string reason = RentalContractTextHelper.NormalizeRequiredReason(request.Reason);
		RentalContract contract = await BaseQuery().FirstOrDefaultAsync((RentalContract x) => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		if (contract == null)
		{
			return null;
		}
		DateTimeOffset now = DateTimeOffset.UtcNow;
		bool isLandlord = contract.Room.RoomingHouse.LandlordUserId == userId;
		if (isLandlord)
		{
			RentalContractStateGuard.EnsureLandlordCanReject(contract);
		}
		else
		{
			RentalContractStateGuard.EnsureMainTenant(userId, contract, RentalContractResponseMapper.GetCurrentMainTenantUserId(contract));
			RentalContractStateGuard.EnsureStatus(contract, RentalContractStatus.PendingTenantSignature);
		}

		RoomDeposit deposit = contract.RoomDeposit;
		RentalContractLifecycleHelper.EnsureDepositReadyForSettlement(deposit);
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
				RentalContractStateGuard.EnsureLandlordCanReject(contract);
				RentalContractLifecycleHelper.EnsureDepositReadyForSettlement(deposit);

				var settlementGroupId = Guid.NewGuid();
				await walletService.TransferFromReservedWithinTransactionAsync(
					landlordWallet.Id,
					tenantWallet.Id,
					deposit.DepositAmount,
					deposit.DepositAmount,
					WalletTransactionType.DepositRefundDebit,
					WalletTransactionType.DepositRefundCredit,
					RentalContractLifecycleHelper.CreateDepositSettlementMetadata(deposit, settlementGroupId, "Landlord contract rejection deposit refund."),
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
				RentalContractStateGuard.EnsureMainTenant(userId, contract, RentalContractResponseMapper.GetCurrentMainTenantUserId(contract));
				RentalContractStateGuard.EnsureStatus(contract, RentalContractStatus.PendingTenantSignature);
				RentalContractLifecycleHelper.EnsureDepositReadyForSettlement(deposit);

				var settlementGroupId = Guid.NewGuid();
				await walletService.ReleaseReservedWithinTransactionAsync(
					landlordWallet.Id,
					deposit.DepositAmount,
					WalletTransactionType.DepositForfeitRelease,
					RentalContractLifecycleHelper.CreateDepositSettlementMetadata(deposit, settlementGroupId, "Tenant contract rejection deposit forfeiture."),
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
		string reason = RentalContractTextHelper.NormalizeRequiredReason(request.Reason);
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
			RentalContractStateGuard.EnsureStatus(contract, RentalContractStatus.PendingLandlordSignature);
			contract.Status = RentalContractStatus.LandlordRevisionRequested;
			contract.StatusReason = reason;
			contract.SignatureDeadlineAt = null;
			contract.UpdatedAt = now;
			await context.SaveChangesAsync(cancellationToken);
			return await GetByIdAsync(userId, contract.Id, cancellationToken);
		}
		RentalContractStateGuard.EnsureMainTenant(userId, contract, RentalContractResponseMapper.GetCurrentMainTenantUserId(contract));
		RentalContractStateGuard.EnsureStatus(contract, RentalContractStatus.PendingTenantSignature);
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
		bool isTenant = RentalContractResponseMapper.GetCurrentMainTenantUserId(contract) == userId;
		if (!isLandlord && !isTenant)
		{
			throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Bạn không có quyền thao tác với hợp đồng này.", new { contractId });
		}
		RentalContractStateGuard.EnsureStatus(contract, RentalContractStatus.Active);
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
		var currentTerms = RentalContractResponseMapper.ResolveCurrentContractTermValues(contract);
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
		RentalContractLifecycleHelper.EnsureDepositReadyForSettlement(deposit);
		var landlordWallet = await walletService.GetOrCreateWalletAsync(deposit.LandlordUserId, cancellationToken);
		var tenantWallet = await walletService.GetOrCreateWalletAsync(deposit.TenantUserId, cancellationToken);
		string reasonText = (string.IsNullOrWhiteSpace(request.Reason) ? string.Empty : request.Reason.Trim());
		await using IAppDbContextTransaction transaction = await context.BeginTransactionAsync(cancellationToken);

		await rowLockService.LockRentalContractAsync(contractId, cancellationToken);
		await rowLockService.LockRoomDepositAsync(deposit.Id, cancellationToken);
		contract = await BaseQuery().FirstAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);
		deposit = contract.RoomDeposit;
		isLandlord = contract.Room.RoomingHouse.LandlordUserId == userId;
		isTenant = RentalContractResponseMapper.GetCurrentMainTenantUserId(contract) == userId;
		if (!isLandlord && !isTenant)
		{
			throw new ForbiddenException(ErrorCodes.RentalContractForbidden, "Bạn không có quyền thao tác với hợp đồng này.", new { contractId });
		}
		RentalContractStateGuard.EnsureStatus(contract, RentalContractStatus.Active);
		RentalContractLifecycleHelper.EnsureDepositReadyForSettlement(deposit);

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
				RentalContractLifecycleHelper.CreateDepositSettlementMetadata(deposit, settlementGroupId, "Landlord unilateral termination deposit refund."),
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
				RentalContractLifecycleHelper.CreateDepositSettlementMetadata(deposit, settlementGroupId, "Tenant unilateral termination deposit forfeiture."),
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
					RentalContractLifecycleHelper.CreateDepositSettlementMetadata(deposit, settlementGroupId, "Contract termination deposit refund."),
					cancellationToken);
			}
			if (damageFee > 0m)
			{
				await walletService.ReleaseReservedWithinTransactionAsync(
					landlordWallet.Id,
					damageFee,
					WalletTransactionType.DepositForfeitRelease,
					RentalContractLifecycleHelper.CreateDepositSettlementMetadata(deposit, settlementGroupId, "Contract termination retained deposit release."),
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
		RentalContractLifecycleHelper.CancelOpenAppendices(contract, now);
		RentalContractLifecycleHelper.CloseContractOccupants(contract, terminationDate, today, now);
		RentalContractLifecycleHelper.MarkRoomAvailableIfReservedOrOccupied(contract, now);
		await context.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return await GetByIdAsync(userId, contract.Id, cancellationToken);
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

					RentalContractLifecycleHelper.EnsureDepositReadyForSettlement(contract.RoomDeposit);
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
						RentalContractLifecycleHelper.CreateDepositSettlementMetadata(contract.RoomDeposit, settlementGroupId, "Tenant signature deadline deposit forfeiture."),
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

}
