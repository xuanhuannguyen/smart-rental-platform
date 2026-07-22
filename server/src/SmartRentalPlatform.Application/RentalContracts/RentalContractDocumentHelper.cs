using System.Security.Cryptography;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Application.RentalContracts;

public sealed class RentalContractDocumentHelper
{
    private readonly IHashService hashService;
    private readonly ISensitiveDataProtector sensitiveDataProtector;

    public RentalContractDocumentHelper(IHashService hashService, ISensitiveDataProtector sensitiveDataProtector)
    {
        this.hashService = hashService;
        this.sensitiveDataProtector = sensitiveDataProtector;
    }

    public string? HashDocumentNumber(string? documentNumber)
    {
        return string.IsNullOrWhiteSpace(documentNumber) ? null : hashService.HashSha256Hex(documentNumber.Trim());
    }

    public string? EncryptDocumentNumber(string? documentNumber)
    {
        return string.IsNullOrWhiteSpace(documentNumber) ? null : sensitiveDataProtector.Encrypt(documentNumber);
    }

    public string? DecryptDocumentNumber(string? encryptedDocumentNumber)
    {
        if (string.IsNullOrWhiteSpace(encryptedDocumentNumber))
        {
            return null;
        }

        try
        {
            return sensitiveDataProtector.Decrypt(encryptedDocumentNumber);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public static string? MaskDocumentNumber(string? documentNumber)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            return null;
        }

        string text = documentNumber.Trim();
        if (text.Length <= 4)
        {
            return new string('*', text.Length);
        }

        return new string('*', text.Length - 4) + text[^4..];
    }
}
