using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Options;

namespace SmartRentalPlatform.Infrastructure.ExternalServices.DeepSeek;

public sealed class DeepSeekChatStructuredOutputService : IChatAiStructuredOutputService, IBackupAiStructuredOutputService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;
    private readonly DeepSeekOptions options;
    private readonly ILogger<DeepSeekChatStructuredOutputService> logger;

    public DeepSeekChatStructuredOutputService(
        HttpClient httpClient,
        IOptions<DeepSeekOptions> options,
        ILogger<DeepSeekChatStructuredOutputService> logger)
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
        if (!options.HasCredential())
        {
            return default;
        }

        var payload = new
        {
            model = options.Model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = BuildSystemPrompt(schemaName, jsonSchema, instructions)
                },
                new
                {
                    role = "user",
                    content = BuildUserPrompt(input)
                }
            },
            response_format = new
            {
                type = "json_object"
            },
            thinking = new
            {
                type = "disabled"
            },
            temperature = options.Temperature,
            max_tokens = options.MaxTokens,
            stream = false
        };

        using var response = await SendWithRetryAsync(payload, cancellationToken);
        if (response is null)
        {
            return default;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var envelope = await JsonSerializer.DeserializeAsync<DeepSeekResponseEnvelope>(
            stream,
            JsonOptions,
            cancellationToken);
        var content = ExtractContent(envelope);
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(StripJsonFence(content), JsonOptions);
    }

    private async Task<HttpResponseMessage?> SendWithRetryAsync(
        object payload,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
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
                "DeepSeek chat JSON request failed with {StatusCode} on attempt {Attempt}/{MaxAttempts}. Retry={Retry}. Body: {Body}",
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

    private static string BuildSystemPrompt(string schemaName, object jsonSchema, string instructions)
        => $"""
        {instructions}

        Bạn phải trả về JSON hợp lệ duy nhất, không markdown, không ```json.
        JSON output phải khớp schema name: {schemaName}.
        JSON schema tham khảo:
        {JsonSerializer.Serialize(jsonSchema, JsonOptions)}

        Nếu thiếu dữ liệu, vẫn trả JSON hợp lệ với các field bắt buộc.
        """;

    private static string BuildUserPrompt(object input)
        => $"""
        Input JSON:
        {JsonSerializer.Serialize(input, JsonOptions)}

        Trả về JSON hợp lệ theo schema đã yêu cầu.
        """;

    private static string? ExtractContent(DeepSeekResponseEnvelope? envelope)
        => envelope?.Choices
            .Select(x => x.Message?.Content)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static string StripJsonFence(string value)
    {
        var text = value.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        text = text.Trim('`').Trim();
        return text.StartsWith("json", StringComparison.OrdinalIgnoreCase)
            ? text[4..].Trim()
            : text;
    }

    private sealed class DeepSeekResponseEnvelope
    {
        public List<DeepSeekChoice> Choices { get; set; } = new();
    }

    private sealed class DeepSeekChoice
    {
        public DeepSeekMessage? Message { get; set; }
    }

    private sealed class DeepSeekMessage
    {
        public string? Content { get; set; }
    }
}
