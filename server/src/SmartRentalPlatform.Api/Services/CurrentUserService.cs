using System.Security.Claims;
using SmartRentalPlatform.Application.Abstractions;

namespace SmartRentalPlatform.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    public bool IsAuthenticated => UserId.HasValue;

    public Guid? UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return null;

            var claimUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue(ClaimTypes.Name)
                ?? httpContext.User.FindFirstValue("sub");

            if (Guid.TryParse(claimUserId, out var fromClaim))
                return fromClaim;

            if (_environment.IsDevelopment() &&
                httpContext.Request.Headers.TryGetValue("X-Dev-User-Id", out var devHeader) &&
                Guid.TryParse(devHeader.FirstOrDefault(), out var fromHeader))
            {
                return fromHeader;
            }

            if (_environment.IsDevelopment() &&
                httpContext.Request.Query.TryGetValue("userId", out var queryValue) &&
                Guid.TryParse(queryValue.FirstOrDefault(), out var fromQuery))
            {
                return fromQuery;
            }

            return null;
        }
    }
}
