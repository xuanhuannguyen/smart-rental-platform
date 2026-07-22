using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class RentalContractTermsValidator
{
    public static void ValidateRequest(UpdateContractTermsRequest request)
    {
        if (request.StartDate == default)
        {
            throw new BadRequestException("VALIDATION_ERROR", "Ngày bắt đầu hợp đồng không được để trống.");
        }

        if (request.EndDate == default)
        {
            throw new BadRequestException("VALIDATION_ERROR", "Ngày kết thúc hợp đồng không được để trống.");
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.StartDate < today)
        {
            throw new BadRequestException("RENTAL_REQUEST_INVALID_DURATION", "Ngày bắt đầu hợp đồng không được nằm trong quá khứ.");
        }

        if (request.EndDate <= request.StartDate)
        {
            throw new BadRequestException("RENTAL_REQUEST_INVALID_DURATION", "Ngày kết thúc hợp đồng phải lớn hơn ngày bắt đầu.");
        }

        if (request.PaymentDay is < 1 or > 28)
        {
            throw new BadRequestException("VALIDATION_ERROR", "Ngày thanh toán phải nằm trong khoảng từ 1 đến 28.");
        }
    }

    public static void ValidateDuration(UpdateContractTermsRequest request, int minRentalMonths, int maxRentalMonths)
    {
        DateOnly minEndDate = request.StartDate.AddMonths(minRentalMonths);
        DateOnly maxEndDate = request.StartDate.AddMonths(maxRentalMonths);
        if (request.EndDate < minEndDate || request.EndDate > maxEndDate)
        {
            throw new BadRequestException("RENTAL_REQUEST_INVALID_DURATION", "Thời hạn hợp đồng không nằm trong chính sách thuê của khu trọ.", new
            {
                request.StartDate,
                request.EndDate,
                minRentalMonths,
                maxRentalMonths
            });
        }
    }
}
