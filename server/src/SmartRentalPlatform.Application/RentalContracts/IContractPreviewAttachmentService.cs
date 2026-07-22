using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public interface IContractPreviewAttachmentService
{
    Task<IReadOnlyList<ContractReviewAttachment>> LoadForContractAsync(
        Guid viewerUserId,
        RentalContract contract,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContractReviewAttachment>> LoadForAppendixAsync(
        Guid viewerUserId,
        ContractAppendix appendix,
        CancellationToken cancellationToken = default);
}
