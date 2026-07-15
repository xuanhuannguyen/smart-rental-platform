using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Infrastructure.Options;

namespace SmartRentalPlatform.Infrastructure.ExternalServices.Ai;

public sealed class MeterAiClient(HttpClient httpClient, IOptions<MeterAiOptions> options) : IMeterAiClient
{
    private readonly MeterAiOptions options = options.Value;

    public async Task<MeterAiClientResult> ReadMeterAsync(
        ImageUploadFile image,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(image.Content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(image.ContentType);
        form.Add(file, "file", image.FileName);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync("predict", form, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new BadRequestException(ErrorCodes.MeterReadingInvalid,
                "Dịch vụ AI đọc đồng hồ hiện không khả dụng. Vui lòng thử lại.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BadRequestException(ErrorCodes.MeterReadingInvalid,
                $"Dịch vụ AI không phản hồi sau {options.TimeoutSeconds} giây.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                AiError? error = null;
                try
                {
                    error = await response.Content.ReadFromJsonAsync<AiError>(cancellationToken: cancellationToken);
                }
                catch (System.Text.Json.JsonException)
                {
                    // Upstream proxies can return HTML/plain text; keep the user-facing error stable.
                }
                throw new BadRequestException(ErrorCodes.MeterReadingInvalid,
                    error?.Detail ?? "AI không nhận diện được chỉ số. Hãy chụp rõ, thẳng và đủ vùng hiển thị số.");
            }

            var result = await response.Content.ReadFromJsonAsync<AiPrediction>(cancellationToken: cancellationToken);
            if (result is null || result.Reading < 0 || string.IsNullOrWhiteSpace(result.RawText))
            {
                throw new BadRequestException(ErrorCodes.MeterReadingInvalid, "Kết quả trả về từ AI không hợp lệ.");
            }

            return new MeterAiClientResult(result.Reading, result.RawText);
        }
    }

    private sealed record AiPrediction(decimal Reading, [property: JsonPropertyName("raw_text")] string RawText);
    private sealed record AiError(string? Detail);
}
