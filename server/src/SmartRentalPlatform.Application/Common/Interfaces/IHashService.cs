namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IHashService
{
    string HashSha256Hex(string value);
}
