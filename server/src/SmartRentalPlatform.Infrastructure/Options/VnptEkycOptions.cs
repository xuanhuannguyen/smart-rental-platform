namespace SmartRentalPlatform.Infrastructure.Options;

public class VnptEkycOptions
{
    public const string SectionName = "VnptEkyc";

    public bool UseMock { get; set; }

    public string BaseUrl { get; set; } = "https://api.idg.vnpt.vn";

    /// <summary>
    /// Access token from VNPT eKYC portal (Quản lý token).
    /// Used as: Authorization: Bearer {AccessToken}
    /// Expires every ~24 hours — must be manually refreshed from the VNPT portal.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token-id header value from VNPT eKYC portal.
    /// </summary>
    public string TokenId { get; set; } = string.Empty;

    /// <summary>
    /// Token-key header value from VNPT eKYC portal.
    /// </summary>
    public string TokenKey { get; set; } = string.Empty;

    public string MacAddress { get; set; } = "TEST1";

    public string ClientSessionPrefix { get; set; } = "srp-kyc";

    public int OcrType { get; set; } = -1;

    public string CropParam { get; set; } = "0.14,0.3";

    public bool ValidatePostcode { get; set; } = true;

    public bool EnableFaceVerification { get; set; } = false;

    public double FaceMatchThreshold { get; set; } = 80.0;

    public int TimeoutSeconds { get; set; } = 30;
}
