using System.Text.Json.Serialization;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class TerminateContractRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContractTerminationType TerminationType { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public decimal DamageFee { get; set; } = 0;
    public string Reason { get; set; } = string.Empty;
    public bool CreateFinalInvoice { get; set; }
    public decimal FinalInvoiceDiscountAmount { get; set; } = 0;
    public string? FinalInvoiceNote { get; set; }
    public List<MeterReadingInput> FinalInvoiceMeterReadings { get; set; } = [];
}
