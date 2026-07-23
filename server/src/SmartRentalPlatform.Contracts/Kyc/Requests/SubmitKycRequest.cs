using System.ComponentModel.DataAnnotations;

namespace SmartRentalPlatform.Contracts.Kyc.Requests;

public class SubmitKycRequest
{
    public string DocumentType { get; set; } = default!;

    [Required]
    public Guid FrontMediaAssetId { get; set; }

    [Required]
    public Guid BackMediaAssetId { get; set; }

    [Required]
    public Guid SelfieMediaAssetId { get; set; }

    public string SelfieCaptureMethod { get; set; } = default!;

    public string? ManualCitizenId { get; set; }

    public string? ManualFullName { get; set; }

    public DateTime? ManualDateOfBirth { get; set; }

    public string? ManualGender { get; set; }

    public string? ManualAddress { get; set; }
}
