using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Infrastructure.Options;

namespace SmartRentalPlatform.Infrastructure.ExternalServices.ESign;

public class ESignWebhookVerifier : IESignWebhookVerifier
{
    private readonly ESignOptions _options;

    public ESignWebhookVerifier(IOptions<ESignOptions> options)
    {
        _options = options.Value;
    }

    public bool VerifySignature(string? signatureHeader, string rawPayload)
    {
        if (string.IsNullOrEmpty(signatureHeader))
            return false;

        try
        {
            var headerObj = System.Text.Json.JsonSerializer.Deserialize<VnptWebhookHeader>(signatureHeader, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (headerObj == null)
                return false;

            return headerObj.Key == _options.WebhookCallbackKey && 
                   headerObj.Secret == _options.WebhookCallbackSecret;
        }
        catch
        {
            return false;
        }
    }

    private class VnptWebhookHeader
    {
        public string? Key { get; set; }
        public string? Secret { get; set; }
    }
}
