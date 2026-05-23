namespace SmartRentalPlatform.Domain.Enums;

public enum TokenRevokedReason
{
    Logout = 1,
    LogoutAllDevices = 2,
    PasswordChanged = 3,
    TokenRotated = 4,
    ReuseDetected = 5,
    Expired = 6,
    AdminRevoked = 7,
    Used = 8,
    Replaced = 9
}
