using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using SmartRentalPlatform.Application.Payments;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Payments.Responses;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payment-webhooks")]
public class PaymentWebhooksController : ControllerBase
{
    private readonly IPaymentWebhookService paymentWebhookService;

    public PaymentWebhooksController(IPaymentWebhookService paymentWebhookService)
    {
        this.paymentWebhookService = paymentWebhookService;
    }

    [HttpPost("payos")]
    public async Task<ActionResult<ApiResponse<PaymentWebhookProcessingResponse>>> ProcessPayOSWebhook(
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawPayload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["x-payos-signature"].FirstOrDefault();
        var result = await paymentWebhookService.ProcessPayOSWebhookAsync(rawPayload, signature, cancellationToken);

        return Ok(new ApiResponse<PaymentWebhookProcessingResponse>
        {
            Success = true,
            Message = "PayOS webhook received.",
            Data = result
        });
    }
}
