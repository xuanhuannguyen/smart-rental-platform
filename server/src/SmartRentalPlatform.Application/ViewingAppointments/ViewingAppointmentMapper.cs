using SmartRentalPlatform.Contracts.ViewingAppointments.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Application.ViewingAppointments
{
    internal static class ViewingAppointmentMapper
    {
        public static ViewingAppointmentResponse ToResponse(ViewingAppointment entity)
        {
            return new ViewingAppointmentResponse
            {
                Id = entity.Id,
                RoomId = entity.RoomId,
                TenantUserId = entity.TenantUserId,
                CreatedByUserId = entity.CreatedByUserId,
                ScheduledAt = entity.ScheduledAt,
                DurationMinutes = entity.DurationMinutes,
                Status = entity.Status.ToString(),
                TenantNote = entity.TenantNote,
                LandlordNote = entity.LandlordNote,
                CancelReason = entity.CancelReason,
                RespondedAt = entity.RespondedAt,
                ProposedScheduledAt = entity.ProposedScheduledAt,
                ProposedDurationMinutes = entity.ProposedDurationMinutes,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                RoomNumber = entity.Room?.RoomNumber,
                RoomingHouseName = entity.Room?.RoomingHouse?.Name,
                TenantDisplayName = entity.TenantUser?.DisplayName
            };
        }
    }
}
