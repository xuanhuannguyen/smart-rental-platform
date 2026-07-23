namespace SmartRentalPlatform.Contracts.Admin.Requests;

public class AdminApproveKycRequest
{
    public string? CitizenId { get; set; }

    public string? FullName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? Address { get; set; }
}
