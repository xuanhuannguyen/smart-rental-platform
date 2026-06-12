namespace SmartRentalPlatform.Contracts.RentalRequests.Requests;

public class ApproveRentalRequestRequest
{
    public DateTimeOffset? PaymentDeadlineAt { get; set; }
}
