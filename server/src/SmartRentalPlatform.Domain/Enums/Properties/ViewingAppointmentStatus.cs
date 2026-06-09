using System;

namespace SmartRentalPlatform.Domain.Enums.Properties
{
    public enum ViewingAppointmentStatus
    {
        Pending,
        Confirmed,
        Rejected,
        CancelledByTenant,
        CancelledByLandlord,
        Completed,
        Expired
    }
}
