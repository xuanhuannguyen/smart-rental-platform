namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class UpdateContractTermsRequest
{
    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int PaymentDay { get; set; }
}
