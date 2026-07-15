using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class RentalContractLifecycleHelper
{
    public static void EnsureDepositReadyForSettlement(RoomDeposit deposit)
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

    public static WalletTransactionMetadata CreateDepositSettlementMetadata(
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

    public static void CancelOpenAppendices(RentalContract contract, DateTimeOffset now)
    {
        foreach (ContractAppendix appendix in contract.Appendices.Where(x => x.Status is ContractAppendixStatus.Draft or ContractAppendixStatus.PendingSignature or ContractAppendixStatus.Active or ContractAppendixStatus.LandlordRevisionRequested or ContractAppendixStatus.TenantRevisionRequested))
        {
            appendix.Status = ContractAppendixStatus.Cancelled;
            appendix.StatusReason = "Hợp đồng đã chấm dứt.";
            appendix.UpdatedAt = now;
        }
    }

    public static void CloseContractOccupants(RentalContract contract, DateOnly terminationDate, DateOnly today, DateTimeOffset now)
    {
        bool isBeforeContractStart = today < contract.StartDate;
        foreach (ContractOccupant occupant in contract.Occupants.Where(x => x.Status is ContractOccupantStatus.Active or ContractOccupantStatus.PendingMoveIn))
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

    public static void MarkRoomAvailableIfReservedOrOccupied(RentalContract contract, DateTimeOffset now)
    {
        if (contract.Room.Status is RoomStatus.Occupied or RoomStatus.Reserved)
        {
            contract.Room.Status = RoomStatus.Available;
            contract.Room.UpdatedAt = now;
        }
    }

    public static ContractOccupantStatus ResolveMoveInStatus(DateOnly moveInDate, DateOnly today)
    {
        return moveInDate <= today ? ContractOccupantStatus.Active : ContractOccupantStatus.PendingMoveIn;
    }
}
