namespace SmartRentalPlatform.Infrastructure.Options;

public class VnptEkycOptions
{
    public const string SectionName = "VnptEkyc";

    public bool UseMock { get; set; } = true;

    public string BaseUrl { get; set; } = "https://api.idg.vnpt.vn";

    public string TokenId { get; set; } = string.Empty;

    public string TokenKey { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public string AuthMode { get; set; } = "StaticToken";

    public int TokenCacheDurationMinutes { get; set; } = 50;

    public string MacAddress { get; set; } = "TEST1";

    public string ClientSessionPrefix { get; set; } = "srp-kyc";

    public int OcrType { get; set; } = -1;

    public string CropParam { get; set; } = "0.14,0.3";

    public bool ValidatePostcode { get; set; } = true;

    public double FaceMatchThreshold { get; set; } = 80.0;

    public int TimeoutSeconds { get; set; } = 30;
}
