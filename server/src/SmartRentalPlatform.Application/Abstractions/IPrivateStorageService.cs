namespace SmartRentalPlatform.Application.Abstractions;

public interface IPrivateStorageService
{
    Task<string> UploadAsync(
        Stream content,
        string contentType,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}
