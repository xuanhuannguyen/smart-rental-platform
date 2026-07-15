using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class ContractAppendixRequestValidator
{
    public static void ValidateCreateRequest(CreateContractAppendixRequest request)
    {
        if (request.EffectiveDate == default)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Ngày hiệu lực phụ lục không hợp lệ.");
        }

        if (request.Changes.Count == 0)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Phụ lục phải có ít nhất một nội dung thay đổi.");
        }
    }

    public static string NormalizeRequiredText(string? value, string message)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        throw new BadRequestException(ErrorCodes.ValidationError, message);
    }

    public static string GenerateAppendixNumber(RentalContract contract)
    {
        var nextNumber = contract.Appendices.Count + 1;
        var appendixNumber = $"PL-{nextNumber:000}-{contract.ContractNumber}";
        return appendixNumber[..Math.Min(50, appendixNumber.Length)];
    }
}
