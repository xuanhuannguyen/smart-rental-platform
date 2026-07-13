export type ViewingAppointmentStatus =
  | 'Pending'
  | 'Confirmed'
  | 'Rejected'
  | 'CancelledByTenant'
  | 'CancelledByLandlord'
  | 'Completed'
  | 'Expired';

export interface ViewingAppointment {
  id: string;
  roomId: string;
  roomingHouseId?: string | null;
  tenantUserId: string;
  createdByUserId: string;
  scheduledAt: string;
  durationMinutes: number;
  status: ViewingAppointmentStatus;
  tenantNote?: string | null;
  landlordNote?: string | null;
  cancelReason?: string | null;
  respondedAt?: string | null;
  proposedScheduledAt?: string | null;
  proposedDurationMinutes?: number | null;
  createdAt: string;
  updatedAt: string;
  // UI extended info (populated from enriched server response)
  roomNumber?: string;
  roomingHouseName?: string;
  tenantDisplayName?: string | null;
}

export interface CreateViewingAppointmentRequest {
  roomId: string;
  scheduledAt: string; // ISO DateTime string
  durationMinutes?: number | null;
  tenantNote?: string | null;
}

export interface ConfirmViewingAppointmentRequest {
  confirmDespiteConflict: boolean;
  landlordNote?: string | null;
}

export interface RejectViewingAppointmentRequest {
  rejectReason: string;
  proposedScheduledAt?: string | null;
  proposedDurationMinutes?: number | null;
}

export interface CancelViewingAppointmentRequest {
  cancelReason?: string | null;
}

export interface ConflictingAppointmentDto {
  id: string;
  scheduledAt: string;
  durationMinutes: number;
  roomNumber: string;
  roomingHouseName: string;
}

export interface ConflictCheckResponse {
  hasConflict: boolean;
  message?: string | null;
  conflictingAppointments: ConflictingAppointmentDto[];
}
