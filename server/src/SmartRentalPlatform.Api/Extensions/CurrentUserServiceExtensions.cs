using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Api.Extensions;

public static class CurrentUserServiceExtensions
{
    public static Guid GetRequiredUserId(this ICurrentUserService currentUserService, string message)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            throw new UnauthorizedException(ErrorCodes.Unauthorized, message);
        }

        return currentUserService.UserId.Value;
    }

    public static Guid GetRequiredUserIdForAction(this ICurrentUserService currentUserService)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Bạn cần đăng nhập để thực hiện thao tác này.");
        }

        return currentUserService.UserId.Value;
    }
}
