using SmartRentalPlatform.Contracts.Requests.Kyc;
using SmartRentalPlatform.Contracts.Responses.Kyc;

namespace SmartRentalPlatform.Application.Services.Kyc;

public interface IKycService
{
    /// <summary>
    /// User gửi KYC với ảnh CCCD/Passport mặt trước, mặt sau, và selfie.
    /// Backend lưu ảnh vào storage (ObjectKey), không lưu binary trong DB.
    /// Backend gọi VNPT eKYC API để xử lý OCR, face matching, liveness detection.
    /// Kết quả eKYC pass -> status = PendingAdminReview; fail -> status = EkycFailed.
    /// </summary>
    Task<KycSubmissionResponse> SubmitAsync(
        Guid userId,
        SubmitKycRequest request);

    /// <summary>
    /// User xem trạng thái KYC mới nhất của mình.
    /// Chỉ trả dữ liệu safe (không trả citizenIdHash, không trả raw VNPT response).
    /// </summary>
    Task<KycStatusResponse> GetMyStatusAsync(Guid userId);

    /// <summary>
    /// User xem lịch sử tất cả các lần gửi KYC (newest first).
    /// Chỉ trả dữ liệu safe.
    /// </summary>
    Task<KycHistoryResponse> GetMyHistoryAsync(Guid userId);
}