using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.Billing;

public sealed class BillingWorkflowGuard
{
    private readonly IAppDbContext context;
    private readonly IBillingContractReadService contractReadService;

    public BillingWorkflowGuard(
        IAppDbContext context,
        IBillingContractReadService contractReadService)
    {
        this.context = context;
        this.contractReadService = contractReadService;
    }

    internal static void EnsureLandlordCanViewInvoice(Invoice invoice, Guid landlordUserId)
    {
        if (invoice.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền xem hóa đơn này.");
        }
    }

    internal static void EnsureLandlordCanIssueInvoice(Invoice invoice, Guid landlordUserId)
    {
        if (invoice.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền phát hành hóa đơn này.");
        }

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Chỉ có thể phát hành hóa đơn nháp (Draft).");
        }
    }

    internal static void EnsureLandlordCanCancelInvoice(Invoice invoice, Guid landlordUserId)
    {
        if (invoice.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền hủy hóa đơn này.");
        }

        if (invoice.Status == InvoiceStatus.Paid ||
            invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Chỉ có thể hủy hóa đơn chưa thanh toán.");
        }
    }

    internal async Task<BillingContractSnapshot> GetOwnedActiveContractAsync(
        Guid landlordUserId,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        var contract = await contractReadService.GetActiveContractAsync(contractId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RentalContractNotFound, "Không tìm thấy hợp đồng đang hoạt động.");

        EnsureLandlordCanAccessContract(contract, landlordUserId);
        return contract;
    }

    internal async Task<BillingContractSnapshot> GetOwnedTerminationBillingContractAsync(
        Guid landlordUserId,
        Guid contractId,
        DateOnly terminationDate,
        bool allowActiveContract,
        CancellationToken cancellationToken)
    {
        var contract = await contractReadService.GetContractAsync(contractId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RentalContractNotFound, "Không tìm thấy hợp đồng.");

        EnsureLandlordCanAccessContract(contract, landlordUserId);

        if (allowActiveContract && contract.Status == RentalContractStatus.Active)
        {
            return contract;
        }

        if (contract.Status != RentalContractStatus.Cancelled ||
            contract.TerminationType != ContractTerminationType.TenantUnilateral ||
            !contract.TerminationDate.HasValue ||
            contract.TerminationDate.Value != terminationDate ||
            terminationDate < contract.StartDate)
        {
            throw new ConflictException(
                ErrorCodes.FinalInvoiceNotAllowed,
                "Hợp đồng không thuộc trường hợp được tạo hóa đơn sau khi chấm dứt.");
        }

        return contract;
    }

    internal async Task EnsureRoomingHouseOwnerAsync(
        Guid landlordUserId,
        Guid roomingHouseId,
        CancellationToken cancellationToken)
    {
        var ownsHouse = await context.RoomingHouses.AnyAsync(
            x => x.Id == roomingHouseId &&
                 x.LandlordUserId == landlordUserId &&
                 x.DeletedAt == null,
            cancellationToken);

        if (!ownsHouse)
        {
            throw new NotFoundException(ErrorCodes.HouseNotFound, "Không tìm thấy khu trọ hoặc bạn không có quyền truy cập.");
        }
    }

    private static void EnsureLandlordCanAccessContract(
        BillingContractSnapshot contract,
        Guid landlordUserId)
    {
        if (contract.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền truy cập hợp đồng này.");
        }
    }
}
