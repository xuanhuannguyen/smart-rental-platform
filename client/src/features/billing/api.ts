import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../shared/api/endpoints';
import type {
  BillingServiceType,
  CreateTerminationInvoiceRequest,
  CreateServicePriceRequest,
  GenerateInvoiceWithReadingsRequest,
  Invoice,
  RoomBillingContext,
  RoomInvoicePreview,
  ServicePrice
} from './types';

export const billingApi = {
  getServiceTypes() {
    return apiClient<ApiResponse<BillingServiceType[]>>(ENDPOINTS.BILLING.SERVICE_TYPES, {
      auth: true
    });
  },

  async getServicePrices(roomingHouseId: string) {
    const response = await apiClient<ApiResponse<ServicePrice[]>>(ENDPOINTS.BILLING.SERVICE_PRICES(roomingHouseId), {
      auth: true
    });
    response.data = response.data.map(normalizeServicePrice);
    return response;
  },

  async createServicePrice(roomingHouseId: string, payload: CreateServicePriceRequest) {
    const response = await apiClient<ApiResponse<ServicePrice>>(ENDPOINTS.BILLING.SERVICE_PRICES(roomingHouseId), {
      method: 'POST',
      auth: true,
      body: {
        serviceTypeId: payload.serviceTypeId,
        pricingUnit: payload.pricingUnit,
        unitPrice: payload.unitPrice,
        effectiveFrom: payload.effectiveFrom,
        note: payload.note ?? null
      }
    });
    response.data = normalizeServicePrice(response.data);
    return response;
  },

  getRoomBillingContext(roomId: string) {
    return apiClient<ApiResponse<RoomBillingContext>>(ENDPOINTS.BILLING.ROOM_BILLING_CONTEXT(roomId), {
      auth: true
    });
  },

  getRoomInvoicePreview(roomId: string, params: { billingPeriodStart: string; billingPeriodEnd?: string | null }) {
    const query = new URLSearchParams();
    query.set('billingPeriodStart', params.billingPeriodStart);
    if (params.billingPeriodEnd) {
      query.set('billingPeriodEnd', params.billingPeriodEnd);
    }

    return apiClient<ApiResponse<RoomInvoicePreview>>(`${ENDPOINTS.BILLING.ROOM_INVOICE_PREVIEW(roomId)}?${query.toString()}`, {
      auth: true
    });
  },

  getTerminationInvoicePreview(contractId: string) {
    return apiClient<ApiResponse<RoomInvoicePreview>>(ENDPOINTS.BILLING.TERMINATION_INVOICE_PREVIEW(contractId), {
      auth: true
    });
  },

  createTerminationInvoice(contractId: string, payload: CreateTerminationInvoiceRequest) {
    return apiClient<ApiResponse<Invoice>>(ENDPOINTS.BILLING.CREATE_TERMINATION_INVOICE(contractId), {
      method: 'POST',
      auth: true,
      body: payload
    });
  },

  generateWithReadings(payload: GenerateInvoiceWithReadingsRequest) {
    return apiClient<ApiResponse<Invoice>>(ENDPOINTS.BILLING.GENERATE_WITH_READINGS, {
      method: 'POST',
      auth: true,
      body: payload
    });
  },

  getLandlordInvoices(filters?: { status?: string; search?: string; contractId?: string }) {
    const params = new URLSearchParams();
    if (filters?.status) {
      params.set('status', filters.status);
    }
    if (filters?.search) {
      params.set('search', filters.search);
    }
    if (filters?.contractId) {
      params.set('contractId', filters.contractId);
    }

    const query = params.toString();
    return apiClient<ApiResponse<Invoice[]>>(`${ENDPOINTS.BILLING.LANDLORD_INVOICES}${query ? `?${query}` : ''}`, {
      auth: true
    });
  },

  getLandlordInvoice(invoiceId: string) {
    return apiClient<ApiResponse<Invoice>>(ENDPOINTS.BILLING.LANDLORD_INVOICE(invoiceId), {
      auth: true
    });
  },

  issueInvoice(invoiceId: string) {
    return apiClient<ApiResponse<Invoice>>(ENDPOINTS.BILLING.ISSUE_INVOICE(invoiceId), {
      method: 'POST',
      auth: true
    });
  },

  cancelInvoice(invoiceId: string, reason?: string) {
    return apiClient<ApiResponse<Invoice>>(ENDPOINTS.BILLING.CANCEL_INVOICE(invoiceId), {
      method: 'POST',
      auth: true,
      body: { reason: reason?.trim() || null }
    });
  },

  getMyInvoices() {
    return apiClient<ApiResponse<Invoice[]>>(ENDPOINTS.BILLING.MY_INVOICES, {
      auth: true
    });
  },

  getMyInvoice(invoiceId: string) {
    return apiClient<ApiResponse<Invoice>>(ENDPOINTS.BILLING.MY_INVOICE(invoiceId), {
      auth: true
    });
  },

  getMyContractInvoices(contractId: string, filters?: { status?: string }) {
    const params = new URLSearchParams();
    if (filters?.status) {
      params.set('status', filters.status);
    }

    const query = params.toString();
    return apiClient<ApiResponse<Invoice[]>>(`${ENDPOINTS.BILLING.MY_CONTRACT_INVOICES(contractId)}${query ? `?${query}` : ''}`, {
      auth: true
    });
  },

  payInvoice(invoiceId: string) {
    return apiClient<ApiResponse<Invoice>>(ENDPOINTS.BILLING.PAY_INVOICE(invoiceId), {
      method: 'POST',
      auth: true
    });
  }
};

function normalizeServicePrice(price: ServicePrice): ServicePrice {
  return {
    ...price,
    unitName: price.unitName ?? price.displayUnitName
  };
}
