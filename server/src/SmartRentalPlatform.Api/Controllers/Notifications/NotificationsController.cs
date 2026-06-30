using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Notifications.Responses;

namespace SmartRentalPlatform.Api.Controllers.Notifications;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;

    public NotificationsController(
        INotificationService notificationService,
        ICurrentUserService currentUserService)
    {
        _notificationService = notificationService;
        _currentUserService = currentUserService;
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<NotificationResponse>>>> GetNotifications(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.GetNotificationsAsync(userId, limit, cancellationToken);

        return Ok(new ApiResponse<List<NotificationResponse>>
        {
            Success = true,
            Message = "Tải danh sách thông báo thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var count = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);

        return Ok(new ApiResponse<int>
        {
            Success = true,
            Message = "Lấy số lượng thông báo chưa đọc thành công.",
            Data = count
        });
    }

    [Authorize]
    [HttpPatch("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAsRead(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        await _notificationService.MarkAsReadAsync(userId, id, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Đã đánh dấu thông báo đã đọc.",
        });
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        await _notificationService.DeleteAsync(userId, id, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Đã xóa thông báo.",
        });
    }

    [Authorize]
    [HttpPatch("read-all")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllAsRead(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Đã đánh dấu tất cả thông báo đã đọc.",
        });
    }

    private Guid GetCurrentUserId()
    {
        return _currentUserService.GetRequiredUserId("Không tìm thấy mã người dùng đã đăng nhập.");
    }
}
