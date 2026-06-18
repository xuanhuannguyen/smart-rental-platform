import { useEffect } from 'react';
import { formatStatus, getStatusToneClass } from '../../../shared/utils/status';
import { formatMoneyString, parseMoneyString } from '../../../shared/utils/format';
import PropertyImageEditor from '../../rooming-houses/components/PropertyImageEditor';
import type { Amenity, PropertyImageRequest, RoomingHouseDetail } from '../../rooming-houses/types';
import type { Room, CreateRoomRequest, RoomPriceTierRequest } from '../../rooms/types';

export interface RoomDetailEditorProps {
  roomEditorMode: 'list' | 'edit' | 'create';
  selectedRoom: Room | null;
  setRoomEditorMode: (mode: 'list' | 'edit' | 'create') => void;
  handlePublishRoom: () => void;
  roomActiveTab: 'basic' | 'images' | 'amenities' | 'price';
  setRoomActiveTab: (tab: 'basic' | 'images' | 'amenities' | 'price') => void;
  roomForm: CreateRoomRequest;
  setRoomForm: (form: CreateRoomRequest) => void;
  handleSaveRoomBasic: () => void;
  roomImages: PropertyImageRequest[];
  setRoomImages: (images: PropertyImageRequest[]) => void;
  handleSaveRoomImages: () => void;
  roomAmenities: Amenity[];
  roomAmenityIds: number[];
  setRoomAmenityIds: (ids: number[]) => void;
  handleSaveRoomAmenities: () => void;
  priceTiers: RoomPriceTierRequest[];
  setPriceTiers: (tiers: RoomPriceTierRequest[]) => void;
  handleSaveRoomPrice: () => void;
  house: RoomingHouseDetail | null;
}

export default function RoomDetailEditor({
  roomEditorMode,
  selectedRoom,
  setRoomEditorMode,
  handlePublishRoom,
  roomActiveTab,
  setRoomActiveTab,
  roomForm,
  setRoomForm,
  handleSaveRoomBasic,
  roomImages,
  setRoomImages,
  handleSaveRoomImages,
  roomAmenities,
  roomAmenityIds,
  setRoomAmenityIds,
  handleSaveRoomAmenities,
  priceTiers,
  setPriceTiers,
  handleSaveRoomPrice,
  house
}: RoomDetailEditorProps) {
  return (
    <div className="editor-panel">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <button className="back-link" style={{ margin: 0 }} onClick={() => setRoomEditorMode('list')}>
          ← Quay lại danh sách phòng
        </button>
        {roomEditorMode === 'edit' && selectedRoom && (
          <div style={{ display: 'flex', gap: '8px' }}>
            {selectedRoom.status === 'Hidden' && (
              <button className="primary-action" onClick={handlePublishRoom}>
                Hiển thị phòng (Hoạt động)
              </button>
            )}
            <span className={`status-pill ${getStatusToneClass(selectedRoom.status)}`} style={{ padding: '8px 16px', fontSize: '13px' }}>
              Trạng thái: {formatStatus(selectedRoom.status)}
            </span>
          </div>
        )}
      </div>

      <h3 style={{ marginBottom: '16px' }}>
        {roomEditorMode === 'create' ? 'Tạo phòng mới' : `Chỉnh sửa Phòng ${selectedRoom?.roomNumber}`}
      </h3>

      <div className="tabs" style={{ marginBottom: '16px' }}>
        <button className={roomActiveTab === 'basic' ? 'active' : ''} onClick={() => setRoomActiveTab('basic')}>
          Thông tin cơ bản
        </button>
        <button
          className={roomActiveTab === 'images' ? 'active' : ''}
          onClick={() => setRoomActiveTab('images')}
          disabled={roomEditorMode === 'create' && !selectedRoom}
        >
          Ảnh phòng
        </button>
        <button
          className={roomActiveTab === 'amenities' ? 'active' : ''}
          onClick={() => setRoomActiveTab('amenities')}
          disabled={roomEditorMode === 'create' && !selectedRoom}
        >
          Tiện ích phòng
        </button>
        <button
          className={roomActiveTab === 'price' ? 'active' : ''}
          onClick={() => setRoomActiveTab('price')}
          disabled={roomEditorMode === 'create' && !selectedRoom}
        >
          Bảng giá
        </button>
      </div>

      {/* ROOM TAB 1: THÔNG TIN CƠ BẢN PHÒNG */}
      {roomActiveTab === 'basic' && (
        <div className="form-grid">
          <label className="field">
            <span>Số phòng / Tên phòng</span>
            <input value={roomForm.roomNumber} onChange={e => setRoomForm({ ...roomForm, roomNumber: e.target.value })} />
          </label>

          <label className="field">
            <span>Tầng</span>
            <input
              type="number"
              value={roomForm.floor}
              onChange={e => setRoomForm({ ...roomForm, floor: Number(e.target.value) || 1 })}
            />
          </label>

          <label className="field">
            <span>Diện tích (m²)</span>
            <input
              type="number"
              value={roomForm.areaM2 ?? ''}
              onChange={e => setRoomForm({ ...roomForm, areaM2: e.target.value === '' ? null : Number(e.target.value) })}
            />
          </label>

          <label className="field">
            <span>Số khách tối đa</span>
            <input
              type="number"
              value={roomForm.maxOccupants}
              onChange={e => setRoomForm({ ...roomForm, maxOccupants: Number(e.target.value) || 1 })}
            />
          </label>

          <label className="field checkbox-field" style={{ gridColumn: '1 / -1', display: 'flex', alignItems: 'center', gap: '8px', marginTop: '8px' }}>
            <input
              type="checkbox"
              checked={roomForm.isTieredPricing}
              onChange={e => setRoomForm({ ...roomForm, isTieredPricing: e.target.checked })}
              style={{ width: '18px', height: '18px', margin: 0, cursor: 'pointer' }}
            />
            <span style={{ fontSize: '14px', fontWeight: 600, color: '#475569' }}>
              Áp dụng giá thuê theo số lượng người ở (bảng giá thay đổi)
            </span>
          </label>

          <label className="field" style={{ gridColumn: '1 / -1' }}>
            <span>Mô tả phòng</span>
            <textarea
              style={{ width: '100%', minHeight: '80px', padding: '10px', border: '1px solid #cbd5e1', borderRadius: '6px', font: 'inherit' }}
              value={roomForm.description ?? ''}
              onChange={e => setRoomForm({ ...roomForm, description: e.target.value })}
            />
          </label>

          <div className="save-row">
            <button className="primary-action" onClick={handleSaveRoomBasic}>Lưu thông tin</button>
          </div>
        </div>
      )}

      {/* ROOM TAB 2: ẢNH PHÒNG */}
      {roomActiveTab === 'images' && selectedRoom && (
        <PropertyImageEditor
          images={roomImages}
          scope="Room"
          onChange={setRoomImages}
          onSave={handleSaveRoomImages}
        />
      )}

      {/* ROOM TAB 3: TIỆN NGHI PHÒNG */}
      {roomActiveTab === 'amenities' && selectedRoom && (
        <AmenityEditor
          amenities={roomAmenities}
          selectedIds={roomAmenityIds}
          onChange={setRoomAmenityIds}
          onSave={handleSaveRoomAmenities}
        />
      )}

      {/* ROOM TAB 4: BẢNG GIÁ PHÒNG */}
      {roomActiveTab === 'price' && selectedRoom && (
        <PriceTierEditor
          priceTiers={priceTiers}
          isTieredPricing={selectedRoom.isTieredPricing}
          maxOccupants={selectedRoom.maxOccupants}
          onChange={setPriceTiers}
          onSave={handleSaveRoomPrice}
          depositMonths={house?.rentalPolicy?.depositMonths}
        />
      )}
    </div>
  );
}

