import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../shared/api/endpoints';
import type {
  Amenity,
  GuestRoomingHouseRecommendationRequest,
  LocationSuggestion,
  LocationSearchResult,
  PagedResult,
  PropertyImageRequest,
  RoomingHouseListingItem,
  RoomingHouseRecommendationResponse,
  RoomingHouseAiChatRequest,
  RoomingHouseAiChatResponse,
  RoomingHouseSearchItem,
  RoomingHouseSearchParams,
  RentalPolicy,
  RoomingHouseBasicInfoRequest,
  RoomInHouseDetail,
  RoomingHouseRule,
  RoomingHouseDetail,
  RoomingHouseOnboarding,
  RoomingHouseSummary,
  UpdateRentalPolicyRequest,
  UpdateLegalDocumentRequest,
  UpsertRoomingHouseRuleRequest,
  RoomingHouseReviewListResponse,
  ReviewEligibilityResponse,
  CreateRoomingHouseReviewRequest,
  RoomingHouseReviewResponse,
  UpdateRoomingHouseReviewRequest,
  ReplyRoomingHouseReviewRequest,
  CreateReviewReportRequest,
  RoomingHouseReviewEligibilitySummaryResponse
} from './types';

export async function getMyRoomingHouses(): Promise<RoomingHouseSummary[]> {
  const data = await apiClient<ApiResponse<RoomingHouseSummary[]>>(
    '/api/rooming-houses/my',
    { auth: true }
  );
  return data.data;
}

export async function getMyRoomingHouseOnboarding(): Promise<RoomingHouseOnboarding> {
  const data = await apiClient<ApiResponse<RoomingHouseOnboarding>>(
    '/api/rooming-houses/my/onboarding',
    { auth: true }
  );
  return data.data;
}

export async function getAmenities(scope?: 'House' | 'Room' | 'Both'): Promise<Amenity[]> {
  const query = scope ? `?scope=${scope}` : '';
  const data = await apiClient<ApiResponse<Amenity[]>>(
    `/api/amenities${query}`
  );
  return data.data;
}

export async function searchLocationAddress(text: string): Promise<LocationSearchResult> {
  const params = new URLSearchParams({ text });
  const data = await apiClient<ApiResponse<LocationSearchResult>>(
    `/api/locations/search-address?${params.toString()}`
  );
  return data.data;
}

export async function suggestLocationAddresses(text: string, limit = 5): Promise<LocationSuggestion[]> {
  const params = new URLSearchParams({ text, limit: String(limit) });
  const data = await apiClient<ApiResponse<LocationSuggestion[]>>(
    `/api/locations/suggest-addresses?${params.toString()}`
  );
  return data.data;
}

export async function getRoomingHouseDetail(id: string): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}`,
    { auth: true }
  );
  return data.data;
}

export async function getPublicRoomingHouses(): Promise<RoomingHouseDetail[]> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail[]>>(
    '/api/public/rooming-houses'
  );
  return data.data;
}

export async function getPublicRoomingHouseListing(): Promise<RoomingHouseListingItem[]> {
  const data = await apiClient<ApiResponse<RoomingHouseListingItem[]>>(
    '/api/public/rooming-houses/listing'
  );
  return data.data;
}

export async function searchPublicRoomingHouses(
  params: RoomingHouseSearchParams = {}
): Promise<PagedResult<RoomingHouseSearchItem>> {
  const query = buildSearchQuery(params);
  const data = await apiClient<ApiResponse<PagedResult<RoomingHouseSearchItem>>>(
    `${ENDPOINTS.PUBLIC.ROOMING_HOUSE_SEARCH}${query ? `?${query}` : ''}`
  );
  return data.data;
}

export async function getGuestRoomingHouseRecommendations(
  request: GuestRoomingHouseRecommendationRequest
): Promise<RoomingHouseRecommendationResponse> {
  const data = await apiClient<ApiResponse<RoomingHouseRecommendationResponse>>(
    ENDPOINTS.PUBLIC.GUEST_ROOMING_HOUSE_RECOMMENDATIONS,
    { method: 'POST', body: request }
  );
  return data.data;
}

export async function chatRoomingHouseAssistant(
  request: RoomingHouseAiChatRequest
): Promise<RoomingHouseAiChatResponse> {
  const data = await apiClient<ApiResponse<RoomingHouseAiChatResponse>>(
    ENDPOINTS.PUBLIC.ROOMING_HOUSE_AI_CHAT,
    { method: 'POST', body: request }
  );
  return data.data;
}

export async function getPublicRoomingHouseDetail(id: string): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/public/rooming-houses/${id}`
  );
  return data.data;
}

