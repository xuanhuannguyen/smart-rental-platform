using System.Net.Http.Json;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
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
        return await CreateJsonWithImagesAsync<T>(
            schemaName,
            jsonSchema,
            instructions,
            input,
            [],
            cancellationToken);
    }

    public async Task<T?> CreateJsonWithImagesAsync<T>(
        string schemaName,
        object jsonSchema,
        string instructions,
        object input,
        IReadOnlyCollection<AiImageInput> images,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || !options.HasCredential())
        {
            return default;
        }

        var parts = new List<object>
        {
            new
            {
                text = BuildPrompt(schemaName, instructions, input)
            }
        };

        foreach (var image in images)
        {
            parts.Add(new
            {
                inlineData = new
                {
                    mimeType = image.ContentType,
                    data = Convert.ToBase64String(image.Content)
                }
            });
        }

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts
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
            string requestUrl;
            if (options.UseVertexAi)
            {
                requestUrl = $"https://{options.Region}-aiplatform.googleapis.com/v1/projects/{options.ProjectId}/locations/{options.Region}/publishers/google/models/{options.Model}:generateContent";
            }
            else
            {
                requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{options.Model}:generateContent";
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            
            if (options.UseVertexAi)
            {
                var token = await GetAccessTokenAsync(cancellationToken);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                request.Headers.Add("x-goog-api-key", options.ApiKey);
            }

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

    private static string? _cachedToken;
    private static DateTime _tokenExpiresAt = DateTime.MinValue;
    private static readonly object TokenLock = new();

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        lock (TokenLock)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && _tokenExpiresAt > DateTime.UtcNow.AddMinutes(5))
            {
                return _cachedToken;
            }
        }

        var credential = GoogleCredential.FromJson(options.ServiceAccountJson)
            .CreateScoped("https://www.googleapis.com/auth/cloud-platform");

        var token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);

        lock (TokenLock)
        {
            _cachedToken = token;
            _tokenExpiresAt = DateTime.UtcNow.AddHours(1);
        }

        return token;
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
