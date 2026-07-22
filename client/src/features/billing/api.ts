import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../shared/api/endpoints';
import type {
  BillingServiceType,
  CreateTerminationInvoiceRequest,
  CreateServicePriceRequest,
  GenerateInvoiceWithReadingsRequest,
  GenerateBulkInvoicesRequest,
  BulkInvoiceResult,
  Invoice,
  MeterAiResponse,
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

  generateBulk(payload: GenerateBulkInvoicesRequest) {
    return apiClient<ApiResponse<BulkInvoiceResult>>(ENDPOINTS.BILLING.GENERATE_BULK, {
      method: 'POST',
      auth: true,
      body: payload
    });
  },

  async readMeterImage(payload: {
    contractId: string;
    serviceTypeId: string;
    billingPeriodStart: string;
    file: File;
  }) {
    const uploadFile = await prepareMeterImageForUpload(payload.file);
    const form = new FormData();
    form.append('contractId', payload.contractId);
    form.append('serviceTypeId', payload.serviceTypeId);
    form.append('billingPeriodStart', payload.billingPeriodStart);
    form.append('file', uploadFile);
    return apiClient<ApiResponse<MeterAiResponse>>(ENDPOINTS.BILLING.METER_READING_AI, {
      method: 'POST',
      auth: true,
      body: form
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

const AI_UPLOAD_LIMIT_BYTES = 1_400_000;
const AI_MAX_IMAGE_DIMENSION = 2200;

async function prepareMeterImageForUpload(file: File): Promise<File> {
  if (file.size <= AI_UPLOAD_LIMIT_BYTES) {
    return file;
  }

  const bitmap = await createImageBitmap(file);
  try {
    const initialScale = Math.min(1, AI_MAX_IMAGE_DIMENSION / Math.max(bitmap.width, bitmap.height));
    let width = Math.max(1, Math.round(bitmap.width * initialScale));
    let height = Math.max(1, Math.round(bitmap.height * initialScale));
    let quality = 0.88;

    for (let attempt = 0; attempt < 7; attempt += 1) {
      const canvas = document.createElement('canvas');
      canvas.width = width;
      canvas.height = height;
      const context = canvas.getContext('2d');
      if (!context) {
        throw new Error('Trình duyệt không hỗ trợ xử lý ảnh đồng hồ.');
      }

      context.drawImage(bitmap, 0, 0, width, height);
      const blob = await new Promise<Blob>((resolve, reject) => {
        canvas.toBlob(
          (value) => value ? resolve(value) : reject(new Error('Không thể nén ảnh đồng hồ.')),
          'image/jpeg',
          quality
        );
      });

      if (blob.size <= AI_UPLOAD_LIMIT_BYTES || attempt === 6) {
        const baseName = file.name.replace(/\.[^.]+$/, '') || 'meter-reading';
        return new File([blob], `${baseName}-ai.jpg`, {
          type: 'image/jpeg',
          lastModified: file.lastModified
        });
      }

      quality = Math.max(0.58, quality - 0.08);
      width = Math.max(1, Math.round(width * 0.88));
      height = Math.max(1, Math.round(height * 0.88));
    }

    return file;
  } finally {
    bitmap.close();
  }
}

function normalizeServicePrice(price: ServicePrice): ServicePrice {
  return {
    ...price,
    unitName: price.unitName ?? price.displayUnitName
  };
}

const AI_UPLOAD_LIMIT_BYTES = 1_400_000;
const AI_MAX_IMAGE_DIMENSION = 2200;

async function prepareMeterImageForUpload(file: File): Promise<File> {
  if (file.size <= AI_UPLOAD_LIMIT_BYTES) {
    return file;
  }

  const bitmap = await createImageBitmap(file);
  try {
    const initialScale = Math.min(1, AI_MAX_IMAGE_DIMENSION / Math.max(bitmap.width, bitmap.height));
    let width = Math.max(1, Math.round(bitmap.width * initialScale));
    let height = Math.max(1, Math.round(bitmap.height * initialScale));
    let quality = 0.88;

    for (let attempt = 0; attempt < 7; attempt += 1) {
      const canvas = document.createElement('canvas');
      canvas.width = width;
      canvas.height = height;
      const context = canvas.getContext('2d');
      if (!context) {
        throw new Error('Trình duyệt không hỗ trợ xử lý ảnh đồng hồ.');
      }

      context.drawImage(bitmap, 0, 0, width, height);
      const blob = await new Promise<Blob>((resolve, reject) => {
        canvas.toBlob(
          (value) => value ? resolve(value) : reject(new Error('Không thể nén ảnh đồng hồ.')),
          'image/jpeg',
          quality
        );
      });

      if (blob.size <= AI_UPLOAD_LIMIT_BYTES || attempt === 6) {
        const baseName = file.name.replace(/\.[^.]+$/, '') || 'meter-reading';
        return new File([blob], `${baseName}-ai.jpg`, {
          type: 'image/jpeg',
          lastModified: file.lastModified
        });
      }

      quality = Math.max(0.58, quality - 0.08);
      width = Math.max(1, Math.round(width * 0.88));
      height = Math.max(1, Math.round(height * 0.88));
    }

    return file;
  } finally {
    bitmap.close();
  }
}
