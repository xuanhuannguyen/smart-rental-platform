using System.Text.Json.Serialization;
using SmartRentalPlatform.Contracts.RentalContracts;

namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class RequestContractRevisionRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContractRevisionType RevisionType { get; set; } = ContractRevisionType.ContractTerms;

    public string Reason { get; set; } = string.Empty;
}
