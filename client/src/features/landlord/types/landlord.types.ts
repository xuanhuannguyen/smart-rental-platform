import {
  Province,
  Ward,
  Amenity,
  PropertyImage,
  LegalDocument,
  RentalPolicy,
  FileUploadResponse,
  RoomingHouseOnboarding,
  RoomingHouseBasicInfoRequest,
  UpdateRentalPolicyRequest
} from '../../../shared/types';

export type {
  Province,
  Ward,
  Amenity,
  PropertyImage,
  LegalDocument,
  RentalPolicy,
  FileUploadResponse,
  RoomingHouseOnboarding,
  RoomingHouseBasicInfoRequest,
  UpdateRentalPolicyRequest
};

export interface PropertyImageItemRequest {
  id?: string;
  mediaAssetId?: string | null;
  caption?: string | null;
  isCover: boolean;
  sortOrder: number;
}

export interface RoomingHouseDetail {
  id: string;
  landlordUserId: string;
  name: string;
  description?: string | null;
  addressLine: string;
  provinceCode: string;
  wardCode: string;
  addressDisplay: string;
  latitude?: number | null;
  longitude?: number | null;
  approvalStatus: string;
  visibilityStatus: string;
  rejectedReason?: string | null;
  reviewedByAdminId?: string | null;
  reviewedAt?: string | null;
  createdAt: string;
  updatedAt: string;
  legalDocument?: LegalDocument | null;
  rentalPolicy?: RentalPolicy | null;
  images: PropertyImage[];
  amenities: Amenity[];
}
