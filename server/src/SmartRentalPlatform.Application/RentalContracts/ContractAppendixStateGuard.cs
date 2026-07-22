using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class ContractAppendixStateGuard
{
    public static void EnsureContractActive(RentalContract contract)
    {
        if (contract.Status == RentalContractStatus.Active)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RentalContractInvalidStatus,
            "Chỉ có thể lập phụ lục cho hợp đồng đang có hiệu lực.",
            new { contract.Id, currentStatus = contract.Status.ToString() });
    }

    public static void EnsureNoPendingAppendix(RentalContract contract)
    {
        if (!contract.Appendices.Any(x =>
                x.Status is ContractAppendixStatus.PendingSignature
                    or ContractAppendixStatus.LandlordRevisionRequested
                    or ContractAppendixStatus.TenantRevisionRequested))
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.ContractAppendixInvalidStatus,
            "Hợp đồng đang có phụ lục chờ ký, vui lòng hoàn tất hoặc từ chối phụ lục hiện tại trước khi tạo phụ lục mới.",
            new { contract.Id });
    }

    public static void EnsureAppendixPendingSignature(ContractAppendix appendix)
    {
        if (appendix.Status == ContractAppendixStatus.PendingSignature)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.ContractAppendixInvalidStatus,
            "Trạng thái phụ lục không cho phép thao tác này.",
            new { appendix.Id, currentStatus = appendix.Status.ToString() });
    }

    public static void EnsureAppendixEffectiveDateStillSignable(ContractAppendix appendix, DateOnly today)
    {
        if (today <= appendix.EffectiveDate)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.ContractAppendixInvalidStatus,
            "Phụ lục đã quá ngày áp dụng, vui lòng hủy và tạo phụ lục mới.",
            new { appendix.Id, appendix.EffectiveDate });
    }

    public static void EnsureAppendixCanPreview(ContractAppendix appendix)
    {
        if (appendix.Status is not ContractAppendixStatus.Active)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.ContractAppendixInvalidStatus,
            "Phụ lục đã có hiệu lực, vui lòng xem file phụ lục đã ký.",
            new { appendix.Id, currentStatus = appendix.Status.ToString() });
    }
}
