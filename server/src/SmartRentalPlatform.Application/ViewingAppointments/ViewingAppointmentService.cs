using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.ViewingAppointments.Requests;
using SmartRentalPlatform.Contracts.ViewingAppointments.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartRentalPlatform.Application.ViewingAppointments
{
    public class ViewingAppointmentService : IViewingAppointmentService
    {
        private readonly IAppDbContext _context;

        public ViewingAppointmentService(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ViewingAppointmentResponse> CreateAsync(
            Guid tenantUserId,
            CreateViewingAppointmentRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.ScheduledAt <= DateTimeOffset.UtcNow)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentTimeInPast,
                    "Không thể đặt lịch xem phòng ở thời gian trong quá khứ.");
            }

            var room = await _context.Rooms
                .Include(r => r.RoomingHouse)
                .FirstOrDefaultAsync(r => r.Id == request.RoomId, cancellationToken);

            if (room == null)
            {
                throw new NotFoundException(
                    ErrorCodes.RoomNotFound,
                    "Không tìm thấy phòng trọ.");
            }

            if (room.Status != RoomStatus.Available)
            {
                throw new BadRequestException(
                    ErrorCodes.RoomNotAvailable,
                    "Phòng trọ này không ở trạng thái sẵn sàng để xem.");
            }

            var house = room.RoomingHouse;
            if (house == null)
            {
                throw new NotFoundException(
                    ErrorCodes.HouseNotFound,
                    "Không tìm thấy thông tin khu trọ của phòng này.");
            }

            if (house.ApprovalStatus != RoomingHouseApprovalStatus.Approved)
            {
                throw new BadRequestException(
                    ErrorCodes.HouseNotApproved,
                    "Khu trọ chứa phòng này chưa được phê duyệt.");
            }

            if (house.VisibilityStatus != RoomingHouseVisibilityStatus.Visible)
            {
                throw new BadRequestException(
                    ErrorCodes.HouseNotPublic,
                    "Khu trọ chứa phòng này đang ở chế độ ẩn.");
            }

            int duration = request.DurationMinutes ?? 30;
            if (duration <= 0)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Thời lượng cuộc hẹn (duration_minutes) phải lớn hơn 0.");
            }

            var now = DateTimeOffset.UtcNow;
            var appointment = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = request.RoomId,
                TenantUserId = tenantUserId,
                CreatedByUserId = tenantUserId,
                ScheduledAt = request.ScheduledAt,
                DurationMinutes = duration,
                Status = ViewingAppointmentStatus.Pending,
                TenantNote = request.TenantNote,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.ViewingAppointments.Add(appointment);
            await _context.SaveChangesAsync(cancellationToken);

            // Stub for sending notification to Landlord (BR-VIEW-CREATE-11)
            Console.WriteLine($"[Notification Stub] Notify landlord {house.LandlordUserId} about new viewing appointment {appointment.Id}");

            return ViewingAppointmentMapper.ToResponse(appointment);
        }

        public async Task<List<ViewingAppointmentResponse>> GetMyAppointmentsAsync(
            Guid tenantUserId,
            CancellationToken cancellationToken = default)
        {
            var appointments = await _context.ViewingAppointments
                .Where(x => x.TenantUserId == tenantUserId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            return appointments.Select(ViewingAppointmentMapper.ToResponse).ToList();
        }

        public async Task<List<ViewingAppointmentResponse>> GetLandlordAppointmentsAsync(
            Guid landlordUserId,
            string? status = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .Where(x => x.Room.RoomingHouse.LandlordUserId == landlordUserId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<ViewingAppointmentStatus>(status, true, out var parsedStatus))
                {
                    query = query.Where(x => x.Status == parsedStatus);
                }
                else
                {
                    throw new BadRequestException(
                        ErrorCodes.InvalidStatus,
                        $"Trạng thái '{status}' không hợp lệ.");
                }
            }

            var appointments = await query
                .OrderBy(x => x.ScheduledAt)
                .ToListAsync(cancellationToken);

            return appointments.Select(ViewingAppointmentMapper.ToResponse).ToList();
        }

        public async Task<ConflictCheckResponse> CheckConflictAsync(
            Guid landlordUserId,
            Guid appointmentId,
            CancellationToken cancellationToken = default)
        {
            var appointment = await _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .FirstOrDefaultAsync(x => x.Id == appointmentId && x.Room.RoomingHouse.LandlordUserId == landlordUserId, cancellationToken);

            if (appointment == null)
            {
                throw new NotFoundException(
                    ErrorCodes.ViewingAppointmentNotFound,
                    "Không tìm thấy lịch hẹn xem phòng.");
            }

            var newStart = appointment.ScheduledAt;
            var newEnd = appointment.ScheduledAt.AddMinutes(appointment.DurationMinutes);

            var existingConfirmedList = await _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .Where(x => x.Room.RoomingHouse.LandlordUserId == landlordUserId
                            && x.Status == ViewingAppointmentStatus.Confirmed
                            && x.Id != appointmentId)
                .ToListAsync(cancellationToken);

            var conflictingList = new List<ConflictingAppointmentDto>();

            foreach (var existing in existingConfirmedList)
            {
                var existingStart = existing.ScheduledAt;
                var existingEnd = existing.ScheduledAt.AddMinutes(existing.DurationMinutes);

                // Overlap formula: newStart < existingEnd && newEnd > existingStart
                if (newStart < existingEnd && newEnd > existingStart)
                {
                    conflictingList.Add(new ConflictingAppointmentDto
                    {
                        Id = existing.Id,
                        ScheduledAt = existing.ScheduledAt,
                        DurationMinutes = existing.DurationMinutes,
                        RoomNumber = existing.Room.RoomNumber,
                        RoomingHouseName = existing.Room.RoomingHouse.Name
                    });
                }
            }

            var hasConflict = conflictingList.Count > 0;
            return new ConflictCheckResponse
            {
                HasConflict = hasConflict,
                Message = hasConflict ? $"Có {conflictingList.Count} lịch hẹn khác bị trùng giờ." : "Không có lịch hẹn bị trùng.",
                ConflictingAppointments = conflictingList
            };
        }

        public async Task<ViewingAppointmentResponse> ConfirmAsync(
            Guid landlordUserId,
            Guid appointmentId,
            ConfirmViewingAppointmentRequest request,
            CancellationToken cancellationToken = default)
        {
            var appointment = await _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .FirstOrDefaultAsync(x => x.Id == appointmentId && x.Room.RoomingHouse.LandlordUserId == landlordUserId, cancellationToken);

            if (appointment == null)
            {
                throw new NotFoundException(
                    ErrorCodes.ViewingAppointmentNotFound,
                    "Không tìm thấy lịch hẹn xem phòng.");
            }

            if (appointment.Status != ViewingAppointmentStatus.Pending)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentInvalidStatus,
                    "Chỉ có thể xác nhận lịch hẹn ở trạng thái Chờ duyệt (Pending).");
            }

            if (appointment.ScheduledAt <= DateTimeOffset.UtcNow)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentTimeInPast,
                    "Lịch hẹn đã quá hạn thời gian hẹn, không thể xác nhận.");
            }

            // Conflict Check
            var conflictCheck = await CheckConflictAsync(landlordUserId, appointmentId, cancellationToken);
            if (conflictCheck.HasConflict && !request.ConfirmDespiteConflict)
            {
                throw new ConflictException(
                    ErrorCodes.ViewingAppointmentConflict,
                    "Lịch hẹn bị trùng giờ với một lịch hẹn đã xác nhận khác của bạn.");
            }

            appointment.Status = ViewingAppointmentStatus.Confirmed;
            appointment.RespondedAt = DateTimeOffset.UtcNow;
            appointment.LandlordNote = request.LandlordNote;
            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Stub for sending notification to Tenant (BR-CONFIRM-05/Landlord note)
            Console.WriteLine($"[Notification Stub] Notify tenant {appointment.TenantUserId} about confirmed viewing appointment {appointment.Id}");

            return ViewingAppointmentMapper.ToResponse(appointment);
        }

        public async Task<ViewingAppointmentResponse> RejectAsync(
            Guid landlordUserId,
            Guid appointmentId,
            RejectViewingAppointmentRequest request,
            CancellationToken cancellationToken = default)
        {
            var appointment = await _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .FirstOrDefaultAsync(x => x.Id == appointmentId && x.Room.RoomingHouse.LandlordUserId == landlordUserId, cancellationToken);

            if (appointment == null)
            {
                throw new NotFoundException(
                    ErrorCodes.ViewingAppointmentNotFound,
                    "Không tìm thấy lịch hẹn xem phòng.");
            }

            if (appointment.Status != ViewingAppointmentStatus.Pending)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentInvalidStatus,
                    "Chỉ có thể từ chối lịch hẹn ở trạng thái Chờ duyệt (Pending).");
            }

            if (string.IsNullOrWhiteSpace(request.RejectReason))
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentReasonRequired,
                    "Lý do từ chối là bắt buộc.");
            }

            appointment.Status = ViewingAppointmentStatus.Rejected;
            appointment.RespondedAt = DateTimeOffset.UtcNow;
            appointment.CancelReason = request.RejectReason.Trim();
            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Stub for sending notification to Tenant
            Console.WriteLine($"[Notification Stub] Notify tenant {appointment.TenantUserId} that appointment {appointment.Id} was rejected. Reason: {request.RejectReason}");

            return ViewingAppointmentMapper.ToResponse(appointment);
        }

        public async Task<ViewingAppointmentResponse> CancelByTenantAsync(
            Guid tenantUserId,
            Guid appointmentId,
            CancelViewingAppointmentRequest request,
            CancellationToken cancellationToken = default)
        {
            var appointment = await _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .FirstOrDefaultAsync(x => x.Id == appointmentId && x.TenantUserId == tenantUserId, cancellationToken);

            if (appointment == null)
            {
                throw new NotFoundException(
                    ErrorCodes.ViewingAppointmentNotFound,
                    "Không tìm thấy lịch hẹn xem phòng.");
            }

            if (appointment.Status != ViewingAppointmentStatus.Pending && appointment.Status != ViewingAppointmentStatus.Confirmed)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentInvalidStatus,
                    "Chỉ có thể hủy lịch hẹn đang ở trạng thái Chờ duyệt (Pending) hoặc Đã xác nhận (Confirmed).");
            }

            appointment.Status = ViewingAppointmentStatus.CancelledByTenant;
            appointment.CancelReason = request.CancelReason?.Trim();
            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Stub for sending notification to Landlord (BR-VIEW-TENANT-08)
            Console.WriteLine($"[Notification Stub] Notify landlord {appointment.Room.RoomingHouse.LandlordUserId} that tenant {tenantUserId} cancelled appointment {appointment.Id}");

            return ViewingAppointmentMapper.ToResponse(appointment);
        }

        public async Task<ViewingAppointmentResponse> CancelByLandlordAsync(
            Guid landlordUserId,
            Guid appointmentId,
            CancelViewingAppointmentRequest request,
            CancellationToken cancellationToken = default)
        {
            var appointment = await _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .FirstOrDefaultAsync(x => x.Id == appointmentId && x.Room.RoomingHouse.LandlordUserId == landlordUserId, cancellationToken);

            if (appointment == null)
            {
                throw new NotFoundException(
                    ErrorCodes.ViewingAppointmentNotFound,
                    "Không tìm thấy lịch hẹn xem phòng.");
            }

            if (appointment.Status != ViewingAppointmentStatus.Confirmed)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentInvalidStatus,
                    "Landlord chỉ có thể hủy lịch hẹn đã được xác nhận (Confirmed).");
            }

            if (string.IsNullOrWhiteSpace(request.CancelReason))
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentReasonRequired,
                    "Lý do hủy lịch hẹn là bắt buộc.");
            }

            appointment.Status = ViewingAppointmentStatus.CancelledByLandlord;
            appointment.CancelReason = request.CancelReason.Trim();
            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Stub for sending notification to Tenant
            Console.WriteLine($"[Notification Stub] Notify tenant {appointment.TenantUserId} that landlord {landlordUserId} cancelled confirmed appointment {appointment.Id}. Reason: {request.CancelReason}");

            return ViewingAppointmentMapper.ToResponse(appointment);
        }

        public async Task<ViewingAppointmentResponse> CompleteAsync(
            Guid landlordUserId,
            Guid appointmentId,
            CancellationToken cancellationToken = default)
        {
            var appointment = await _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .FirstOrDefaultAsync(x => x.Id == appointmentId && x.Room.RoomingHouse.LandlordUserId == landlordUserId, cancellationToken);

            if (appointment == null)
            {
                throw new NotFoundException(
                    ErrorCodes.ViewingAppointmentNotFound,
                    "Không tìm thấy lịch hẹn xem phòng.");
            }

            if (appointment.Status != ViewingAppointmentStatus.Confirmed)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentInvalidStatus,
                    "Chỉ có thể hoàn tất lịch hẹn đã được xác nhận (Confirmed).");
            }

            if (appointment.ScheduledAt > DateTimeOffset.UtcNow)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentNotAllowed,
                    "Không thể hoàn tất lịch hẹn khi thời gian hẹn chưa đến.");
            }

            appointment.Status = ViewingAppointmentStatus.Completed;
            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Stub for sending notification to Tenant
            Console.WriteLine($"[Notification Stub] Notify tenant {appointment.TenantUserId} that appointment {appointment.Id} was completed.");

            return ViewingAppointmentMapper.ToResponse(appointment);
        }
    }
}