export async function getPublicAvailableRooms(roomingHouseId: string): Promise<RoomInHouseDetail[]> {
  const data = await apiClient<ApiResponse<RoomInHouseDetail[]>>(
    ENDPOINTS.PUBLIC.ROOMS(roomingHouseId)
  );
  return data.data;
}

export async function createRoomingHouseDraft(
  request: RoomingHouseBasicInfoRequest
): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    '/api/rooming-houses/draft',
    { method: 'POST', auth: true, body: request }
  );
  return data.data;
}

export async function updateRoomingHouseBasicInfo(
  id: string,
  request: RoomingHouseBasicInfoRequest
): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}`,
    { method: 'PUT', auth: true, body: request }
  );
  return data.data;
}

export async function updateRoomingHouseImages(
  id: string,
  images: PropertyImageRequest[]
): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}/images`,
    { method: 'PUT', auth: true, body: { images } }
  );
  return data.data;
}

export async function updateRoomingHouseAmenities(
  id: string,
  amenityIds: number[]
): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}/amenities`,
    { method: 'PUT', auth: true, body: { amenityIds } }
  );
  return data.data;
}

export async function updateRoomingHouseLegalDocument(
  id: string,
  request: UpdateLegalDocumentRequest
): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}/legal-document`,
    { method: 'PUT', auth: true, body: request }
  );
  return data.data;
}

export async function updateRoomingHouseVisibility(
  id: string,
  visibilityStatus: 'Visible' | 'Hidden'
): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}/visibility`,
    { method: 'PUT', auth: true, body: { visibilityStatus } }
  );
  return data.data;
}

export async function submitRoomingHouse(id: string): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}/submit`,
    { method: 'POST', auth: true }
  );
  return data.data;
}

export async function getRoomingHouseRentalPolicy(id: string): Promise<RentalPolicy | null> {
  const data = await apiClient<ApiResponse<RentalPolicy | null>>(
    `/api/rooming-houses/${id}/rental-policy`,
    { auth: true }
  );
  return data.data;
}

export async function updateRoomingHouseRentalPolicy(
  id: string,
  request: UpdateRentalPolicyRequest
): Promise<RentalPolicy> {
  const data = await apiClient<ApiResponse<RentalPolicy>>(
    `/api/rooming-houses/${id}/rental-policy`,
    { method: 'PUT', auth: true, body: request }
  );
  return data.data;
}

export async function getRoomingHouseRule(id: string): Promise<RoomingHouseRule | null> {
  const data = await apiClient<ApiResponse<RoomingHouseRule | null>>(
    `/api/rooming-houses/${id}/rule`,
    { auth: true }
  );
  return data.data;
}

export async function upsertRoomingHouseRule(
  id: string,
  request: UpsertRoomingHouseRuleRequest
): Promise<RoomingHouseRule> {
  const data = await apiClient<ApiResponse<RoomingHouseRule>>(
    `/api/rooming-houses/${id}/rule`,
    { method: 'PUT', auth: true, body: request }
  );
  return data.data;
}

export async function previewRoomingHouseRule(
  id: string,
  request: UpsertRoomingHouseRuleRequest
): Promise<Blob> {
  const blob = await apiClient<Blob>(
    `/api/rooming-houses/${id}/rule/preview`,
    {
      method: 'POST',
      auth: true,
      body: request,
      responseType: 'blob',
    }
  );
  return blob;
}

function buildSearchQuery(params: RoomingHouseSearchParams) {
  const query = new URLSearchParams();

  const mappedParams: Record<string, unknown> = {};
  Object.entries(params).forEach(([key, value]) => {
    if (key === 'minAreaM2') {
      mappedParams['minArea'] = value;
    } else if (key === 'maxAreaM2') {
      mappedParams['maxArea'] = value;
    } else if (key === 'sortBy') {
      mappedParams['sort'] = value;
    } else if (key === 'minPrice' || key === 'maxPrice') {
      mappedParams[key] = normalizeRentalPrice(value);
    } else {
      mappedParams[key] = value;
    }
  });

  Object.entries(mappedParams).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    if (Array.isArray(value)) {
      value.forEach((item) => {
        if (item !== undefined && item !== null) {
          query.append(key, String(item));
        }
      });
      return;
    }

    query.set(key, String(value));
  });

  return query.toString();
}

function normalizeRentalPrice(value: unknown) {
  if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0) {
    return value;
  }

  if (value < 100) {
    return value * 1_000_000;
  }

  if (value < 10_000) {
    return value * 1_000;
  }

  return value;
}

