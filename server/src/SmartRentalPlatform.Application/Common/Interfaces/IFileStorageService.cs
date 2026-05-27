using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Contracts.Files;

namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<FileUploadResponse> UploadImageAsync(
        ImageUploadFile file,
        FileUploadScope scope,
        CancellationToken cancellationToken = default);
}
