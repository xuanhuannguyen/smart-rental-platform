import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../shared/api/endpoints';
import type {
  CreateMeterReadingRequest,
  CreateServicePriceRequest,
  GenerateInvoiceDraftRequest,
  Invoice,
  MeterReading,
  RoomBillingContext,
  ServicePrice
} from './types';

export const billingApi = {
  getServicePrices(roomingHouseId: string) {
    return apiClient<ApiResponse<ServicePrice[]>>(ENDPOINTS.BILLING.SERVICE_PRICES(roomingHouseId), {
      auth: true
    });
  },

  createServicePrice(roomingHouseId: string, payload: CreateServicePriceRequest) {
    return apiClient<ApiResponse<ServicePrice>>(ENDPOINTS.BILLING.SERVICE_PRICES(roomingHouseId), {
      method: 'POST',
      auth: true,
      body: payload
    });
  },

  getRoomBillingContext(roomId: string) {
    return apiClient<ApiResponse<RoomBillingContext>>(ENDPOINTS.BILLING.ROOM_BILLING_CONTEXT(roomId), {
      auth: true
    });
  },

  createMeterReading(payload: CreateMeterReadingRequest) {
    return apiClient<ApiResponse<MeterReading>>(ENDPOINTS.BILLING.METER_READINGS, {
      method: 'POST',
      auth: true,
      body: payload
    });
  },

  generateDraft(payload: GenerateInvoiceDraftRequest) {
    return apiClient<ApiResponse<Invoice>>(ENDPOINTS.BILLING.GENERATE_DRAFT, {
      method: 'POST',
      auth: true,
      body: payload
    });
  },

  getLandlordInvoices(filters?: { status?: string; search?: string }) {
    const params = new URLSearchParams();
    if (filters?.status) {
      params.set('status', filters.status);
    }
    if (filters?.search) {
      params.set('search', filters.search);
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

  payInvoice(invoiceId: string) {
    return apiClient<ApiResponse<Invoice>>(ENDPOINTS.BILLING.PAY_INVOICE(invoiceId), {
      method: 'POST',
      auth: true
    });
  }
};
