// Paged Result mapping
export interface PagedResult<T> {
  items: T[];
  totalItems: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// 1. Province & Ward
export interface AdminProvinceResponse {
  code: string;
  name: string;
  type: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface AdminWardResponse {
  code: string;
  name: string;
  type: string;
  provinceCode: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateProvinceRequest {
  code: string;
  name: string;
  type: string;
}

export interface UpdateProvinceRequest {
  name: string;
  type: string;
}

export interface CreateWardRequest {
  code: string;
  name: string;
  type: string;
  provinceCode: string;
}

export interface UpdateWardRequest {
  name: string;
  type: string;
}

// 2. Amenity
export interface AdminAmenityResponse {
  id: number;
  name: string;
  scope: string; // 'House', 'Room', 'Both'
  iconCode?: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateAmenityRequest {
  name: string;
  scope: string;
  iconCode?: string;
}

export interface UpdateAmenityRequest {
  name: string;
  scope: string;
  iconCode?: string;
}

// 3. Billing Service Type
export interface AdminBillingServiceTypeResponse {
  id: number;
  name: string;
  supportsMeterReading: boolean;
  meterUnitName: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateBillingServiceTypeRequest {
  name: string;
  supportsMeterReading: boolean;
  meterUnitName: string;
}

export interface UpdateBillingServiceTypeRequest {
  name: string;
  supportsMeterReading: boolean;
  meterUnitName: string;
}
