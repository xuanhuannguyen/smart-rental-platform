using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Payments;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Payments.Requests;
using SmartRentalPlatform.Contracts.Payments.Responses;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/dev/mock-payments")]
public class DevMockPaymentsController : ControllerBase
{
    private readonly IMockPaymentService mockPaymentService;
    private readonly IWebHostEnvironment environment;

    public DevMockPaymentsController(
        IMockPaymentService mockPaymentService,
        IWebHostEnvironment environment)
    {
        this.mockPaymentService = mockPaymentService;
        this.environment = environment;
    }

    [HttpPost("{paymentTransactionId:guid}/success")]
    public async Task<ActionResult<ApiResponse<PaymentWebhookProcessingResponse>>> Success(
        Guid paymentTransactionId,
        MockPaymentRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsMockEnabled())
        {
            return NotFound();
        }

        var result = await mockPaymentService.SimulateSuccessAsync(paymentTransactionId, request, cancellationToken);
        return Ok(new ApiResponse<PaymentWebhookProcessingResponse>
        {
            Success = true,
            Message = "Mock payment success processed.",
            Data = result
        });
    }

    [HttpPost("{paymentTransactionId:guid}/failed")]
    public async Task<ActionResult<ApiResponse<PaymentWebhookProcessingResponse>>> Failed(
        Guid paymentTransactionId,
        MockPaymentRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsMockEnabled())
        {
            return NotFound();
        }

        var result = await mockPaymentService.SimulateFailedAsync(paymentTransactionId, request, cancellationToken);
        return Ok(new ApiResponse<PaymentWebhookProcessingResponse>
        {
            Success = true,
            Message = "Mock payment failed processed.",
            Data = result
        });
    }

    private bool IsMockEnabled()
    {
        return environment.IsDevelopment();
    }
}
