using SmartRentalPlatform.Application.Common.Models;

namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IMeterAiClient
{
    Task<MeterAiClientResult> ReadMeterAsync(
        ImageUploadFile image,
        CancellationToken cancellationToken = default);
}

public sealed record MeterAiClientResult(decimal Reading, string RawText);
