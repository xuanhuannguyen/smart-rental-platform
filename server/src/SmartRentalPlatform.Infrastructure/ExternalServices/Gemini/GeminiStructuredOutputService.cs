using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Options;

namespace SmartRentalPlatform.Infrastructure.ExternalServices.Gemini;

public sealed class GeminiStructuredOutputService : IAiStructuredOutputService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;
    private readonly GeminiOptions options;
    private readonly ILogger<GeminiStructuredOutputService> logger;

    public GeminiStructuredOutputService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiStructuredOutputService> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<T?> CreateJsonAsync<T>(
        string schemaName,
        object jsonSchema,
        string instructions,
        object input,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return default;
        }

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = BuildPrompt(schemaName, instructions, input)
                        }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = jsonSchema
            }
        };

        using var response = await SendWithRetryAsync(payload, cancellationToken);
        if (response is null)
        {
            return default;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var envelope = await JsonSerializer.DeserializeAsync<GeminiResponseEnvelope>(
            stream,
            JsonOptions,
            cancellationToken);
        var text = ExtractText(envelope);
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(text, JsonOptions);
    }

    private async Task<HttpResponseMessage?> SendWithRetryAsync(
        object payload,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"models/{Uri.EscapeDataString(options.Model)}:generateContent");
            request.Headers.Add("x-goog-api-key", options.ApiKey);
            request.Content = JsonContent.Create(payload, options: JsonOptions);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var statusCode = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var shouldRetry = statusCode is 429 or 500 or 502 or 503 or 504;
            logger.LogWarning(
                "Gemini structured output request failed with {StatusCode} on attempt {Attempt}/{MaxAttempts}. Retry={Retry}. Body: {Body}",
                statusCode,
                attempt,
                maxAttempts,
                shouldRetry && attempt < maxAttempts,
                body);
            response.Dispose();

            if (!shouldRetry || attempt == maxAttempts)
            {
                return null;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(350 * attempt), cancellationToken);
        }

        return null;
    }

    private static string BuildPrompt(string schemaName, string instructions, object input)
        => $"""
        {instructions}

        Schema name: {schemaName}
        Input JSON:
        {JsonSerializer.Serialize(input, JsonOptions)}
        """;

    private static string? ExtractText(GeminiResponseEnvelope? envelope)
        => envelope?.Candidates
            .SelectMany(x => x.Content?.Parts ?? [])
            .Select(x => x.Text)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private sealed class GeminiResponseEnvelope
    {
        public List<GeminiCandidate> Candidates { get; set; } = new();
    }

    private sealed class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiContent
    {
        public List<GeminiPart> Parts { get; set; } = new();
    }

    private sealed class GeminiPart
    {
        public string? Text { get; set; }
    }
}
