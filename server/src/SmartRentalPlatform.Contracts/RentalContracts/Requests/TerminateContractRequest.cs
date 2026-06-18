using System.Text.Json.Serialization;

namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class TerminateContractRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContractTerminationType TerminationType { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public decimal DamageFee { get; set; } = 0;
    public string Reason { get; set; } = string.Empty;
}
