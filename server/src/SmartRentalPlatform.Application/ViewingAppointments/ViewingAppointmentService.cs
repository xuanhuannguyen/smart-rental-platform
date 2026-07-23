using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.ViewingAppointments.Requests;
using SmartRentalPlatform.Contracts.ViewingAppointments.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Notifications;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Users;
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
        private readonly INotificationService _notificationService;

        public ViewingAppointmentService(IAppDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<ViewingAppointmentResponse> CreateAsync(
            Guid tenantUserId,
            CreateViewingAppointmentRequest request,
            CancellationToken cancellationToken = default)
        {
            await EnsureRequesterIsNotAdminAsync(tenantUserId, cancellationToken);

            // Minimum advance time: phải đặt trước ít nhất 2 giờ
            if (request.ScheduledAt < DateTimeOffset.UtcNow.AddHours(2))
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentTimeInPast,
                    "Thời gian hẹn phải cách hiện tại ít nhất 2 giờ.");
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

            // Chặn landlord tự đặt lịch xem phòng chính mình
            if (house.LandlordUserId == tenantUserId)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentNotAllowed,
                    "Bạn không thể đặt lịch xem phòng trọ của chính mình.");
            }

            // Chặn duplicate: tenant đã có appointment Pending/Confirmed cho cùng room,
            // hoặc Rejected nhưng vẫn còn đề xuất đang chờ phản hồi (ProposedScheduledAt != null)
            var existingActive = await _context.ViewingAppointments
                .AnyAsync(x => x.RoomId == request.RoomId
                    && x.TenantUserId == tenantUserId
                    && (x.Status == ViewingAppointmentStatus.Pending
                        || x.Status == ViewingAppointmentStatus.Confirmed
                        || (x.Status == ViewingAppointmentStatus.Rejected && x.ProposedScheduledAt != null)),
                    cancellationToken);

            if (existingActive)
            {
                throw new ConflictException(
                    ErrorCodes.ViewingAppointmentDuplicate,
                    "Bạn đã có lịch hẹn cho phòng này và chu trình chưa kết thúc. Vui lòng chờ hoặc liên hệ chủ trọ.");
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

            // Reload entity with navigation properties for enriched response
            var savedAppointment = await _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .Include(x => x.TenantUser)
                .FirstAsync(x => x.Id == appointment.Id, cancellationToken);

            // Send notification to Landlord (BR-VIEW-CREATE-11)
            await _notificationService.CreateAsync(
                userId: house.LandlordUserId,
                type: NotificationType.NewViewingAppointment,
                title: "📅 Lịch xem phòng mới",
                body: $"{savedAppointment.TenantUser?.DisplayName ?? "Khách hàng"} muốn xem phòng {room.RoomNumber} tại {house.Name} lúc {appointment.ScheduledAt:HH:mm 'ngày' dd/MM/yyyy}.",
                referenceId: appointment.Id.ToString(),
                referenceType: "ViewingAppointment",
                cancellationToken: cancellationToken);

            return ViewingAppointmentMapper.ToResponse(savedAppointment);
        }

        private async Task EnsureRequesterIsNotAdminAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var isAdmin = await _context.UserRoles
                .AsNoTracking()
                .AnyAsync(x => x.UserId == userId && x.Role.Name == RoleName.Admin, cancellationToken);

            if (isAdmin)
            {
                throw new ForbiddenException(
                    ErrorCodes.Forbidden,
                    "Tài khoản admin chỉ được xem thông tin khu trọ, không thể đặt lịch xem phòng.");
            }
        }

        public async Task<List<ViewingAppointmentResponse>> GetMyAppointmentsAsync(
            Guid tenantUserId,
            CancellationToken cancellationToken = default)
        {
            // Auto-expire Pending appointments that are past their scheduled time
            await AutoExpirePendingAsync(
                _context.ViewingAppointments.Where(x => x.TenantUserId == tenantUserId),
                cancellationToken);

            var appointments = await _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .Include(x => x.TenantUser)
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
            // Auto-expire Pending appointments that are past their scheduled time
            await AutoExpirePendingAsync(
                _context.ViewingAppointments
                    .Include(x => x.Room)
                    .ThenInclude(r => r.RoomingHouse)
                    .Where(x => x.Room.RoomingHouse.LandlordUserId == landlordUserId),
                cancellationToken);

            var query = _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .Include(x => x.TenantUser)
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
                .Include(x => x.TenantUser)
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

            // Send notification to Tenant (BR-CONFIRM-05/Landlord note)
            await _notificationService.CreateAsync(
                userId: appointment.TenantUserId,
                type: NotificationType.ViewingAppointmentConfirmed,
                title: "✅ Lịch xem phòng đã được xác nhận",
                body: $"Chủ trọ đã xác nhận lịch xem phòng {appointment.Room?.RoomNumber ?? ""} lúc {appointment.ScheduledAt:HH:mm 'ngày' dd/MM/yyyy}.",
                referenceId: appointment.Id.ToString(),
                referenceType: "ViewingAppointment",
                cancellationToken: cancellationToken);

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
                .Include(x => x.TenantUser)
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

            // Validate proposal if provided
            if (request.ProposedScheduledAt.HasValue)
            {
                if (request.ProposedScheduledAt.Value <= DateTimeOffset.UtcNow.AddHours(1))
                {
                    throw new BadRequestException(
                        ErrorCodes.ViewingAppointmentTimeInPast,
                        "Thời gian đề xuất phải cách hiện tại ít nhất 1 giờ.");
                }

                var proposedDuration = request.ProposedDurationMinutes ?? appointment.DurationMinutes;
                if (proposedDuration <= 0)
                {
                    throw new BadRequestException(
                        ErrorCodes.ValidationError,
                        "Thời lượng đề xuất (proposed_duration_minutes) phải lớn hơn 0.");
                }

                appointment.ProposedScheduledAt = request.ProposedScheduledAt.Value;
                appointment.ProposedDurationMinutes = proposedDuration;
            }

            appointment.Status = ViewingAppointmentStatus.Rejected;
            appointment.RespondedAt = DateTimeOffset.UtcNow;
            // Lưu reject reason vào CancelReason (field dùng chung cho cả reject/cancel reason)
            appointment.CancelReason = request.RejectReason.Trim();
            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Send notification to Tenant — with or without proposal info
            if (request.ProposedScheduledAt.HasValue)
            {
                await _notificationService.CreateAsync(
                    userId: appointment.TenantUserId,
                    type: NotificationType.ViewingAppointmentRejected,
                    title: "📅 Chủ trọ đề xuất lịch mới",
                    body: $"Chủ trọ đã từ chối lịch xem phòng {appointment.Room?.RoomNumber ?? ""} và đề xuất khung giờ mới: {request.ProposedScheduledAt.Value:HH:mm 'ngày' dd/MM/yyyy}. Vui lòng phản hồi.",
                    referenceId: appointment.Id.ToString(),
                    referenceType: "ViewingAppointment",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await _notificationService.CreateAsync(
                    userId: appointment.TenantUserId,
                    type: NotificationType.ViewingAppointmentRejected,
                    title: "❌ Lịch xem phòng bị từ chối",
                    body: $"Lịch xem phòng {appointment.Room?.RoomNumber ?? ""} đã bị chủ trọ từ chối. Lý do: {request.RejectReason.Trim()}",
                    referenceId: appointment.Id.ToString(),
                    referenceType: "ViewingAppointment",
                    cancellationToken: cancellationToken);
            }

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
                .Include(x => x.TenantUser)
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
            appointment.RespondedAt = DateTimeOffset.UtcNow;
            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Send notification to Landlord (BR-VIEW-TENANT-08)
            await _notificationService.CreateAsync(
                userId: appointment.Room.RoomingHouse.LandlordUserId,
                type: NotificationType.ViewingAppointmentCancelled,
                title: "🗑️ Khách thuê đã hủy lịch hẹn",
                body: $"Khách thuê đã hủy lịch xem phòng {appointment.Room?.RoomNumber ?? ""} lúc {appointment.ScheduledAt:HH:mm 'ngày' dd/MM/yyyy}.",
                referenceId: appointment.Id.ToString(),
                referenceType: "ViewingAppointment",
                cancellationToken: cancellationToken);

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
                .Include(x => x.TenantUser)
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
            appointment.RespondedAt = DateTimeOffset.UtcNow;
            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Send notification to Tenant
            await _notificationService.CreateAsync(
                userId: appointment.TenantUserId,
                type: NotificationType.ViewingAppointmentCancelled,
                title: "🗑️ Chủ trọ đã hủy lịch hẹn",
                body: $"Chủ trọ đã hủy lịch xem phòng {appointment.Room?.RoomNumber ?? ""} lúc {appointment.ScheduledAt:HH:mm 'ngày' dd/MM/yyyy}. Lý do: {request.CancelReason?.Trim()}",
                referenceId: appointment.Id.ToString(),
                referenceType: "ViewingAppointment",
                cancellationToken: cancellationToken);

            return ViewingAppointmentMapper.ToResponse(appointment);
        }

        public async Task<ViewingAppointmentResponse> AcceptProposalAsync(
            Guid tenantUserId,
            Guid appointmentId,
            CancellationToken cancellationToken = default)
        {
            var appointment = await _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .Include(x => x.TenantUser)
                .FirstOrDefaultAsync(x => x.Id == appointmentId && x.TenantUserId == tenantUserId, cancellationToken);

            if (appointment == null)
            {
                throw new NotFoundException(
                    ErrorCodes.ViewingAppointmentNotFound,
                    "Không tìm thấy lịch hẹn xem phòng.");
            }

            if (appointment.Status != ViewingAppointmentStatus.Rejected)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentInvalidStatus,
                    "Chỉ có thể chấp nhận đề xuất lịch hẹn đã bị từ chối (Rejected).");
            }

            if (!appointment.ProposedScheduledAt.HasValue)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentInvalidStatus,
                    "Lịch hẹn này không có đề xuất mới từ chủ trọ.");
            }

            // Apply the proposal — update actual schedule to proposed values
            appointment.ScheduledAt = appointment.ProposedScheduledAt.Value;
            appointment.DurationMinutes = appointment.ProposedDurationMinutes ?? appointment.DurationMinutes;
            appointment.Status = ViewingAppointmentStatus.Confirmed;
            appointment.ProposedScheduledAt = null;
            appointment.ProposedDurationMinutes = null;
            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Notify landlord
            await _notificationService.CreateAsync(
                userId: appointment.Room.RoomingHouse.LandlordUserId,
                type: NotificationType.ViewingAppointmentConfirmed,
                title: "✅ Khách thuê đã chấp nhận đề xuất",
                body: $"Khách thuê đã đồng ý với lịch xem phòng {appointment.Room?.RoomNumber ?? ""} lúc {appointment.ScheduledAt:HH:mm 'ngày' dd/MM/yyyy}.",
                referenceId: appointment.Id.ToString(),
                referenceType: "ViewingAppointment",
                cancellationToken: cancellationToken);

            return ViewingAppointmentMapper.ToResponse(appointment);
        }

        public async Task<ViewingAppointmentResponse> RejectProposalAsync(
            Guid tenantUserId,
            Guid appointmentId,
            CancellationToken cancellationToken = default)
        {
            var appointment = await _context.ViewingAppointments
                .Include(x => x.Room)
                .ThenInclude(r => r.RoomingHouse)
                .Include(x => x.TenantUser)
                .FirstOrDefaultAsync(x => x.Id == appointmentId && x.TenantUserId == tenantUserId, cancellationToken);

            if (appointment == null)
            {
                throw new NotFoundException(
                    ErrorCodes.ViewingAppointmentNotFound,
                    "Không tìm thấy lịch hẹn xem phòng.");
            }

            if (appointment.Status != ViewingAppointmentStatus.Rejected)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentInvalidStatus,
                    "Chỉ có thể từ chối đề xuất của lịch hẹn đã bị từ chối (Rejected).");
            }

            if (!appointment.ProposedScheduledAt.HasValue)
            {
                throw new BadRequestException(
                    ErrorCodes.ViewingAppointmentInvalidStatus,
                    "Lịch hẹn này không có đề xuất mới từ chủ trọ.");
            }

            // Clear proposal — stay Rejected (final)
            appointment.ProposedScheduledAt = null;
            appointment.ProposedDurationMinutes = null;
            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Notify landlord
            await _notificationService.CreateAsync(
                userId: appointment.Room.RoomingHouse.LandlordUserId,
                type: NotificationType.ViewingAppointmentRejected,
                title: "❌ Khách thuê từ chối đề xuất",
                body: $"Khách thuê đã từ chối đề xuất lịch xem phòng {appointment.Room?.RoomNumber ?? ""}.",
                referenceId: appointment.Id.ToString(),
                referenceType: "ViewingAppointment",
                cancellationToken: cancellationToken);

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
                .Include(x => x.TenantUser)
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

            // Send notification to Tenant
            await _notificationService.CreateAsync(
                userId: appointment.TenantUserId,
                type: NotificationType.ViewingAppointmentCompleted,
                title: "✅ Buổi xem phòng đã hoàn tất",
                body: $"Buổi xem phòng {appointment.Room?.RoomNumber ?? ""} đã được chủ trọ đánh dấu hoàn tất.",
                referenceId: appointment.Id.ToString(),
                referenceType: "ViewingAppointment",
                cancellationToken: cancellationToken);

            return ViewingAppointmentMapper.ToResponse(appointment);
        }

        /// <summary>
        /// Auto-expire Pending appointments whose ScheduledAt has already passed.
        /// Called inline during list queries so no background job is needed.
        /// </summary>
        private async Task AutoExpirePendingAsync(
            IQueryable<ViewingAppointment> scopedQuery,
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var expiredList = await scopedQuery
                .Where(x => x.Status == ViewingAppointmentStatus.Pending && x.ScheduledAt <= now)
                .ToListAsync(cancellationToken);

            if (expiredList.Count == 0)
                return;

            foreach (var appt in expiredList)
            {
                appt.Status = ViewingAppointmentStatus.Expired;
                appt.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
