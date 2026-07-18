using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class ContractAppendixResponseMapper
{
    public static ContractAppendixResponse ToResponse(ContractAppendix appendix)
    {
        return new ContractAppendixResponse
        {
            Id = appendix.Id,
            RentalContractId = appendix.RentalContractId,
            AppendixNumber = appendix.AppendixNumber,
            EffectiveDate = appendix.EffectiveDate,
            Status = appendix.Status.ToString(),
            CreatedByUserId = appendix.CreatedByUserId,
            ActivatedAt = appendix.ActivatedAt,
            AppliedAt = appendix.AppliedAt,
            StatusReason = appendix.StatusReason,
            Changes = appendix.Changes
                .OrderBy(x => x.SortOrder)
                .Select(ToChangeResponse)
                .ToList(),
            Signatures = appendix.Signatures
                .OrderBy(x => x.SignedAt)
                .Select(ToSignatureResponse)
                .ToList(),
            Files = appendix.Files
                .OrderByDescending(x => x.CreatedAt)
                .Select(ToFileResponse)
                .ToList(),
            CreatedAt = appendix.CreatedAt,
            UpdatedAt = appendix.UpdatedAt
        };
    }

    private static ContractAppendixChangeResponse ToChangeResponse(ContractAppendixChange change)
    {
        return new ContractAppendixChangeResponse
        {
            Id = change.Id,
            ChangeType = change.ChangeType.ToString(),
            TargetType = change.TargetType.ToString(),
            TargetId = change.TargetId,
            FieldName = change.FieldName,
            OldValue = change.OldValue,
            NewValue = change.NewValue,
            SortOrder = change.SortOrder,
            CreatedAt = change.CreatedAt
        };
    }

    private static ContractSignatureResponse ToSignatureResponse(ContractSignature signature)
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

    private static ContractFileResponse ToFileResponse(ContractFile file)
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
}
