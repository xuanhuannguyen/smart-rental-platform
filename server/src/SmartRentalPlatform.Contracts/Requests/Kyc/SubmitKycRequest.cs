using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SmartRentalPlatform.Contracts.Requests.Kyc;

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