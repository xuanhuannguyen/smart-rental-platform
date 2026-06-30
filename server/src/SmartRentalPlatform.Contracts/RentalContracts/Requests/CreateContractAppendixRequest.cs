namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class CreateContractAppendixRequest
{
    public DateOnly EffectiveDate { get; set; }

    public List<ContractAppendixChangeRequest> Changes { get; set; } = [];
}

public class ContractAppendixChangeRequest
{
    public string ChangeType { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public Guid? TargetId { get; set; }

    public string? FieldName { get; set; }

    public string? NewValue { get; set; }
}
