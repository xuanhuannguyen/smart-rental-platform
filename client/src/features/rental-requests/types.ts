import type { ContractBriefResponse } from '../contracts/types';

export interface CreateRentalRequestRequest {
  desiredStartDate: string;
  expectedEndDate: string;
  expectedOccupantCount: number;
  tenantNote?: string | null;
}

export interface ApproveRentalRequestRequest {
  paymentDeadlineAt?: string | null;
}

export interface RejectRentalRequestRequest {
  rejectedReason: string;
}

export interface RoomDepositResponse {
  id: string;
  rentalRequestId: string;
  roomId: string;
  tenantUserId: string;
  landlordUserId: string;
  depositAmount: number;
  currency: string;
  status: string;
  paymentDeadlineAt?: string | null;
  paidAt?: string | null;
  refundedAt?: string | null;
  forfeitedAt?: string | null;
  refundAmount?: number | null;
  forfeitedAmount?: number | null;
  note?: string | null;
  paymentTransferGroupId?: string | null;
  refundTransferGroupId?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface RentalRequestResponse {
  id: string;
  roomId: string;
  roomNumber: string;
  roomingHouseId: string;
  roomingHouseName: string;
  tenantUserId: string;
  tenantName: string;
  approvedByLandlordId?: string | null;
  desiredStartDate: string;
  expectedEndDate: string;
  expectedOccupantCount: number;
  monthlyRentSnapshot: number;
  depositAmountSnapshot: number;
  tenantNote?: string | null;
  status: string;
  respondedAt?: string | null;
  rejectedReason?: string | null;
  deposit?: RoomDepositResponse | null;
  contract?: ContractBriefResponse | null;
  createdAt: string;
  updatedAt: string;
}
