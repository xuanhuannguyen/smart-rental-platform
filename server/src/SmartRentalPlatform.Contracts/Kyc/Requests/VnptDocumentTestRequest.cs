using Microsoft.AspNetCore.Http;

namespace SmartRentalPlatform.Contracts.Kyc.Requests;

public sealed class VnptDocumentTestRequest
{
    public string DocumentType { get; set; } = "CCCD";

    public IFormFile FrontImage { get; set; } = default!;

    public IFormFile BackImage { get; set; } = default!;
}
