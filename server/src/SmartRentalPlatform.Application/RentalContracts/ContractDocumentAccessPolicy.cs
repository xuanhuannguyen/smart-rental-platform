using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class ContractDocumentAccessPolicy
{
    public static bool HasContractRelationship(Guid userId, RentalContract contract)
    {
        return contract.Room.RoomingHouse.LandlordUserId == userId ||
               contract.MainTenantUserId == userId ||
               contract.Occupants.Any(x => x.UserId == userId) ||
               contract.Signatures.Any(x => x.SignerUserId == userId) ||
               contract.Appendices.Any(x => x.Signatures.Any(s => s.SignerUserId == userId));
    }

    public static bool CanViewFullContract(Guid userId, RentalContract contract)
    {
        return contract.Signatures.Any(x =>
            x.SignerUserId == userId &&
            x.Status == ContractSignatureStatus.Signed);
    }

    public static bool CanViewMaskedContract(Guid userId, RentalContract contract)
    {
        return HasContractRelationship(userId, contract);
    }

    public static bool CanViewFullAppendix(Guid userId, ContractAppendix appendix)
    {
        return appendix.Signatures.Any(x =>
            x.SignerUserId == userId &&
            x.Status == ContractSignatureStatus.Signed);
    }

    public static bool CanViewMaskedAppendix(
        Guid userId,
        RentalContract contract,
        ContractAppendix appendix)
    {
        if (appendix.Status is not (ContractAppendixStatus.Active or ContractAppendixStatus.Cancelled) ||
            !HasContractRelationship(userId, contract))
        {
            return false;
        }

        if (contract.Room.RoomingHouse.LandlordUserId == userId)
        {
            return true;
        }

        var occupantRecords = contract.Occupants
            .Where(x => x.UserId == userId)
            .ToList();

        if (occupantRecords.Count == 0)
        {
            return false;
        }

        // Deliberately do not compare MoveInDate. A participant needs the complete
        // signed appendix chain to reconstruct the terms that apply to their stay.
        return occupantRecords.Any(x =>
            !x.MoveOutDate.HasValue ||
            appendix.EffectiveDate <= x.MoveOutDate.Value);
    }

    public static bool CanOpenUnsignedWorkflowFile(Guid userId, Guid landlordUserId, Guid currentMainTenantUserId)
    {
        return landlordUserId == userId ||
               currentMainTenantUserId == userId;
    }
}
