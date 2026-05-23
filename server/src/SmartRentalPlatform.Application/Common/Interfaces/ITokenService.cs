using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user, IReadOnlyCollection<string> roles);

    string GenerateRefreshToken();

    string GenerateOtp(int length = 6);

    string HashToken(string token);
}
