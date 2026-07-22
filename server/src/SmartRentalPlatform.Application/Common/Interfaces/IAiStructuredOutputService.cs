namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IAiStructuredOutputService
{
    Task<T?> CreateJsonAsync<T>(
        string schemaName,
        object jsonSchema,
        string instructions,
        object input,
        CancellationToken cancellationToken = default);

    Task<T?> CreateJsonWithImagesAsync<T>(
        string schemaName,
        object jsonSchema,
        string instructions,
        object input,
        IReadOnlyCollection<AiImageInput> images,
        CancellationToken cancellationToken = default);
}

public sealed record AiImageInput(
    string FileName,
    string ContentType,
    byte[] Content);