// Reviews APIs
export async function getRoomingHouseReviews(
  roomingHouseId: string,
  page = 1,
  pageSize = 10
): Promise<RoomingHouseReviewListResponse> {
  const data = await apiClient<ApiResponse<RoomingHouseReviewListResponse>>(
    `/api/rooming-houses/${roomingHouseId}/reviews?page=${page}&pageSize=${pageSize}`
  );
  return data.data;
}

export async function checkReviewEligibility(contractId: string): Promise<ReviewEligibilityResponse> {
  const data = await apiClient<ApiResponse<ReviewEligibilityResponse>>(
    `/api/rooming-houses/contracts/${contractId}/review-eligibility`,
    { auth: true }
  );
  return data.data;
}

export async function checkRoomingHouseReviewEligibility(roomingHouseId: string): Promise<RoomingHouseReviewEligibilitySummaryResponse> {
  const data = await apiClient<ApiResponse<RoomingHouseReviewEligibilitySummaryResponse>>(
    `/api/rooming-houses/${roomingHouseId}/review-eligibility-summary`,
    { auth: true }
  );
  return data.data;
}

export async function createRoomingHouseReview(
  contractId: string,
  request: CreateRoomingHouseReviewRequest
): Promise<RoomingHouseReviewResponse> {
  const formData = new FormData();
  formData.append('rating', request.rating.toString());
  if (request.comment) {
    formData.append('comment', request.comment);
  }
  if (request.images) {
    request.images.forEach(img => formData.append('images', img));
  }

  const data = await apiClient<ApiResponse<RoomingHouseReviewResponse>>(
    `/api/rooming-houses/contracts/${contractId}/reviews`,
    {
      method: 'POST',
      auth: true,
      body: formData,
    }
  );
  return data.data;
}

export async function updateRoomingHouseReview(
  reviewId: string,
  request: UpdateRoomingHouseReviewRequest
): Promise<RoomingHouseReviewResponse> {
  const formData = new FormData();
  formData.append('rating', request.rating.toString());
  if (request.comment) {
    formData.append('comment', request.comment);
  }
  if (request.retainedImageIds) {
    request.retainedImageIds.forEach(id => formData.append('retainedImageIds', id));
  }
  if (request.newImages) {
    request.newImages.forEach(img => formData.append('newImages', img));
  }

  const data = await apiClient<ApiResponse<RoomingHouseReviewResponse>>(
    `/api/rooming-houses/reviews/${reviewId}`,
    {
      method: 'PUT',
      auth: true,
      body: formData,
    }
  );
  return data.data;
}

export async function deleteRoomingHouseReview(reviewId: string): Promise<void> {
  await apiClient(
    `/api/rooming-houses/reviews/${reviewId}`,
    { method: 'DELETE', auth: true }
  );
}

export async function replyRoomingHouseReview(
  reviewId: string,
  request: ReplyRoomingHouseReviewRequest
): Promise<void> {
  await apiClient(
    `/api/rooming-houses/reviews/${reviewId}/reply`,
    { method: 'POST', auth: true, body: request }
  );
}

export async function deleteRoomingHouseReviewReply(reviewId: string): Promise<void> {
  await apiClient(
    `/api/rooming-houses/reviews/${reviewId}/reply`,
    { method: 'DELETE', auth: true }
  );
}

export async function reportRoomingHouseReview(
  reviewId: string,
  request: CreateReviewReportRequest
): Promise<void> {
  await apiClient(
    `/api/rooming-houses/reviews/${reviewId}/report`,
    { method: 'POST', auth: true, body: request }
  );
}

// Favorites APIs
export async function getFavoriteRoomingHouseIds(): Promise<string[]> {
  const data = await apiClient<ApiResponse<string[]>>(
    '/api/favorite-rooming-houses/ids',
    { auth: true }
  );
  return data.data;
}

export async function toggleFavoriteRoomingHouse(roomingHouseId: string): Promise<boolean> {
  const data = await apiClient<ApiResponse<boolean>>(
    `/api/favorite-rooming-houses/${roomingHouseId}`,
    { method: 'POST', auth: true }
  );
  return data.data;
}

export async function getFavoriteRoomingHouses(pageNumber = 1, pageSize = 20): Promise<PagedResult<RoomingHouseListingItem>> {
  const data = await apiClient<ApiResponse<PagedResult<RoomingHouseListingItem>>>(
    `/api/favorite-rooming-houses?pageNumber=${pageNumber}&pageSize=${pageSize}`,
    { auth: true }
  );
  return data.data;
}
