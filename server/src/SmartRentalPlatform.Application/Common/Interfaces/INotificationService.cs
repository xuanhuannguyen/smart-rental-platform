using SmartRentalPlatform.Contracts.Notifications.Responses;
using SmartRentalPlatform.Domain.Enums.Notifications;

namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface INotificationService
{
    Task CreateAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? referenceId = null,
        string? referenceType = null,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<NotificationResponse>> GetNotificationsAsync(
        Guid userId,
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
