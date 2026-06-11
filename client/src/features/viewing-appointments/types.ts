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
  tenantUserId: string;
  createdByUserId: string;
  scheduledAt: string;
  durationMinutes: number;
  status: ViewingAppointmentStatus;
  tenantNote?: string | null;
  landlordNote?: string | null;
  cancelReason?: string | null;
  respondedAt?: string | null;
  createdAt: string;
  updatedAt: string;
  // UI extended info (we will map them from responses if needed, or get them if room is included)
  roomNumber?: string;
  roomingHouseName?: string;
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
