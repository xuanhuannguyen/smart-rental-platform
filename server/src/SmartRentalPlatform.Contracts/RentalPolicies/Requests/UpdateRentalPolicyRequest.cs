namespace SmartRentalPlatform.Contracts.RentalPolicies.Requests;

public class UpdateRentalPolicyRequest
{
    public int MinRentalMonths { get; set; } = 1;

    public int MaxRentalMonths { get; set; } = 12;

    public bool AllowShortTermRenewal { get; set; }

    public int RenewalNoticeDays { get; set; }

    public decimal DepositMonths { get; set; }

    public int DefaultPaymentDay { get; set; } = 5;
}

