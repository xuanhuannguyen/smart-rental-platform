using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using SmartRentalPlatform.Application.Payments;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Payments.Responses;
using SmartRentalPlatform.Contracts.Wallets.Requests;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payment-webhooks")]
public class PaymentWebhooksController : ControllerBase
{
    private readonly IPaymentWebhookService paymentWebhookService;
    private readonly IWithdrawalWebhookService withdrawalWebhookService;

    public PaymentWebhooksController(
        IPaymentWebhookService paymentWebhookService,
        IWithdrawalWebhookService withdrawalWebhookService)
    {
        this.paymentWebhookService = paymentWebhookService;
        this.withdrawalWebhookService = withdrawalWebhookService;
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

    [HttpPost("payos/payout")]
    public async Task<IActionResult> ProcessPayOSPayoutWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawPayload = await reader.ReadToEndAsync(cancellationToken);
        PayOSPayoutWebhookRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<PayOSPayoutWebhookRequest>(rawPayload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return BadRequest();
        }

        var signature = Request.Headers["x-payos-signature"].FirstOrDefault()
            ?? request?.Signature;

        var payout = request?.Data?.Payouts?.FirstOrDefault();
        if (payout == null)
        {
            return BadRequest();
        }

        var status = payout.Transactions?.FirstOrDefault()?.State ?? payout.ApprovalState;

        await withdrawalWebhookService.ProcessWebhookAsync(
            payout.ReferenceId,
            status,
            rawPayload,
            signature,
            false,
            cancellationToken);

        return Ok(new { success = true });
    }
}
