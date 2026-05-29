namespace SmartRentalPlatform.Contracts.LeasePolicies.Requests;

public class UpdateLeasePolicyRequest
{
    public bool AllowShortTermRenewal { get; set; }
    public int RenewalNoticeDays { get; set; }
    public decimal DepositMonths { get; set; }
    public decimal Discount6MonthsPercent { get; set; }
    public decimal Discount9MonthsPercent { get; set; }
    public decimal Discount12MonthsPercent { get; set; }
    public decimal Discount24MonthsPercent { get; set; }
}

