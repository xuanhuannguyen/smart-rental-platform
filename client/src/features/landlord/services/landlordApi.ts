import { apiClient } from '../../../shared/api/apiClient';
import type { ApiResponse } from '../../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  Amenity,
  FileUploadResponse,
  LandlordDashboardData,
  RentalPolicy,
  PropertyImageItemRequest,
  Province,
  RoomingHouseBasicInfoRequest,
  RoomingHouseDetail,
  RoomingHouseOnboarding,
  UpdateRentalPolicyRequest,
  Ward
} from '../types/landlord.types';

interface LegalDocumentRequest {
  documentType: string;
  frontImageObjectKey: string;
  backImageObjectKey: string;
  extraImageObjectKey?: string | null;
  documentNumber: string;
}

export const landlordApi = {
  getDashboard(month: string) {
    return apiClient<ApiResponse<LandlordDashboardData>>(`${ENDPOINTS.LANDLORD_DASHBOARD}?month=${encodeURIComponent(month)}`, {
      auth: true
    });
  },

  getProvinces() {
    return apiClient<Province[]>(ENDPOINTS.ADMINISTRATIVE.PROVINCES);
  },

  getWards(provinceCode: string) {
    return apiClient<Ward[]>(ENDPOINTS.ADMINISTRATIVE.WARDS_BY_PROVINCE(provinceCode));
  },

  getHouseAmenities() {
    return apiClient<ApiResponse<Amenity[]>>(`${ENDPOINTS.AMENITIES.ROOT}?scope=House`);
  },

  getOnboarding() {
    return apiClient<ApiResponse<RoomingHouseOnboarding>>(ENDPOINTS.ROOMING_HOUSES.MY_ONBOARDING, {
      auth: true
    });
  },

  createDraft(payload: RoomingHouseBasicInfoRequest) {
    return apiClient<ApiResponse<RoomingHouseDetail>>(ENDPOINTS.ROOMING_HOUSES.DRAFT, {
      method: 'POST',
      auth: true,
      body: payload
    });
  },

  updateBasicInfo(roomingHouseId: string, payload: RoomingHouseBasicInfoRequest) {
    return apiClient<ApiResponse<RoomingHouseDetail>>(ENDPOINTS.ROOMING_HOUSES.BY_ID(roomingHouseId), {
      method: 'PUT',
      auth: true,
      body: payload
    });
  },

  updateAmenities(roomingHouseId: string, amenityIds: number[]) {
    return apiClient<ApiResponse<RoomingHouseDetail>>(ENDPOINTS.ROOMING_HOUSES.AMENITIES(roomingHouseId), {
      method: 'PUT',
      auth: true,
      body: { amenityIds }
    });
  },

  updateImages(roomingHouseId: string, images: PropertyImageItemRequest[]) {
    return apiClient<ApiResponse<RoomingHouseDetail>>(ENDPOINTS.ROOMING_HOUSES.IMAGES(roomingHouseId), {
      method: 'PUT',
      auth: true,
      body: { images }
    });
  },

  updateLegalDocument(roomingHouseId: string, payload: LegalDocumentRequest) {
    return apiClient<ApiResponse<RoomingHouseDetail>>(ENDPOINTS.ROOMING_HOUSES.LEGAL_DOCUMENT(roomingHouseId), {
      method: 'PUT',
      auth: true,
      body: payload
    });
  },

  getRentalPolicy(roomingHouseId: string) {
    return apiClient<ApiResponse<RentalPolicy | null>>(ENDPOINTS.ROOMING_HOUSES.RENTAL_POLICY(roomingHouseId), {
      auth: true
    });
  },

  updateRentalPolicy(roomingHouseId: string, payload: UpdateRentalPolicyRequest) {
    return apiClient<ApiResponse<RentalPolicy>>(ENDPOINTS.ROOMING_HOUSES.RENTAL_POLICY(roomingHouseId), {
      method: 'PUT',
      auth: true,
      body: payload
    });
  },

  submit(roomingHouseId: string) {
    return apiClient<ApiResponse<RoomingHouseDetail>>(ENDPOINTS.ROOMING_HOUSES.SUBMIT(roomingHouseId), {
      method: 'POST',
      auth: true
    });
  },

  uploadImage(file: File, scope: 'RoomingHouse' | 'LegalDocument') {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('scope', scope);

    return apiClient<ApiResponse<FileUploadResponse>>(ENDPOINTS.FILES.IMAGES, {
      method: 'POST',
      auth: true,
      body: formData
    });
  }
};
