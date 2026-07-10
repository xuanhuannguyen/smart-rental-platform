namespace SmartRentalPlatform.Domain.Enums.RentalContracts
{
    public enum ContractSignatureMethod
    {
        Unknown = 0,
        ClickToSign = 1,
        ESignatureProvider = 2,
        EmailOtp = 3,
        VnptSmsOtp = 4,
        VnptEmailOtp = 5
    }
}
