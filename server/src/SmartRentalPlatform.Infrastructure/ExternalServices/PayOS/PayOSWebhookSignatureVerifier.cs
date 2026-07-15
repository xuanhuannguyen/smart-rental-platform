using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Infrastructure.Options;

namespace SmartRentalPlatform.Infrastructure.ExternalServices.PayOS;

public class PayOSWebhookSignatureVerifier : IPayOSWebhookSignatureVerifier
{
    private readonly PayOSOptions options;

    public PayOSWebhookSignatureVerifier(IOptions<PayOSOptions> options)
    {
        this.options = options.Value;
    }

    public bool Verify(string rawPayload, string? signatureHeader)
    {
        return VerifyPayment(rawPayload, signatureHeader);
    }

    public bool VerifyPayment(string rawPayload, string? signatureHeader)
    {
        return VerifyCore(rawPayload, signatureHeader, options.ChecksumKey);
    }

    public bool VerifyPayout(string rawPayload, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(options.PayoutChecksumKey))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            var root = document.RootElement;
            var signature = ExtractSignature(root, signatureHeader);
            if (string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            var data = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("data", out var dataElement)
                && dataElement.ValueKind == JsonValueKind.Object
                    ? dataElement
                    : root;

            var canonicalData = BuildCanonicalPayoutData(data);
            var expected = ComputeHmacSha256(canonicalData, options.PayoutChecksumKey);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BuildCanonicalPayoutData(JsonElement data)
    {
        var values = data.EnumerateObject()
            .Where(x => !string.Equals(x.Name, "signature", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => $"{x.Name}={ToPayoutCanonicalValue(x.Value)}");

        return string.Join('&', values);
    }

    private static string ToPayoutCanonicalValue(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return Uri.EscapeDataString(value.GetString() ?? string.Empty);
            case JsonValueKind.Number:
                return Uri.EscapeDataString(value.GetRawText());
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Null:
                return string.Empty;
            case JsonValueKind.Object:
            case JsonValueKind.Array:
                var sorted = DeepSort(value);
                var serializeOptions = new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = false
                };
                var json = JsonSerializer.Serialize(sorted, serializeOptions);
                return Uri.EscapeDataString(json);
            default:
                return string.Empty;
        }
    }

    private static object? DeepSort(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                foreach (var prop in element.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "signature", StringComparison.OrdinalIgnoreCase))
                        continue;
                    dict[prop.Name] = DeepSort(prop.Value);
                }
                return dict;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(DeepSort(item));
                }
                return list;
            case JsonValueKind.String:
                return element.GetString() ?? string.Empty;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l)) return l;
                if (element.TryGetDouble(out var d)) return d;
                return element.GetRawText();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            default:
                return string.Empty;
        }
    }

    private bool VerifyCore(string rawPayload, string? signatureHeader, string checksumKey)
    {
        if (string.IsNullOrWhiteSpace(checksumKey))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            var root = document.RootElement;
            var signature = ExtractSignature(root, signatureHeader);
            if (string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            var data = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("data", out var dataElement)
                && dataElement.ValueKind == JsonValueKind.Object
                    ? dataElement
                    : root;

            var canonicalData = BuildCanonicalData(data);
            var expected = ComputeHmacSha256(canonicalData, checksumKey);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ExtractSignature(JsonElement root, string? signatureHeader)
    {
        if (!string.IsNullOrWhiteSpace(signatureHeader))
        {
            return signatureHeader;
        }

        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("signature", out var signature)
            && signature.ValueKind == JsonValueKind.String
                ? signature.GetString()
                : null;
    }

    private static string BuildCanonicalData(JsonElement data)
    {
        var values = data.EnumerateObject()
            .Where(x => !string.Equals(x.Name, "signature", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => $"{x.Name}={ToCanonicalValue(x.Value)}");

        return string.Join('&', values);
    }

    private static string ToCanonicalValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText()
        };
    }

    private string ComputeHmacSha256(string data, string checksumKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }
}
