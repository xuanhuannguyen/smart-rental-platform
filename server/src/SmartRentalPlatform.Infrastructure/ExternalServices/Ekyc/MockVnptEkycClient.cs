using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Infrastructure.ExternalServices.Ekyc;

public class MockVnptEkycClient : IVnptEkycClient
{
    public async Task<VnptEkycClientResult> VerifyAsync(
        VnptEkycVerifyInput input,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        if (input.FrontImage.FileName.Contains("fail-document", StringComparison.OrdinalIgnoreCase))
        {
            return new VnptEkycClientResult
            {
                SessionId = $"vnpt-session-{Guid.NewGuid():N}",
                EkycResult = "Failed",
                DocumentCheckResult = "Unreadable",
                IsDocumentUnreadable = true,
                ErrorCode = "DOC_UNREADABLE",
                ErrorMessage = "Document image is unreadable."
            };
        }

        if (input.FrontImage.FileName.Contains("fail-provider", StringComparison.OrdinalIgnoreCase))
        {
            return new VnptEkycClientResult
            {
                SessionId = null,
                EkycResult = "ProviderError",
                IsProviderFailure = true,
                ErrorCode = "VNPT_TIMEOUT",
                ErrorMessage = "VNPT provider connection failed."
            };
        }

        return new VnptEkycClientResult
        {
            SessionId = $"vnpt-session-{Guid.NewGuid():N}",
            EkycResult = "Passed",
            OcrFullName = "Nguyen Van A",
            OcrCitizenId = "123456789012",
            OcrDateOfBirth = new DateTime(2000, 1, 15),
            OcrGender = "Male",
            OcrAddress = "123 ABC Street, Ho Chi Minh City",
            OcrConfidence = 0.96m,
            DocumentCheckResult = "Valid",
            FaceMatchScore = 0.92m,
            FaceMatchResult = "Matched",
            LivenessResult = "Passed"
        };
    }
}

