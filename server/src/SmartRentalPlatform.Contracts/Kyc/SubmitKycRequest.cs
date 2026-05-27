using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SmartRentalPlatform.Contracts.Kyc;

public class SubmitKycRequest
{
    public string DocumentType { get; set; } = default!;

    [Required]
    public IFormFile FrontImage { get; set; } = default!;

    [Required]
    public IFormFile BackImage { get; set; } = default!;

    [Required]
    public IFormFile SelfieImage { get; set; } = default!;

    public string SelfieCaptureMethod { get; set; } = default!;
}
