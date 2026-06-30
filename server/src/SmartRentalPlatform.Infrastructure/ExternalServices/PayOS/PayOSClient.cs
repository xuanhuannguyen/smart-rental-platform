using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Infrastructure.Options;

namespace SmartRentalPlatform.Infrastructure.ExternalServices.PayOS;

public class PayOSClient : IPayOSClient
{
    public const string HttpClientName = "PayOS";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly PayOSOptions options;
    private readonly IHostEnvironment environment;
    private readonly ILogger<PayOSClient> logger;

    public PayOSClient(
        IHttpClientFactory httpClientFactory,
        IOptions<PayOSOptions> options,
        IHostEnvironment environment,
        ILogger<PayOSClient> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
        this.environment = environment;
        this.logger = logger;
    }

    public async Task<PayOSCreatePaymentResult> CreatePaymentAsync(
        PayOSCreatePaymentInput input,
        CancellationToken cancellationToken = default)
    {
        if (IsDevelopmentMockMode())
        {
            logger.LogWarning("PayOS is running in Development Mock Mode because credentials are not configured.");
            return CreateMockPaymentResult(input);
        }

        EnsureConfigured();

        var amount = decimal.ToInt64(decimal.Round(input.Amount, 0, MidpointRounding.AwayFromZero));
        var orderCode = long.Parse(input.ProviderOrderCode);
        var returnUrl = string.IsNullOrWhiteSpace(input.ReturnUrl) ? options.ReturnUrl : input.ReturnUrl;
        var cancelUrl = string.IsNullOrWhiteSpace(input.CancelUrl) ? options.CancelUrl : input.CancelUrl;

        var payload = new PayOSCreatePaymentRequest(
            orderCode,
            amount,
            input.Description,
            cancelUrl,
            returnUrl,
            (long)input.ExpiresAt.ToUnixTimeSeconds(),
            [new PayOSCreatePaymentItem(input.Description, 1, amount)],
            BuildCreatePaymentSignature(amount, orderCode, input.Description, cancelUrl, returnUrl));

        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, options.CreatePaymentPath)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation("x-client-id", options.ClientId);
        request.Headers.TryAddWithoutValidation("x-api-key", options.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"PayOS create payment failed with HTTP {(int)response.StatusCode}.");
        }

        var parsed = JsonSerializer.Deserialize<PayOSCreatePaymentResponse>(body, JsonOptions);
        if (parsed is null)
        {
            throw new InvalidOperationException("PayOS create payment response was invalid.");
        }

        if (!IsSuccessCode(parsed.Code))
        {
            var message = string.IsNullOrWhiteSpace(parsed.Desc)
                ? "PayOS create payment failed."
                : parsed.Desc.Trim();
            throw new InvalidOperationException($"PayOS create payment failed: {message}");
        }

        if (parsed.Data is null)
        {
            throw new InvalidOperationException("PayOS create payment response did not include payment data.");
        }

        return new PayOSCreatePaymentResult
        {
            ProviderTransactionCode = parsed.Data.PaymentLinkId,
            CheckoutUrl = parsed.Data.CheckoutUrl,
            QrCode = parsed.Data.QrCode,
            GatewayResponseCode = parsed.Code,
            GatewayResponseMessage = parsed.Desc,
            ExpiresAt = parsed.Data.ExpiredAt.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(parsed.Data.ExpiredAt.Value)
                : input.ExpiresAt
        };
    }

    private static PayOSCreatePaymentResult CreateMockPaymentResult(PayOSCreatePaymentInput input)
    {
        var expiresAt = input.ExpiresAt == default
            ? DateTimeOffset.UtcNow.AddMinutes(15)
            : input.ExpiresAt;

        return new PayOSCreatePaymentResult
        {
            ProviderTransactionCode = $"mock-{input.ProviderOrderCode}",
            CheckoutUrl = $"http://localhost:5173/dev/mock-payment?providerOrderCode={Uri.EscapeDataString(input.ProviderOrderCode)}",
            QrCode = $"MOCK-PAYOS-QR:{input.ProviderOrderCode}:{input.Amount:0}",
            GatewayResponseCode = "00",
            GatewayResponseMessage = "PayOS Development Mock Mode payment created.",
            ExpiresAt = expiresAt
        };
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.ClientId)
            || string.IsNullOrWhiteSpace(options.ApiKey)
            || string.IsNullOrWhiteSpace(options.ChecksumKey)
            || string.IsNullOrWhiteSpace(options.BaseUrl)
            || string.IsNullOrWhiteSpace(options.ReturnUrl)
            || string.IsNullOrWhiteSpace(options.CancelUrl))
        {
            throw new InvalidOperationException("PayOS configuration is incomplete.");
        }
    }

    private bool IsDevelopmentMockMode()
    {
        return environment.IsDevelopment() &&
            (IsMissingOrPlaceholder(options.ClientId)
            || IsMissingOrPlaceholder(options.ApiKey)
            || IsMissingOrPlaceholder(options.ChecksumKey)
            || IsMissingOrPlaceholder(options.BaseUrl));
    }

    private static bool IsMissingOrPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "your-client-id"
            or "your-api-key"
            or "your-checksum-key"
            or "placeholder"
            or "dev-placeholder"
            or "changeme"
            or "dev-client-id"
            or "dev-api-key"
            or "dev-checksum-key"
            || normalized.Contains("placeholder", StringComparison.Ordinal)
            || normalized.StartsWith("your-", StringComparison.Ordinal);
    }

    private static bool IsSuccessCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code)
            || string.Equals(code, "00", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildCreatePaymentSignature(
        long amount,
        long orderCode,
        string description,
        string? cancelUrl,
        string? returnUrl)
    {
        var fields = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["amount"] = amount.ToString(),
            ["cancelUrl"] = cancelUrl,
            ["description"] = description,
            ["orderCode"] = orderCode.ToString(),
            ["returnUrl"] = returnUrl
        };

        return BuildHmacSha256Signature(fields);
    }

    private string BuildHmacSha256Signature(SortedDictionary<string, string?> fields)
    {
        var data = string.Join('&', fields.Select(x => $"{x.Key}={x.Value ?? string.Empty}"));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.ChecksumKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private sealed record PayOSCreatePaymentRequest(
        long OrderCode,
        long Amount,
        string Description,
        string? CancelUrl,
        string? ReturnUrl,
        long ExpiredAt,
        IReadOnlyList<PayOSCreatePaymentItem> Items,
        string Signature);

    private sealed record PayOSCreatePaymentItem(
        string Name,
        int Quantity,
        long Price);

    private sealed class PayOSCreatePaymentResponse
    {
        public string? Code { get; set; }
        public string? Desc { get; set; }
        public PayOSCreatePaymentData? Data { get; set; }
    }

    private sealed class PayOSCreatePaymentData
    {
        public string? PaymentLinkId { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? QrCode { get; set; }
        public long? ExpiredAt { get; set; }
    }
}
