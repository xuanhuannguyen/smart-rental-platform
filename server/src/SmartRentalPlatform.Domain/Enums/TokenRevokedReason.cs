namespace SmartRentalPlatform.Domain.Enums;

public enum TokenRevokedReason
{
    Logout,
    LogoutAllDevices,
    PasswordChanged,
    TokenRotated,
    ReuseDetected,
    Expired,
    AdminRevoked
}
