export interface ServicePrice {
  id: string;
  roomingHouseId: string;
  serviceTypeId: string;
  serviceName: string;
  supportsMeterReading: boolean;
  meterUnitName?: string | null;
  pricingUnit: PricingUnit;
  displayUnitName: string;
  unitName: string;
  unitPrice: number;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
  note?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface BillingServiceType {
  id: string;
  name: string;
  supportsMeterReading: boolean;
  meterUnitName?: string | null;
  isActive: boolean;
}

export type PricingUnit = 'MeterReading' | 'Metered' | 'MeterBased' | 'PerMonth' | 'Fixed' | 'PerPerson' | 'PerPersonPerMonth';

export interface LatestMeterReading {
  serviceTypeId: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  previousReading: number;
  currentReading: number;
  consumption: number;
}

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
  latestReadingByServiceType: Record<string, LatestMeterReading>;
}

export interface InvoiceLinePreview {
  description: string;
  quantity: number;
  unitPrice: number;
  amount: number;
}

export interface FixedServicePreview {
  serviceTypeId: string;
  serviceName: string;
  pricingUnit: PricingUnit;
  displayUnitName: string;
  unitPrice: number;
  quantity: number;
  occupantCount: number;
  amount: number;
}

export interface MeteredServicePreview {
  serviceTypeId: string;
  serviceName: string;
  meterUnitName: string;
  unitPrice: number;
  latestReading?: LatestMeterReading | null;
  requiresPreviousReading: boolean;
}

export interface RoomInvoicePreview {
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
  billingPeriodStart: string;
  billingPeriodEnd: string;
  billableDays: number;
  daysInMonth: number;
  isFullMonth: boolean;
  rentPreview: InvoiceLinePreview;
  fixedServices: FixedServicePreview[];
  meteredServices: MeteredServicePreview[];
  rentAmount: number;
  fixedServiceAmount: number;
  utilityAmount: number;
  totalAmount: number;
  canGenerate: boolean;
  blockReason?: string | null;
}

export interface CreateServicePriceRequest {
  serviceTypeId?: string;
  pricingUnit?: PricingUnit;
  unitName?: string;
  unitPrice: number;
  effectiveFrom: string;
  note?: string | null;
}

export interface MeterReadingInput {
  serviceTypeId: string;
  previousReading?: number | null;
  currentReading: number;
  proofImageObjectKey?: string | null;
  aiReading?: number | null;
  aiRawText?: string | null;
}

export interface MeterAiResponse {
  reading: number;
  rawText: string;
  proofImageObjectKey: string;
  proofImageUrl: string;
}

export interface GenerateInvoiceWithReadingsRequest {
  contractId: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  discountAmount: number;
  note?: string | null;
  meterReadings: MeterReadingInput[];
}

export interface BulkInvoiceRoomInput {
  contractId: string;
  discountAmount: number;
  note?: string | null;
  meterReadings: MeterReadingInput[];
}

export interface GenerateBulkInvoicesRequest {
  roomingHouseId: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  rooms: BulkInvoiceRoomInput[];
}

export type BulkInvoiceRoomStatus = 'Created' | 'Skipped' | 'MissingData';

export interface BulkInvoiceRoomResult {
  roomId: string;
  contractId: string;
  roomNumber: string;
  status: BulkInvoiceRoomStatus;
  message: string;
  invoice?: Invoice | null;
}

export interface BulkInvoiceResult {
  totalActiveRooms: number;
  createdCount: number;
  skippedCount: number;
  missingDataCount: number;
  rooms: BulkInvoiceRoomResult[];
}

export interface CreateTerminationInvoiceRequest {
  discountAmount: number;
  note?: string | null;
  meterReadings: MeterReadingInput[];
}

export interface InvoiceItem {
  id: string;
  serviceTypeId?: string | null;
  serviceName?: string | null;
  meterReadingId?: string | null;
  itemType: string;
  description: string;
  quantity: number;
  unitPrice: number;
  amount: number;
}

export interface Invoice {
  id: string;
  contractId: string;
  roomId: string;
  roomNumber: string;
  roomingHouseId: string;
  roomingHouseName: string;
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
  status: string;
  note?: string | null;
  sentAt?: string | null;
  paidAt?: string | null;
  items: InvoiceItem[];
  walletTransferGroupId?: string | null;
}
