export interface ServicePrice {
  id: string;
  roomingHouseId: string;
  serviceTypeId: string;
  serviceCode: BillingServiceCode;
  serviceName: string;
  billingMethod: BillingMethod;
  unitName: string;
  unitPrice: number;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
  note?: string | null;
  createdAt: string;
  updatedAt: string;
}

export type BillingServiceCode = 'Electric' | 'Water' | 'Wifi' | 'Trash';
export type BillingMethod = 'Metered' | 'MeterBased' | 'Fixed' | 'PerMonth' | 'PerPerson';

export interface RoomBillingContext {
  roomId: string;
  roomNumber: string;
  roomingHouseId: string;
  contractId: string;
  contractNumber: string;
  tenantUserId: string;
  tenantName: string;
  tenantEmail: string;
  monthlyRent: number;
  paymentDay: number;
  contractStartDate: string;
  contractEndDate: string;
  contractStatus: string;
}

export interface CreateServicePriceRequest {
  serviceCode: BillingServiceCode;
  billingMethod: BillingMethod;
  unitName: string;
  unitPrice: number;
  effectiveFrom: string;
  note?: string | null;
}

export interface CreateMeterReadingRequest {
  roomId: string;
  contractId: string;
  serviceCode: 'Electric' | 'Water';
  billingPeriodStart: string;
  billingPeriodEnd: string;
  previousReading: number;
  currentReading: number;
  proofImageObjectKey?: string | null;
}

export interface MeterReading {
  id: string;
  roomId: string;
  contractId: string;
  serviceTypeId: string;
  serviceCode: BillingServiceCode;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  previousReading: number;
  currentReading: number;
  consumption: number;
  proofImageObjectKey?: string | null;
  status: string;
  recordedByLandlordUserId: string;
  readingAt: string;
}

export interface GenerateInvoiceDraftRequest {
  contractId: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  discountAmount: number;
  note?: string | null;
}

export interface InvoiceItem {
  id: string;
  serviceTypeId?: string | null;
  meterReadingId?: string | null;
  itemType: string;
  description: string;
  quantity: number;
  unitPrice: number;
  amount: number;
}

export interface InvoicePayment {
  id: string;
  invoiceId: string;
  amount: number;
  walletTransferGroupId: string;
  status: string;
  paidAt: string;
}

export interface Invoice {
  id: string;
  contractId: string;
  roomId: string;
  roomNumber: string;
  tenantUserId: string;
  tenantName: string;
  tenantEmail: string;
  landlordUserId: string;
  invoiceNo: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  issueDate?: string | null;
  dueDate: string;
  rentAmount: number;
  utilityAmount: number;
  serviceAmount: number;
  discountAmount: number;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  status: string;
  note?: string | null;
  sentAt?: string | null;
  paidAt?: string | null;
  items: InvoiceItem[];
  payments: InvoicePayment[];
}
