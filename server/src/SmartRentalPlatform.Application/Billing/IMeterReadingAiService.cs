using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Contracts.Billing.Responses;

namespace SmartRentalPlatform.Application.Billing;

public interface IMeterReadingAiService
{
    Task<MeterAiResponse> ReadAsync(
        Guid landlordUserId,
        Guid contractId,
        Guid serviceTypeId,
        DateOnly billingPeriodStart,
        ImageUploadFile image,
        CancellationToken cancellationToken = default);
}
