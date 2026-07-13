namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IChatAiStructuredOutputService
{
    Task<T?> CreateJsonAsync<T>(
        string schemaName,
        object jsonSchema,
        string instructions,
        object input,
        CancellationToken cancellationToken = default);
}
