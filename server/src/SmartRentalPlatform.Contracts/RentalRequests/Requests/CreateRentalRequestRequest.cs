namespace SmartRentalPlatform.Contracts.RentalRequests.Requests;

public class CreateRentalRequestRequest
{
    public DateOnly DesiredStartDate { get; set; }

    public DateOnly ExpectedEndDate { get; set; }

    public int ExpectedOccupantCount { get; set; } = 1;

    public string? TenantNote { get; set; }
}
