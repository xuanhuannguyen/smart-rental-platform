using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Contracts.Files;

namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<FileUploadResponse> UploadImageAsync(
        ImageUploadFile file,
        FileUploadScope scope,
        CancellationToken cancellationToken = default);

    Task<FileUploadResponse> UploadPdfAsync(
        ImageUploadFile file,
        FileUploadScope scope,
        CancellationToken cancellationToken = default);

    Task<FileUploadResponse> UploadPdfAsync(
        Stream content,
        string fileName,
        FileUploadScope scope,
        CancellationToken cancellationToken = default);
}
