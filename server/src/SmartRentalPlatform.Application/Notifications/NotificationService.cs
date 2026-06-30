using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Notifications.Responses;
using SmartRentalPlatform.Domain.Entities.Notifications;
using SmartRentalPlatform.Domain.Enums.Notifications;

namespace SmartRentalPlatform.Application.Notifications;

public class NotificationService : INotificationService
{
    private readonly IAppDbContext _context;

    public NotificationService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? referenceId = null,
        string? referenceType = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);
    }

    public async Task<List<NotificationResponse>> GetNotificationsAsync(
        Guid userId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _context.Notifications
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return notifications.Select(NotificationMapper.ToResponse).ToList();
    }

    public async Task MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, cancellationToken);

        if (notification == null)
            throw new NotFoundException("NOTIFICATION_NOT_FOUND", "Không tìm thấy thông báo.");

        notification.IsRead = true;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var unreadNotifications = await _context.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, cancellationToken);

        if (notification == null)
            throw new NotFoundException("NOTIFICATION_NOT_FOUND", "Không tìm thấy thông báo.");

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
