using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Entities.Media;

namespace SmartRentalPlatform.Application.Common.Interfaces.Media;

public interface IMediaAssetService
{
    Task<MediaAsset> CreateAsync(
        CreateMediaAssetRequest request,
        CancellationToken cancellationToken = default);

    Task<MediaAsset?> GetByIdAsync(
        Guid mediaAssetId,
        CancellationToken cancellationToken = default);

    Task<MediaAsset?> GetByObjectKeyAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task MarkDeletedAsync(
        Guid mediaAssetId,
        CancellationToken cancellationToken = default);
}
