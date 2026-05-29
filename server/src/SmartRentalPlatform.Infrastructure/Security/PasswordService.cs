using Microsoft.AspNetCore.Identity;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Infrastructure.Security;

public class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> _passwordHasher = new();

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(new object(), password);
    }

    public bool VerifyPassword(string hashedPassword, string password)
    {
    var result = _passwordHasher.VerifyHashedPassword(
        new object(),
        hashedPassword,
        password);

    return result == PasswordVerificationResult.Success
        || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
