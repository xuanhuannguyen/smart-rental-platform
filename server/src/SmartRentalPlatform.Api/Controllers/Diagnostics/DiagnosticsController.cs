using Microsoft.AspNetCore.Mvc;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/diagnostics")]
public sealed class DiagnosticsController : ControllerBase
{
    private readonly IHttpClientFactory httpClientFactory;

    public DiagnosticsController(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    [HttpGet("outbound-ip")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetOutboundIp(CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        using var response = await httpClient.GetAsync("https://api.ipify.org", cancellationToken);
        response.EnsureSuccessStatusCode();

        var ip = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

        return Ok(new
        {
            ip,
            checkedAt = DateTimeOffset.UtcNow
        });
    }
}
