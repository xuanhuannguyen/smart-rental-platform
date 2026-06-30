namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface ISensitiveDataProtector
{
    string Encrypt(string plainText);

    string Decrypt(string encryptedText);
}
