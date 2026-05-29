using System.Security.Cryptography;
using System.Text;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Infrastructure.Security;

public class Sha256HashService : IHashService
{
    public string HashSha256Hex(string value)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hashedBytes).ToLowerInvariant();
    }
}
