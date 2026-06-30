using SmartRentalPlatform.Contracts.Notifications.Responses;
using SmartRentalPlatform.Domain.Entities.Notifications;

namespace SmartRentalPlatform.Application.Notifications;

internal static class NotificationMapper
{
    public static NotificationResponse ToResponse(Notification entity)
    {
        return new NotificationResponse
        {
            Id = entity.Id,
            Type = entity.Type.ToString(),
            Title = entity.Title,
            Body = entity.Body,
            ReferenceId = entity.ReferenceId,
            ReferenceType = entity.ReferenceType,
            IsRead = entity.IsRead,
            CreatedAt = entity.CreatedAt,
        };
    }
}
