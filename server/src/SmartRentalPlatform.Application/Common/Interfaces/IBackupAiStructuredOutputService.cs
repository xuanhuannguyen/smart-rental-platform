namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IBackupAiStructuredOutputService
{
    Task<T?> CreateJsonAsync<T>(
        string schemaName,
        object jsonSchema,
        string instructions,
        object input,
        CancellationToken cancellationToken = default);
}
