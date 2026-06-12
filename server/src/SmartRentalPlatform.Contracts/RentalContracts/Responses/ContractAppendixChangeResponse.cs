namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ContractAppendixChangeResponse
{
    public Guid Id { get; set; }

    public string ChangeType { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public Guid? TargetId { get; set; }

    public string? FieldName { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
