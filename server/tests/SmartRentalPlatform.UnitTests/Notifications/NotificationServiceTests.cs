using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Notifications;
using SmartRentalPlatform.Domain.Entities.Notifications;
using SmartRentalPlatform.Domain.Enums.Notifications;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Notifications;

public class NotificationServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();

    [Fact]
    public async Task CreateAsync_PersistsUnreadNotificationWithReference()
    {
        var userId = Guid.NewGuid();
        var service = new NotificationService(_fixture.Context);

        await service.CreateAsync(
            userId,
            NotificationType.NewRentalRequest,
            "New request",
            "You have a rental request",
            "REQ-1",
            "RentalRequest");

        var notification = Assert.Single(_fixture.Context.Notifications);
        Assert.Equal(userId, notification.UserId);
        Assert.Equal(NotificationType.NewRentalRequest, notification.Type);
        Assert.Equal("New request", notification.Title);
        Assert.Equal("REQ-1", notification.ReferenceId);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyUnreadNotificationsForUser()
    {
        var userId = Guid.NewGuid();
        SeedNotification(userId, isRead: false);
        SeedNotification(userId, isRead: true);
        SeedNotification(Guid.NewGuid(), isRead: false);
        await _fixture.Context.SaveChangesAsync();

        var service = new NotificationService(_fixture.Context);

        var count = await service.GetUnreadCountAsync(userId);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetNotificationsAsync_ReturnsUserNotificationsOrderedByNewestAndLimited()
    {
        var userId = Guid.NewGuid();
        SeedNotification(userId, title: "old", createdAt: DateTimeOffset.UtcNow.AddMinutes(-3));
        SeedNotification(userId, title: "new", createdAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        SeedNotification(userId, title: "middle", createdAt: DateTimeOffset.UtcNow.AddMinutes(-2));
        SeedNotification(Guid.NewGuid(), title: "other", createdAt: DateTimeOffset.UtcNow);
        await _fixture.Context.SaveChangesAsync();

        var service = new NotificationService(_fixture.Context);

        var result = await service.GetNotificationsAsync(userId, limit: 2);

        Assert.Equal(["new", "middle"], result.Select(x => x.Title));
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotificationBelongsToUser_MarksItRead()
    {
        var userId = Guid.NewGuid();
        var notification = SeedNotification(userId, isRead: false);
        await _fixture.Context.SaveChangesAsync();

        var service = new NotificationService(_fixture.Context);

        await service.MarkAsReadAsync(userId, notification.Id);

        Assert.True(notification.IsRead);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotificationMissing_ThrowsNotFoundException()
    {
        var service = new NotificationService(_fixture.Context);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => service.MarkAsReadAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("NOTIFICATION_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksOnlyCurrentUsersUnreadNotifications()
    {
        var userId = Guid.NewGuid();
        var unread = SeedNotification(userId, isRead: false);
        var alreadyRead = SeedNotification(userId, isRead: true);
        var otherUser = SeedNotification(Guid.NewGuid(), isRead: false);
        await _fixture.Context.SaveChangesAsync();

        var service = new NotificationService(_fixture.Context);

        await service.MarkAllAsReadAsync(userId);

        Assert.True(unread.IsRead);
        Assert.True(alreadyRead.IsRead);
        Assert.False(otherUser.IsRead);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotificationBelongsToUser_RemovesIt()
    {
        var userId = Guid.NewGuid();
        var notification = SeedNotification(userId);
        var other = SeedNotification(Guid.NewGuid());
        await _fixture.Context.SaveChangesAsync();

        var service = new NotificationService(_fixture.Context);

        await service.DeleteAsync(userId, notification.Id);

        Assert.DoesNotContain(_fixture.Context.Notifications, x => x.Id == notification.Id);
        Assert.Contains(_fixture.Context.Notifications, x => x.Id == other.Id);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotificationMissing_ThrowsNotFoundException()
    {
        var service = new NotificationService(_fixture.Context);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("NOTIFICATION_NOT_FOUND", ex.ErrorCode);
    }

    private Notification SeedNotification(
        Guid userId,
        string title = "Title",
        bool isRead = false,
        DateTimeOffset? createdAt = null)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.NewViewingAppointment,
            Title = title,
            Body = "Body",
            IsRead = isRead,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };

        _fixture.Context.Notifications.Add(notification);
        return notification;
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
