using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Infrastructure.ExternalServices.PayOS;
using SmartRentalPlatform.Infrastructure.Options;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Payments;

public class PayOSWebhookSignatureVerifierTests
{
    private readonly PayOSOptions _options;
    private readonly PayOSWebhookSignatureVerifier _verifier;

    public PayOSWebhookSignatureVerifierTests()
    {
        _options = new PayOSOptions
        {
            PayoutChecksumKey = "test_payout_checksum_key",
            ChecksumKey = "test_payment_checksum_key"
        };
        _verifier = new PayOSWebhookSignatureVerifier(Options.Create(_options));
    }

    [Fact]
    public void VerifyPayout_WithValidPayloadAndSignature_ReturnsTrue()
    {
        // Sample PayOS payout payload
        var payload = new
        {
            code = "00",
            desc = "success",
            data = new
            {
                referenceId = "WD-12345",
                approvalState = "SUCCEEDED",
                payouts = new[]
                {
                    new
                    {
                        referenceId = "WD-12345",
                        amount = 100000,
                        description = "WD WD-12345",
                        toBin = "970415",
                        toAccountNumber = "1234567890",
                        category = new[] { "withdrawal" }
                    }
                }
            }
        };

        var rawPayload = JsonSerializer.Serialize(payload);

        // Calculate expected signature manually
        // canonicalData: key order in data: approvalState, payouts, referenceId
        // payouts: serialized JSON. Inner elements of payouts: amount, category, description, referenceId, toAccountNumber, toBin
        // category: ["withdrawal"]
        var expectedPayoutJson = "%5B%7B%22amount%22%3A100000%2C%22category%22%3A%5B%22withdrawal%22%5D%2C%22description%22%3A%22WD%20WD-12345%22%2C%22referenceId%22%3A%22WD-12345%22%2C%22toAccountNumber%22%3A%221234567890%22%2C%22toBin%22%3A%22970415%22%7D%5D";
        var canonicalData = $"approvalState=SUCCEEDED&payouts={expectedPayoutJson}&referenceId=WD-12345";

        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_options.PayoutChecksumKey));
        var signature = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonicalData))).ToLowerInvariant();

        // Verify with signature passed in header
        var resultHeader = _verifier.VerifyPayout(rawPayload, signature);
        Assert.True(resultHeader);

        // Verify with signature in body
        var bodyWithSignature = new
        {
            code = "00",
            desc = "success",
            data = payload.data,
            signature = signature
        };
        var rawPayloadBody = JsonSerializer.Serialize(bodyWithSignature);
        var resultBody = _verifier.VerifyPayout(rawPayloadBody, null);
        Assert.True(resultBody);
    }

    [Fact]
    public void VerifyPayout_WithNullValues_TranslatesToEmptyString()
    {
        var payload = new
        {
            code = "00",
            desc = "success",
            data = new
            {
                referenceId = "WD-12345",
                approvalState = "SUCCEEDED",
                payouts = new[]
                {
                    new
                    {
                        referenceId = "WD-12345",
                        amount = 100000,
                        description = (string?)null, // Should become empty string ""
                        toBin = "970415",
                        toAccountNumber = "1234567890",
                        category = new[] { "withdrawal" }
                    }
                }
            }
        };

        var rawPayload = JsonSerializer.Serialize(payload);

        // In deep sorting, null values inside objects/arrays become empty strings ""
        // So description should be serialized as "" (i.e. "description":"" in JSON)
        var expectedPayoutJson = "%5B%7B%22amount%22%3A100000%2C%22category%22%3A%5B%22withdrawal%22%5D%2C%22description%22%3A%22%22%2C%22referenceId%22%3A%22WD-12345%22%2C%22toAccountNumber%22%3A%221234567890%22%2C%22toBin%22%3A%22970415%22%7D%5D";
        var canonicalData = $"approvalState=SUCCEEDED&payouts={expectedPayoutJson}&referenceId=WD-12345";

        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_options.PayoutChecksumKey));
        var signature = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonicalData))).ToLowerInvariant();

        var result = _verifier.VerifyPayout(rawPayload, signature);
        Assert.True(result);
    }

    [Fact]
    public void VerifyPayout_WithInvalidSignature_ReturnsFalse()
    {
        var rawPayload = "{\"code\":\"00\",\"data\":{\"referenceId\":\"WD-123\"}}";
        var result = _verifier.VerifyPayout(rawPayload, "invalid_signature");
        Assert.False(result);
    }
}
