using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Abstractions;
using SmartRentalPlatform.Application.Common;
using SmartRentalPlatform.Application.Services.Kyc;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Requests.Kyc;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/kyc")]
public class KycController : ControllerBase
{
    private readonly IKycService _kycService;
    private readonly ICurrentUserService _currentUser;

    public KycController(IKycService kycService, ICurrentUserService currentUser)
    {
        _kycService = kycService;
        _currentUser = currentUser;
    }

    [HttpPost("submissions")]
    public async Task<IActionResult> Submit([FromForm] SubmitKycRequest request)
    {
        if (!_currentUser.IsAuthenticated)
            return Unauthorized(ApiErrorResponse.Create(
                ErrorCodes.Unauthorized,
                "Authentication required."));

        try
        {
            var result = await _kycService.SubmitAsync(_currentUser.UserId!.Value, request);
            return Ok(ApiResponse<object>.Ok(result, result.Message));
        }
        catch (KycBusinessException ex)
        {
            return StatusCode(ex.StatusCode, ApiErrorResponse.Create(ex.Code, ex.Message));
        }
    }

    [HttpGet("my-status")]
    public async Task<IActionResult> GetMyStatus()
    {
        if (!_currentUser.IsAuthenticated)
            return Unauthorized(ApiErrorResponse.Create(
                ErrorCodes.Unauthorized,
                "Authentication required."));

        var result = await _kycService.GetMyStatusAsync(_currentUser.UserId!.Value);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("my-history")]
    public async Task<IActionResult> GetMyHistory()
    {
        if (!_currentUser.IsAuthenticated)
            return Unauthorized(ApiErrorResponse.Create(
                ErrorCodes.Unauthorized,
                "Authentication required."));

        var result = await _kycService.GetMyHistoryAsync(_currentUser.UserId!.Value);
        return Ok(ApiResponse<object>.Ok(result.Items));
    }
}
