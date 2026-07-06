using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class ContractAppendixSignatureFactory
{
    public static ContractSignature Create(
        Guid appendixId,
        Guid signerUserId,
        ContractSignerRole signerRole,
        SignContractRequest request,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now)
    {
        return new ContractSignature
        {
            Id = Guid.NewGuid(),
            RentalContractAppendixId = appendixId,
            SignerUserId = signerUserId,
            SignerRole = signerRole,
            SignatureMethod = ContractSignatureMethod.EmailOtp,
            SignatureText = RentalContractTextHelper.NormalizeOptionalText(request.SignatureText),
            IpAddress = RentalContractTextHelper.NormalizeOptionalText(ipAddress),
            UserAgent = RentalContractTextHelper.NormalizeOptionalText(userAgent),
            SignedAt = now,
            CreatedAt = now
        };
    }
}
