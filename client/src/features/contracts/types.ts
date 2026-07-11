import type { MeterReadingInput } from '../billing/types';

export interface ContractBriefResponse {
  id: string;
  status: string;
  startDate: string;
  endDate: string;
  signatureDeadlineAt?: string | null;
  activatedAt?: string | null;
  statusReason?: string | null;
}

export interface ContractOccupantDocumentRequest {
  documentType: string;
  documentNumber?: string | null;
  frontImageObjectKey: string;
  backImageObjectKey?: string | null;
  extraImageObjectKey?: string | null;
}

export interface ContractOccupantRequest {
  clientReferenceId?: string | null;
  guardianClientReferenceId?: string | null;
  email?: string | null;
  userId?: string | null;
  fullName?: string | null;
  phoneNumber?: string | null;
  dateOfBirth?: string | null;
  relationshipToMainTenant?: string | null;
  moveInDate: string;
  moveOutDate?: string | null;
  document?: ContractOccupantDocumentRequest | null;
}

export interface SubmitContractOccupantsRequest {
  occupants: ContractOccupantRequest[];
}

export interface UpdateContractTermsRequest {
  startDate: string;
  endDate: string;
  paymentDay: number;
}

export interface SignContractRequest {
  otp: string;
  signatureText?: string | null;
}

export interface RequestContractSignatureOtpResponse {
  contractId: string;
  signerRole: string;
  expiresAt: string;
  maskedEmail: string;
}

export type ContractFileVariant = 'Raw' | 'Masked';

export interface ContractFileResponse {
  id: string;
  rentalContractId: string;
  rentalContractAppendixId?: string | null;
  mediaAssetId?: string | null;
  storageObjectKey: string;
  fileVariant: ContractFileVariant;
  fileUrl?: string | null;
  viewUrl?: string | null;
  createdAt: string;
}

export interface ContractFileViewUrlResponse {
  url: string;
  deliveryMode: 'signed-url' | 'backend-route' | string;
}

export interface RequestContractRevisionRequest {
  revisionType?: 'Occupants' | 'ContractTerms';
  reason: string;
}

export interface RejectContractRequest {
  reason: string;
}

export interface TerminateContractRequest {
  terminationType: string;
  terminationDate?: string | null;
  damageFee: number;
  reason: string;
  createFinalInvoice?: boolean;
  finalInvoiceDiscountAmount?: number;
  finalInvoiceNote?: string | null;
  finalInvoiceMeterReadings?: MeterReadingInput[];
}

export type ContractTerminationType = 'NormalExpiration' | 'MutualAgreement' | 'TenantUnilateral' | 'LandlordUnilateral';

export interface ContractOccupantDocumentResponse {
  id: string;
  contractOccupantId: string;
  documentType: string;
  documentNumberMasked?: string | null;
  frontImageObjectKey: string;
  backImageObjectKey?: string | null;
  extraImageObjectKey?: string | null;
  uploadedAt: string;
}

export interface ContractOccupantResponse {
  id: string;
  userId?: string | null;
  email?: string | null;
  guardianOccupantId?: string | null;
  fullName: string;
  phoneNumber?: string | null;
  dateOfBirth: string;
  relationshipToMainTenant?: string | null;
  moveInDate: string;
  moveOutDate?: string | null;
  status: string;
  document?: ContractOccupantDocumentResponse | null;
}

export interface ContractSignatureResponse {
  id: string;
  signerUserId: string;
  signerRole: ContractSignerRole;
  signatureMethod: string;
  signedAt: string;
}

export type ContractSignerRole = 'Landlord' | 'Tenant';

export interface ContractDetailResponse {
  id: string;
  rentalRequestId: string;
  roomDepositId: string;
  roomId: string;
  roomNumber: string;
  roomingHouseId: string;
  roomingHouseName: string;
  mainTenantUserId: string;
  mainTenantName: string;
  contractNumber: string;
  startDate: string;
  endDate: string;
  monthlyRent: number;
  depositAmount: number;
  paymentDay: number;
  status: string;
  roomSnapshot?: string | null;
  signatureDeadlineAt?: string | null;
  activatedAt?: string | null;
  terminationDate?: string | null;
  terminationType?: ContractTerminationType | null;
  isAwaitingFinalInvoice: boolean;
  statusReason?: string | null;
  occupants: ContractOccupantResponse[];
  signatures: ContractSignatureResponse[];
  createdAt: string;
  updatedAt: string;
}

export interface ContractHistoryItemResponse {
  id: string;
  rentalRequestId: string;
  roomId: string;
  roomNumber: string;
  roomingHouseId: string;
  roomingHouseName: string;
  mainTenantUserId: string;
  mainTenantName: string;
  contractNumber: string;
  startDate: string;
  endDate: string;
  monthlyRent: number;
  depositAmount: number;
  paymentDay: number;
  maxOccupants: number;
  status: string;
  statusReason?: string | null;
  signatureDeadlineAt?: string | null;
  activatedAt?: string | null;
  terminationDate?: string | null;
  terminationType?: ContractTerminationType | null;
  isAwaitingFinalInvoice: boolean;
  isMainTenant: boolean;
  wasMainTenant: boolean;
  isFormerMainTenant: boolean;
  isCoTenant: boolean;
  isFormerCoTenant: boolean;
  currentUserRelation: string;
  currentUserOccupantId?: string | null;
  currentUserOccupantStatus?: string | null;
  currentUserMoveInDate?: string | null;
  currentUserMoveOutDate?: string | null;
  snapshotAtAppendixId?: string | null;
  snapshotAtDate?: string | null;
  occupants: ContractOccupantResponse[];
  canViewRawContract: boolean;
  canViewMaskedContract: boolean;
  canCreateAppendix: boolean;
  canTerminateContract: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ContractPreviewResponse {
  contractId: string;
  contractNumber: string;
  status: string;
  contentFormat: string;
  renderedContent: string;
  generatedAt: string;
}

export interface ContractAppendixChangeRequest {
  changeType: string;
  targetType: string;
  targetId?: string | null;
  fieldName?: string | null;
  newValue?: string | null;
}

export interface CreateContractAppendixRequest {
  effectiveDate: string;
  changes: ContractAppendixChangeRequest[];
}

export interface ContractAppendixChangeResponse {
  id: string;
  changeType: string;
  targetType: string;
  targetId?: string | null;
  fieldName?: string | null;
  oldValue?: string | null;
  newValue?: string | null;
  sortOrder: number;
  createdAt: string;
}

export type ContractAppendixStatus =
  | 'Draft'
  | 'PendingSignature'
  | 'Active'
  | 'Rejected'
  | 'Cancelled'
  | 'LandlordRevisionRequested'
  | 'TenantRevisionRequested';

export interface ContractAppendixResponse {
  id: string;
  rentalContractId: string;
  appendixNumber: string;
  effectiveDate: string;
  status: ContractAppendixStatus;
  createdByUserId: string;
  activatedAt?: string | null;
  appliedAt?: string | null;
  statusReason?: string | null;
  changes: ContractAppendixChangeResponse[];
  signatures: ContractSignatureResponse[];
  files: ContractFileResponse[];
  createdAt: string;
  updatedAt: string;
}
