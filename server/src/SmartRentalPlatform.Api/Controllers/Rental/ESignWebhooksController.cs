using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using System.Text.Json;

namespace SmartRentalPlatform.Api.Controllers.Rental;

[ApiController]
[Route("api/esign-webhooks")]
public class ESignWebhooksController : ControllerBase
{
    private readonly IContractESignService eSignService;
    private readonly ILogger<ESignWebhooksController> logger;

    public ESignWebhooksController(IContractESignService eSignService, ILogger<ESignWebhooksController> logger)
    {
        this.eSignService = eSignService;
        this.logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("vnpt")]
    public async Task<IActionResult> ProcessVnptWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(cancellationToken);
        
        var key = Request.Headers["X-APP-CB-KEY"].ToString();
        var secret = Request.Headers["X-APP-CB-SECRET"].ToString();

        var signatureHeader = JsonSerializer.Serialize(new { Key = key, Secret = secret });

        try
        {
            await eSignService.ProcessProviderWebhookAsync(
                ESignProvider.Vnpt,
                rawPayload,
                signatureHeader,
                cancellationToken);
            return Ok(new { message = "Webhook received successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized webhook request from VNPT");
            return Unauthorized(new { message = "Invalid webhook credentials" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing VNPT webhook");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
