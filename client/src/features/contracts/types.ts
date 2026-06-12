export interface ContractBriefResponse {
  id: string;
  status: string;
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

export interface ContractFileResponse {
  id: string;
  rentalContractId: string;
  rentalContractAppendixId?: string | null;
  storageObjectKey: string;
  fileUrl?: string | null;
  createdAt: string;
}

export interface RequestContractRevisionRequest {
  revisionType?: 'Occupants' | 'ContractTerms';
  reason: string;
}

export interface RejectContractRequest {
  reason: string;
}

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
  signerRole: string;
  signatureMethod: string;
  signedAt: string;
}

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
  statusReason?: string | null;
  occupants: ContractOccupantResponse[];
  signatures: ContractSignatureResponse[];
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

export interface ContractAppendixResponse {
  id: string;
  rentalContractId: string;
  appendixNumber: string;
  effectiveDate: string;
  status: string;
  createdByUserId: string;
  activatedAt?: string | null;
  statusReason?: string | null;
  changes: ContractAppendixChangeResponse[];
  signatures: ContractSignatureResponse[];
  files: ContractFileResponse[];
  createdAt: string;
  updatedAt: string;
}
