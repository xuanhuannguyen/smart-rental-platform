import type { Amenity, PropertyImage } from '../rooming-houses/types';

export type CreateRoomRequest = {
  roomNumber: string;
  floor: number;
  areaM2?: number | null;
  maxOccupants: number;
  isTieredPricing: boolean;
  description?: string;
};

export type RoomPriceTier = {
  id: string;
  occupantCount: number;
  monthlyRent: number;
  isActive: boolean;
};

export type RoomPriceTierRequest = {
  occupantCount: number;
  monthlyRent: number;
  isActive: boolean;
};

export type Room = CreateRoomRequest & {
  id: string;
  roomingHouseId: string;
  status: string;
  createdAt: string;
  updatedAt: string;
  priceTiers: RoomPriceTier[];
  images: PropertyImage[];
  amenities: Amenity[];
};
