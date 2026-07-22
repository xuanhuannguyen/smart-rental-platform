using SmartRentalPlatform.Application.Common.Exceptions;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class RentalContractTextHelper
{
    public static string NormalizeRequiredReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BadRequestException("VALIDATION_ERROR", "Lý do không được để trống.");
        }

        return reason.Trim();
    }

    public static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