export function AmenityEditor({
  amenities,
  selectedIds,
  onChange,
  onSave,
}: {
  amenities: Amenity[];
  selectedIds: number[];
  onChange: (selectedIds: number[]) => void;
  onSave: () => void;
}) {
  return (
    <div className="stack-panel">
      <div className="amenity-grid">
        {amenities.map((amenity) => (
          <label className="checkbox-field" key={amenity.id}>
            <input
              type="checkbox"
              checked={selectedIds.includes(amenity.id)}
              onChange={(event) =>
                onChange(
                  event.target.checked
                    ? [...selectedIds, amenity.id]
                    : selectedIds.filter((id) => id !== amenity.id)
                )
              }
            />
            {amenity.name}
          </label>
        ))}
      </div>
      <div className="save-row">
        <button className="primary-action" onClick={onSave}>
          Lưu tiện ích
        </button>
      </div>
    </div>
  );
}

function PriceTierEditor({
  priceTiers,
  isTieredPricing,
  maxOccupants,
  onChange,
  onSave,
  depositMonths = 0,
}: {
  priceTiers: RoomPriceTierRequest[];
  isTieredPricing: boolean;
  maxOccupants: number;
  onChange: (tiers: RoomPriceTierRequest[]) => void;
  onSave: () => void;
  depositMonths?: number;
}) {
  const structureKey = priceTiers.map(t => t.occupantCount).join(',');

  useEffect(() => {
    const targetCount = isTieredPricing ? (maxOccupants || 1) : 1;
    const expectedStructure = Array.from({ length: targetCount }, (_, i) => i + 1).join(',');

    if (structureKey !== expectedStructure) {
      const finalTiers: RoomPriceTierRequest[] = [];
      const firstTier = priceTiers.find(t => t.occupantCount === 1) || priceTiers[0];
      const basePrice = firstTier?.monthlyRent || 0;

      for (let i = 1; i <= targetCount; i++) {
        const existing = priceTiers.find(t => t.occupantCount === i);
        finalTiers.push({
          occupantCount: i,
          monthlyRent: existing ? existing.monthlyRent : basePrice,
          isActive: true
        });
      }
      onChange(finalTiers);
    }
  }, [isTieredPricing, maxOccupants, structureKey, onChange]);

  function updateTier(index: number, tier: RoomPriceTierRequest) {
    onChange(priceTiers.map((t, i) => (i === index ? tier : t)));
  }

  return (
    <div className="stack-panel">
      {priceTiers.map((tier, index) => (
        <div className="inline-editor" key={index} style={{ gridTemplateColumns: '1fr 1fr', alignItems: 'center' }}>
          <label className="field">
            <span>
              {isTieredPricing
                ? `Giá thuê cho ${tier.occupantCount} người (VND/tháng)`
                : 'Giá thuê cố định (VND/tháng)'}
            </span>
            <input
              type="text"
              value={formatMoneyString(tier.monthlyRent)}
              onChange={(e) =>
                updateTier(index, { ...tier, monthlyRent: parseMoneyString(e.target.value) })
              }
              placeholder="0"
            />
          </label>
          <label className="field">
            <span>Đặt cọc (tháng)</span>
            <input
              type="number"
              min={0}
              value={depositMonths}
              readOnly
              style={{ background: '#f1f5f9' }}
            />
          </label>
        </div>
      ))}
      <div className="save-row" style={{ marginTop: '8px' }}>
        <button className="primary-action" onClick={onSave}>
          Lưu bảng giá
        </button>
      </div>
    </div>
  );
}
