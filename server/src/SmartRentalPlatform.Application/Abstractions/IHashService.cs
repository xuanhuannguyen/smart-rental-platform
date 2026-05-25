namespace SmartRentalPlatform.Application.Abstractions;

public interface IHashService
{
    string HashSha256Hex(string value);
}
