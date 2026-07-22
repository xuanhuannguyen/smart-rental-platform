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
}
