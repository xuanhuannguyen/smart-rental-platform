using Microsoft.AspNetCore.DataProtection;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Infrastructure.Security;

public class DataProtectionSensitiveDataProtector : ISensitiveDataProtector
{
    private const string Purpose = "SmartRentalPlatform.SensitiveData.v1";

    private readonly IDataProtector protector;

    public DataProtectionSensitiveDataProtector(IDataProtectionProvider dataProtectionProvider)
    {
        protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Encrypt(string plainText)
    {
        return protector.Protect(plainText.Trim());
    }

    public string Decrypt(string encryptedText)
    {
        return protector.Unprotect(encryptedText);
    }
}
