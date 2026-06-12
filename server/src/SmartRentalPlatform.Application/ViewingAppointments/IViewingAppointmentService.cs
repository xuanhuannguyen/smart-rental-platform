using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartRentalPlatform.Contracts.ViewingAppointments.Requests;
using SmartRentalPlatform.Contracts.ViewingAppointments.Responses;

namespace SmartRentalPlatform.Application.ViewingAppointments
{
    public interface IViewingAppointmentService
    {
        Task<ViewingAppointmentResponse> CreateAsync(
            Guid tenantUserId,
            CreateViewingAppointmentRequest request,
            CancellationToken cancellationToken = default);

        Task<List<ViewingAppointmentResponse>> GetMyAppointmentsAsync(
            Guid tenantUserId,
            CancellationToken cancellationToken = default);

        Task<List<ViewingAppointmentResponse>> GetLandlordAppointmentsAsync(
            Guid landlordUserId,
            string? status = null,
            CancellationToken cancellationToken = default);

        Task<ConflictCheckResponse> CheckConflictAsync(
            Guid landlordUserId,
            Guid appointmentId,
            CancellationToken cancellationToken = default);

        Task<ViewingAppointmentResponse> ConfirmAsync(
            Guid landlordUserId,
            Guid appointmentId,
            ConfirmViewingAppointmentRequest request,
            CancellationToken cancellationToken = default);

        Task<ViewingAppointmentResponse> RejectAsync(
            Guid landlordUserId,
            Guid appointmentId,
            RejectViewingAppointmentRequest request,
            CancellationToken cancellationToken = default);

        Task<ViewingAppointmentResponse> CancelByTenantAsync(
            Guid tenantUserId,
            Guid appointmentId,
            CancelViewingAppointmentRequest request,
            CancellationToken cancellationToken = default);

        Task<ViewingAppointmentResponse> CancelByLandlordAsync(
            Guid landlordUserId,
            Guid appointmentId,
            CancelViewingAppointmentRequest request,
            CancellationToken cancellationToken = default);

        Task<ViewingAppointmentResponse> CompleteAsync(
            Guid landlordUserId,
            Guid appointmentId,
            CancellationToken cancellationToken = default);
    }
}
