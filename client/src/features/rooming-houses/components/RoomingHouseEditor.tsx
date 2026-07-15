import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { formatStatus } from '../../../shared/utils/status';
import { uploadImage } from '../../files/api';
import {
  createRoomingHouseDraft,
  getAmenities,
  getRoomingHouseDetail,
  submitRoomingHouse,
  updateRoomingHouseAmenities,
  updateRoomingHouseBasicInfo,
  updateRoomingHouseImages,
  updateRoomingHouseRentalPolicy,
  updateRoomingHouseLegalDocument,
} from '../api';
import { Toast } from '../../../shared/components/ui/Toast';
import { Tabs } from '../../../shared/components/ui/Tabs';
import type {
  Amenity,
  PropertyImageRequest,
  RoomingHouseBasicInfoRequest,
  RoomingHouseDetail,
  UpdateRentalPolicyRequest,
  UpdateLegalDocumentRequest,
} from '../types';
// Các API hành chính (Tỉnh/Thành, Phường/Xã)
import { getProvinces, getWardsByProvince } from '../../administrative/api';
import type { Province, Ward } from '../../administrative/types';
import PropertyImageEditor from './PropertyImageEditor';
import LeafletLocationPicker from './LeafletLocationPicker';
import { cleanImages, toImageRequests } from '../utils/imageRequests';
import './RoomingHouseEditor.css';

type RoomingHouseTab = 'basic' | 'images' | 'amenities' | 'legal' | 'rental-policy';
type RoomingHouseEditorProps = {
  title: string;
  subtitle: string;
  initialRoomingHouse?: RoomingHouseDetail | null;
  initialTab?: RoomingHouseTab;
  allowSubmit?: boolean;
  submitLabel?: string;
  onChange?: (roomingHouse: RoomingHouseDetail) => void;
  onSubmitSuccess?: (roomingHouse: RoomingHouseDetail) => void;
};

const emptyHouseForm: RoomingHouseBasicInfoRequest = {
  name: '',
  description: '',
  addressLine: '',
  provinceCode: '',
  wardCode: '',
  latitude: null,
  longitude: null,
  googleMapUrl: '',
};

const emptyLegalForm: UpdateLegalDocumentRequest = {
  documentType: '',
  frontMediaAssetId: null,
  backMediaAssetId: null,
  extraMediaAssetId: null,
  documentNumber: '',
};

const emptyRentalPolicyForm: UpdateRentalPolicyRequest = {
  minRentalMonths: 1,
  maxRentalMonths: 12,
  allowShortTermRenewal: false,
  renewalNoticeDays: 30,
  depositMonths: 1,
  defaultPaymentDay: 5,
};

function buildBasicForm(detail: RoomingHouseDetail | null): RoomingHouseBasicInfoRequest {
  if (!detail) return emptyHouseForm;
  return {
    name: detail.name,
    description: detail.description ?? '',
    addressLine: detail.addressLine,
    provinceCode: detail.provinceCode,
    wardCode: detail.wardCode,
    latitude: detail.latitude ?? null,
    longitude: detail.longitude ?? null,
    googleMapUrl: detail.googleMapUrl ?? '',
  };
}

function buildLegalForm(detail: RoomingHouseDetail | null): UpdateLegalDocumentRequest {
  if (!detail) return emptyLegalForm;
  return {
    documentType: detail.legalDocument?.documentType ?? '',
    frontMediaAssetId: detail.legalDocument?.frontMediaAssetId ?? null,
    backMediaAssetId: detail.legalDocument?.backMediaAssetId ?? null,
    extraMediaAssetId: detail.legalDocument?.extraMediaAssetId ?? null,
    documentNumber: detail.legalDocument?.documentNumberMasked ?? '',
  };
}

function buildRentalPolicyForm(detail: RoomingHouseDetail | null): UpdateRentalPolicyRequest {
  if (!detail?.rentalPolicy) return emptyRentalPolicyForm;
  return {
    minRentalMonths: detail.rentalPolicy.minRentalMonths,
    maxRentalMonths: detail.rentalPolicy.maxRentalMonths,
    allowShortTermRenewal: detail.rentalPolicy.allowShortTermRenewal,
    renewalNoticeDays: detail.rentalPolicy.renewalNoticeDays,
    depositMonths: detail.rentalPolicy.depositMonths,
    defaultPaymentDay: detail.rentalPolicy.defaultPaymentDay,
  };
}

const buildingIcon = (
  <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="4" y="2" width="16" height="20" rx="2" ry="2" />
    <line x1="9" y1="22" x2="9" y2="16" />
    <line x1="15" y1="22" x2="15" y2="16" />
    <line x1="9" y1="16" x2="15" y2="16" />
    <path d="M9 6h.01" />
    <path d="M15 6h.01" />
    <path d="M9 10h.01" />
    <path d="M15 10h.01" />
  </svg>
);

const descIcon = (
  <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
    <line x1="21" y1="10" x2="3" y2="10" />
    <line x1="21" y1="6" x2="3" y2="6" />
    <line x1="21" y1="14" x2="3" y2="14" />
    <line x1="21" y1="18" x2="3" y2="18" />
  </svg>
);

const mapPinIcon = (
  <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
    <circle cx="12" cy="10" r="3" />
  </svg>
);

export default function RoomingHouseEditor({
  title,
  subtitle,
  initialRoomingHouse,
  initialTab = 'basic',
  allowSubmit = true,
  submitLabel = 'Gửi duyệt',
  onChange,
  onSubmitSuccess,
}: RoomingHouseEditorProps) {
  const [roomingHouse, setRoomingHouse] = useState<RoomingHouseDetail | null>(
    initialRoomingHouse ?? null
  );
  const [activeTab, setActiveTab] = useState<RoomingHouseTab>(initialTab);

  useEffect(() => {
    setActiveTab(initialTab);
  }, [initialTab]);
  const [amenities, setAmenities] = useState<Amenity[]>([]);
  const [provinces, setProvinces] = useState<Province[]>([]);
  const [wards, setWards] = useState<Ward[]>([]);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  const [saving, setSaving] = useState(false);

  const [basicForm, setBasicForm] = useState<RoomingHouseBasicInfoRequest>(() =>
    buildBasicForm(initialRoomingHouse ?? null)
  );
  const [images, setImages] = useState<PropertyImageRequest[]>(() =>
    initialRoomingHouse ? toImageRequests(initialRoomingHouse.images) : []
  );
  const [amenityIds, setAmenityIds] = useState<number[]>(() =>
    initialRoomingHouse
      ? initialRoomingHouse.amenities.map((amenity) => amenity.id)
      : []
  );
  const [legalForm, setLegalForm] = useState<UpdateLegalDocumentRequest>(() =>
    buildLegalForm(initialRoomingHouse ?? null)
  );
  const [rentalPolicyForm, setRentalPolicyForm] = useState<UpdateRentalPolicyRequest>(() =>
    buildRentalPolicyForm(initialRoomingHouse ?? null)
  );

  const hasDraft = Boolean(roomingHouse?.id);
  const isApproved = roomingHouse?.approvalStatus === 'Approved';
  const canEditLegalDocument =
    !roomingHouse ||
    roomingHouse.approvalStatus === 'Draft' ||
    roomingHouse.approvalStatus === 'Rejected';
  const canSubmitForReview =
    hasDraft &&
    (roomingHouse?.approvalStatus === 'Draft' ||
      roomingHouse?.approvalStatus === 'Rejected');
  const selectedProvinceName =
    provinces.find((province) => province.code === basicForm.provinceCode)?.name ?? '';
  const selectedWardName =
    wards.find((ward) => ward.code === basicForm.wardCode)?.name ?? '';

  useEffect(() => {
    setRoomingHouse(initialRoomingHouse ?? null);
    setBasicForm(buildBasicForm(initialRoomingHouse ?? null));
    setImages(initialRoomingHouse ? toImageRequests(initialRoomingHouse.images) : []);
    setAmenityIds(
      initialRoomingHouse
        ? initialRoomingHouse.amenities.map((amenity) => amenity.id)
        : []
    );
    setLegalForm(buildLegalForm(initialRoomingHouse ?? null));
    setRentalPolicyForm(buildRentalPolicyForm(initialRoomingHouse ?? null));
  }, [initialRoomingHouse]);


  useEffect(() => {
    async function loadInitialOptions() {
      try {
        const [provinceList, amenityList] = await Promise.all([
          getProvinces(),
          getAmenities('House'),
        ]);
        setProvinces(provinceList);
        setAmenities(amenityList);
      } catch (error) {
        setToast({ message: getApiErrorMessage(error, 'Không thể tải dữ liệu biểu mẫu.'), type: 'error' });
      }
    }
    loadInitialOptions();
  }, []);

  useEffect(() => {
    async function loadWards() {
      if (!basicForm.provinceCode) {
        setWards([]);
        return;
      }
      try {
        setWards(await getWardsByProvince(basicForm.provinceCode));
      } catch (error) {
        setToast({ message: getApiErrorMessage(error, 'Không thể tải danh sách phường/xã.'), type: 'error' });
      }
    }
    loadWards();
  }, [basicForm.provinceCode]);

  async function saveBasicInfo() {
    await saveChange(async () => {
      const saved = roomingHouse
        ? await updateRoomingHouseBasicInfo(roomingHouse.id, basicForm)
        : await createRoomingHouseDraft(basicForm);

      if (!roomingHouse) {
        setActiveTab('images');
      }
      return saved;
    }, 'Đã lưu thông tin cơ bản.');
  }

  async function saveImages() {
    if (!roomingHouse) {
      setToast({ message: 'Vui lòng lưu thông tin cơ bản trước.', type: 'info' });
      return;
    }
    await saveChange(
      () => updateRoomingHouseImages(roomingHouse.id, cleanImages(images)),
      'Đã lưu ảnh khu trọ.'
    );
  }

  async function saveAmenities() {
    if (!roomingHouse) {
      setToast({ message: 'Vui lòng lưu thông tin cơ bản trước.', type: 'info' });
      return;
    }
    await saveChange(
      () => updateRoomingHouseAmenities(roomingHouse.id, amenityIds),
      'Đã lưu tiện ích.'
    );
  }

  async function saveLegalDocument() {
    if (!roomingHouse) {
      setToast({ message: 'Vui lòng lưu thông tin cơ bản trước.', type: 'info' });
      return;
    }
    if (!canEditLegalDocument) {
      setToast({ message: 'Giấy tờ pháp lý chỉ được chỉnh sửa khi khu trọ là bản nháp hoặc bị từ chối.', type: 'info' });
      return;
    }
    await saveChange(
      () => updateRoomingHouseLegalDocument(roomingHouse.id, legalForm),
      'Đã lưu giấy tờ pháp lý.'
    );
  }

  async function saveRentalPolicy() {
    if (!roomingHouse) {
      setToast({ message: 'Vui lòng lưu thông tin cơ bản trước.', type: 'info' });
      return;
    }
    await saveChange(
      async () => {
        await updateRoomingHouseRentalPolicy(roomingHouse.id, rentalPolicyForm);
        return await getRoomingHouseDetail(roomingHouse.id);
      },
      'Đã lưu chính sách thuê.'
    );
  }

  async function submitForReview() {
    if (!roomingHouse) {
      setToast({ message: 'Vui lòng lưu thông tin cơ bản trước khi gửi duyệt.', type: 'info' });
      return;
    }

    setSaving(true);
    setToast(null);

    try {
      const submitted = await submitRoomingHouse(roomingHouse.id);
      setRoomingHouse(submitted);
      onSubmitSuccess?.(submitted);
      setToast({ message: 'Đã gửi khu trọ để quản trị viên xét duyệt.', type: 'success' });
    } catch (error) {
      setToast({ message: getApiErrorMessage(error, 'Không thể gửi duyệt khu trọ.'), type: 'error' });
    } finally {
      setSaving(false);
    }
  }

  async function saveChange(
    request: () => Promise<RoomingHouseDetail>,
    successMessage: string
  ) {
    setSaving(true);
    setToast(null);
    try {
      const saved = await request();
      setRoomingHouse(saved);
      hydrateForms(saved);
      onChange?.(saved);
      setToast({ message: successMessage, type: 'success' });
    } catch (error) {
      setToast({ message: getApiErrorMessage(error, 'Không thể lưu thông tin khu trọ.'), type: 'error' });
    } finally {
      setSaving(false);
    }
  }

  function hydrateForms(detail: RoomingHouseDetail) {
    setBasicForm(buildBasicForm(detail));
    setImages(toImageRequests(detail.images));
    setAmenityIds(detail.amenities.map((amenity) => amenity.id));
    setLegalForm(buildLegalForm(detail));
    setRentalPolicyForm(buildRentalPolicyForm(detail));
  }

  function selectProvince(provinceCode: string) {
    setBasicForm({ ...basicForm, provinceCode, wardCode: '' });
  }

  async function uploadLegalImage(fieldName: 'front' | 'back' | 'extra', file: File | null) {
    if (!file || !canEditLegalDocument) return;

    setSaving(true);
    setToast(null);

    try {
      const uploaded = await uploadImage(file, 'LegalDocument');
      setLegalForm((current) => ({
        ...current,
        [`${fieldName}MediaAssetId`]: uploaded.mediaAssetId || null,
      }));
    } catch (error) {
      setToast({
        message: getApiErrorMessage(error, 'Không thể tải ảnh giấy tờ lên.'),
        type: 'error',
      });
    } finally {
      setSaving(false);
    }
  }

  function removeLegalImage(fieldName: 'front' | 'back' | 'extra') {
    if (!canEditLegalDocument) return;
    setLegalForm((current) => ({
      ...current,
      [`${fieldName}MediaAssetId`]: null,
    }));
  }

  return (
    <div className="rooming-house-editor">
      <header className="rooming-house-editor__header">
        <div className="header-brand-icon">
          <svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
            <polyline points="9 22 9 12 15 12 15 22" />
          </svg>
        </div>
        <div className="header-text-group">
          <h1>{title}</h1>
          <p>{subtitle}</p>
        </div>
        {roomingHouse && (
          <div className="header-right-group">
            <p className="rooming-house-editor__status">
              Trạng thái: {formatStatus(roomingHouse.approvalStatus)}
            </p>
            {allowSubmit && canSubmitForReview && (
              <button
                className="rooming-house-editor__primary"
                disabled={saving}
                type="button"
                onClick={submitForReview}
                style={{ minHeight: '36px', borderRadius: '8px', padding: '7px 18px', fontSize: '14px' }}
              >
                <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }}>
                  <line x1="22" y1="2" x2="11" y2="13" />
                  <polygon points="22 2 15 22 11 13 2 9 22 2" />
                </svg>
                Gửi duyệt khu trọ
              </button>
            )}
          </div>
        )}
      </header>

      {saving && <p className="rooming-house-editor__message">Đang xử lý...</p>}

      <>
        <Tabs
          className="attached-bottom"
          variant="segmented-secondary"
          activeId={activeTab}
          onChange={(tab) => setActiveTab(tab as RoomingHouseTab)}
          items={[
            {
              id: 'basic',
              label: 'Thông tin cơ bản',
              icon: <span className="tab-number-circle">1</span>,
            },
            {
              id: 'images',
              label: 'Ảnh',
              disabled: !hasDraft,
              title: !hasDraft ? 'Lưu thông tin cơ bản trước khi cập nhật ảnh' : undefined,
              icon: (
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                  <circle cx="8.5" cy="8.5" r="1.5" />
                  <polyline points="21 15 16 10 5 21" />
                </svg>
              ),
            },
            {
              id: 'amenities',
              label: 'Tiện ích',
              disabled: !hasDraft,
              title: !hasDraft ? 'Lưu thông tin cơ bản trước khi cập nhật tiện ích' : undefined,
              icon: (
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="3" width="7" height="7" />
                  <rect x="14" y="3" width="7" height="7" />
                  <rect x="14" y="14" width="7" height="7" />
                  <rect x="3" y="14" width="7" height="7" />
                </svg>
              ),
            },
            {
              id: 'legal',
              label: 'Giấy tờ pháp lý',
              disabled: !hasDraft,
              title: !hasDraft ? 'Lưu thông tin cơ bản trước khi cập nhật giấy tờ pháp lý' : undefined,
              icon: (
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                  <polyline points="14 2 14 8 20 8" />
                  <line x1="16" y1="13" x2="8" y2="13" />
                  <line x1="16" y1="17" x2="8" y2="17" />
                </svg>
              ),
            },
            ...(isApproved
              ? [{
                id: 'rental-policy',
                label: 'Chính sách thuê',
                icon: (
                  <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                ),
              }]
              : []),
          ]}
        />

      <section className="rooming-house-editor__panel tab-attached-panel tab-attached-panel--compact">
        {activeTab === 'basic' && (
          <div className="rooming-house-editor__form-grid">
            <TextField
              label="Tên khu trọ"
              required
              placeholder="Nhập tên khu trọ"
              icon={buildingIcon}
              value={basicForm.name}
              onChange={(value) => setBasicForm({ ...basicForm, name: value })}
            />
            <TextField
              label="Mô tả"
              placeholder="Nhập mô tả khu trọ"
              icon={descIcon}
              value={basicForm.description ?? ''}
              onChange={(value) => setBasicForm({ ...basicForm, description: value })}
            />
            <SelectField
              label="Tỉnh/Thành phố"
              required
              icon={buildingIcon}
              value={basicForm.provinceCode}
              onChange={selectProvince}
            >
              <option value="">Chọn tỉnh/thành phố</option>
              {provinces.map((province) => (
                <option key={province.code} value={province.code}>
                  {province.name}
                </option>
              ))}
            </SelectField>
            <SelectField
              label="Phường/Xã"
              icon={mapPinIcon}
              value={basicForm.wardCode}
              disabled={!basicForm.provinceCode}
              onChange={(value) => setBasicForm({ ...basicForm, wardCode: value })}
            >
              <option value="">Chọn phường/xã</option>
              {wards.map((ward) => (
                <option key={ward.code} value={ward.code}>
                  {ward.name}
                </option>
              ))}
            </SelectField>
            <TextField
              label="Địa chỉ chi tiết"
              required
              placeholder="Nhập địa chỉ chi tiết"
              icon={mapPinIcon}
              className="rooming-house-editor__field--full-width"
              value={basicForm.addressLine}
              onChange={(value) => setBasicForm({ ...basicForm, addressLine: value })}
            />
            <LeafletLocationPicker
              addressLine={basicForm.addressLine}
              provinceName={selectedProvinceName}
              wardName={selectedWardName}
              latitude={basicForm.latitude}
              longitude={basicForm.longitude}
              googleMapUrl={basicForm.googleMapUrl}
              onAddressChange={(addressLine) =>
                setBasicForm((current) => ({ ...current, addressLine }))
              }
              onLocationChange={(latitude, longitude) =>
                setBasicForm((current) => ({ ...current, latitude, longitude }))
              }
              onGoogleMapUrlChange={(googleMapUrl) =>
                setBasicForm((current) => ({ ...current, googleMapUrl }))
              }
            />
            <ActionRow>
              <button className="rooming-house-editor__primary" disabled={saving} onClick={saveBasicInfo}>
                <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '8px' }}>
                  <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                  <polyline points="17 21 17 13 7 13 7 21" />
                  <polyline points="7 3 7 8 15 8" />
                </svg>
                {hasDraft ? 'Lưu' : 'Lưu và tiếp tục'}
              </button>
            </ActionRow>
          </div>
        )}

        {activeTab === 'images' && (
          <PropertyImageEditor
            images={images}
            scope="RoomingHouse"
            onChange={setImages}
            onSave={saveImages}
            onSubmit={canSubmitForReview ? submitForReview : undefined}
            saving={saving}
          />
        )}

        {activeTab === 'amenities' && (
          <AmenityEditor
            amenities={amenities}
            selectedIds={amenityIds}
            onChange={setAmenityIds}
            onSave={saveAmenities}
          />
        )}

        {activeTab === 'legal' && (
          <div className="rooming-house-editor__stack">
            {/* Section header */}
            <div className="legal-section-header">
              <div className="legal-section-header__icon">
                <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                  <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                </svg>
              </div>
              <div>
                <h3 className="legal-section-title">Giấy tờ pháp lý</h3>
                <p className="legal-section-subtitle">Cung cấp giấy tờ xác nhận quyền sở hữu hoặc quyền sử dụng bất động sản.</p>
              </div>
              {!canEditLegalDocument && (
                <span className="legal-readonly-badge">
                  <svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '5px' }}>
                    <path d="M17 1l4 4-9.5 9.5-5 1 1-5L17 1z" />
                    <path d="M3 21h18" />
                  </svg>
                  Chế độ xem
                </span>
              )}
            </div>

            {/* Document info card */}
            <div className="legal-info-card">
              <div className="legal-info-card__grid">
                <SelectField
                  disabled={!canEditLegalDocument}
                  label="Loại giấy tờ"
                  value={legalForm.documentType}
                  onChange={(value) => setLegalForm({ ...legalForm, documentType: value })}
                >
                  <option value="">Chọn loại giấy tờ</option>
                  <option value="LAND_USE_CERTIFICATE">Giấy chứng nhận quyền sử dụng đất</option>
                </SelectField>
                <TextField
                  readOnly={!canEditLegalDocument}
                  label="Số giấy tờ"
                  value={legalForm.documentNumber}
                  onChange={(value) => setLegalForm({ ...legalForm, documentNumber: value })}
                />
              </div>
            </div>

            {/* Image uploads */}
            <div className="legal-images-section">
              <h4 className="legal-images-section__title">
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }}>
                  <rect x="3" y="3" width="18" height="18" rx="2" />
                  <circle cx="8.5" cy="8.5" r="1.5" />
                  <polyline points="21 15 16 10 5 21" />
                </svg>
                Ảnh giấy tờ
              </h4>
              <div className="legal-images-grid">
                <LegalImageField
                  label="Ảnh mặt trước"
                  imageUrl={roomingHouse?.legalDocument?.frontImageUrl ?? ''}
                  readOnly={!canEditLegalDocument}
                  onUpload={(file) => void uploadLegalImage('front', file)}
                  onRemove={() => removeLegalImage('front')}
                />
                <LegalImageField
                  label="Ảnh mặt sau"
                  imageUrl={roomingHouse?.legalDocument?.backImageUrl ?? ''}
                  readOnly={!canEditLegalDocument}
                  onUpload={(file) => void uploadLegalImage('back', file)}
                  onRemove={() => removeLegalImage('back')}
                />
                <LegalImageField
                  label="Ảnh bổ sung"
                  imageUrl={roomingHouse?.legalDocument?.extraImageUrl ?? ''}
                  readOnly={!canEditLegalDocument}
                  optional
                  onUpload={(file) => void uploadLegalImage('extra', file)}
                  onRemove={() => removeLegalImage('extra')}
                />
              </div>
            </div>

            {canEditLegalDocument && (
              <ActionRow>
                <button
                  className="rooming-house-editor__primary"
                  disabled={saving}
                  type="button"
                  onClick={saveLegalDocument}
                  style={{ minHeight: '38px', borderRadius: '8px', padding: '8px 24px' }}
                >
                  <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '8px' }}>
                    <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                    <polyline points="17 21 17 13 7 13 7 21" />
                    <polyline points="7 3 7 8 15 8" />
                  </svg>
                  Lưu giấy tờ
                </button>
              </ActionRow>
            )}
          </div>
        )}

        {activeTab === 'rental-policy' && (
          <div className="rooming-house-editor__form-grid">
            <NumberField
              label="Số tháng thuê tối thiểu"
              value={rentalPolicyForm.minRentalMonths}
              onChange={(v) =>
                setRentalPolicyForm({ ...rentalPolicyForm, minRentalMonths: v ?? 1 })
              }
            />
            <NumberField
              label="Số tháng thuê tối đa"
              value={rentalPolicyForm.maxRentalMonths}
              onChange={(v) =>
                setRentalPolicyForm({ ...rentalPolicyForm, maxRentalMonths: v ?? 1 })
              }
            />
            <label className="rooming-house-editor__field rooming-house-editor__field--checkbox">
              <span>Cho phép gia hạn ngắn hạn</span>
              <input
                type="checkbox"
                checked={rentalPolicyForm.allowShortTermRenewal}
                onChange={(e) =>
                  setRentalPolicyForm({ ...rentalPolicyForm, allowShortTermRenewal: e.target.checked })
                }
              />
            </label>
            <NumberField
              label="Số ngày báo trước khi gia hạn"
              value={rentalPolicyForm.renewalNoticeDays}
              onChange={(v) =>
                setRentalPolicyForm({ ...rentalPolicyForm, renewalNoticeDays: v ?? 0 })
              }
            />
            <NumberField
              label="Số tháng tiền cọc"
              value={rentalPolicyForm.depositMonths}
              onChange={(v) =>
                setRentalPolicyForm({ ...rentalPolicyForm, depositMonths: v ?? 0 })
              }
            />
            <NumberField
              label="Ngày thanh toán mặc định"
              value={rentalPolicyForm.defaultPaymentDay}
              onChange={(v) =>
                setRentalPolicyForm({ ...rentalPolicyForm, defaultPaymentDay: v ?? 5 })
              }
            />
            <ActionRow>
              <button className="rooming-house-editor__primary" disabled={saving} onClick={saveRentalPolicy}>
                Lưu chính sách thuê
              </button>
            </ActionRow>
          </div>
        )}
      </section>
      </>
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}

// ─── Sub-components ──────────────────────────────────────────────────────────

function TextField({
  label,
  value,
  readOnly,
  placeholder,
  required,
  icon,
  className,
  onChange,
}: {
  label: string;
  value: string;
  readOnly?: boolean;
  placeholder?: string;
  required?: boolean;
  icon?: React.ReactNode;
  className?: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className={`rooming-house-editor__field ${className ?? ''}`}>
      <span>
        {label}
        {required && <span className="field-required"> *</span>}
      </span>
      <div className="field-input-wrapper">
        {icon && <span className="field-icon">{icon}</span>}
        <input
          readOnly={readOnly}
          value={value}
          placeholder={placeholder}
          className={icon ? 'has-icon' : ''}
          onChange={(event) => onChange(event.target.value)}
        />
      </div>
    </label>
  );
}

function SelectField({
  label,
  value,
  disabled,
  required,
  icon,
  className,
  children,
  onChange,
}: {
  label: string;
  value: string;
  disabled?: boolean;
  required?: boolean;
  icon?: React.ReactNode;
  className?: string;
  children: ReactNode;
  onChange: (value: string) => void;
}) {
  return (
    <label className={`rooming-house-editor__field ${className ?? ''}`}>
      <span>
        {label}
        {required && <span className="field-required"> *</span>}
      </span>
      <div className="field-input-wrapper">
        {icon && <span className="field-icon">{icon}</span>}
        <select
          disabled={disabled}
          value={value}
          className={icon ? 'has-icon' : ''}
          onChange={(event) => onChange(event.target.value)}
        >
          {children}
        </select>
        <span className="select-chevron">
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="6 9 12 15 18 9" />
          </svg>
        </span>
      </div>
    </label>
  );
}

function NumberField({
  label,
  value,
  onChange,
}: {
  label: string;
  value?: number | null;
  onChange: (value: number | null) => void;
}) {
  return (
    <label className="rooming-house-editor__field">
      <span>{label}</span>
      <input
        type="number"
        value={value ?? ''}
        onChange={(event) =>
          onChange(event.target.value === '' ? null : Number(event.target.value))
        }
      />
    </label>
  );
}

function LegalImageField({
  label,
  imageUrl,
  readOnly = false,
  optional = false,
  onUpload,
  onRemove,
}: {
  label: string;
  imageUrl?: string;
  readOnly?: boolean;
  optional?: boolean;
  onUpload: (file: File | null) => void;
  onRemove: () => void;
}) {
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const previewSrc = imageUrl || '';

  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragOver(false);
    if (readOnly) return;
    const file = e.dataTransfer.files?.[0];
    if (file) onUpload(file);
  }

  return (
    <div className="legal-image-field">
      {/* Field label */}
      <div className="legal-image-field__label">
        <span>{label}</span>
        {optional && <span className="legal-image-field__optional">Tuỳ chọn</span>}
      </div>

      {previewSrc ? (
        /* Preview state */
        <div className="legal-image-preview">
          <img alt={label} src={previewSrc} className="legal-image-preview__img" />
          {!readOnly && (
            <div className="legal-image-preview__overlay">
              <label className="legal-image-action-btn legal-image-action-btn--replace">
                <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                  <polyline points="17 8 12 3 7 8" />
                  <line x1="12" y1="3" x2="12" y2="15" />
                </svg>
                Thay ảnh
                <input
                  ref={fileInputRef}
                  accept="image/jpeg,image/png,image/webp"
                  type="file"
                  style={{ display: 'none' }}
                  onChange={(e) => {
                    onUpload(e.target.files?.[0] ?? null);
                    e.target.value = '';
                  }}
                />
              </label>
              <button
                type="button"
                className="legal-image-action-btn legal-image-action-btn--delete"
                onClick={onRemove}
              >
                <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="3 6 5 6 21 6" />
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                </svg>
                Xóa
              </button>
            </div>
          )}
        </div>
      ) : (
        /* Dropzone state */
        <div
          className={`legal-image-dropzone ${isDragOver ? 'legal-image-dropzone--active' : ''} ${readOnly ? 'legal-image-dropzone--readonly' : ''}`}
          onClick={() => !readOnly && fileInputRef.current?.click()}
          onDragOver={(e) => { e.preventDefault(); if (!readOnly) setIsDragOver(true); }}
          onDragLeave={() => setIsDragOver(false)}
          onDrop={handleDrop}
        >
          <div className="legal-image-dropzone__icon">
            <svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="16 16 12 12 8 16" />
              <line x1="12" y1="12" x2="12" y2="21" />
              <path d="M20.39 18.39A5 5 0 0 0 18 9h-1.26A8 8 0 1 0 3 16.3" />
            </svg>
          </div>
          <p className="legal-image-dropzone__text">
            {readOnly ? 'Chưa có ảnh' : 'Kéo & thả hoặc bấm để tải lên'}
          </p>
          {!readOnly && (
            <p className="legal-image-dropzone__hint">JPG, PNG hoặc PDF</p>
          )}
          {!readOnly && (
            <input
              ref={fileInputRef}
              accept="image/jpeg,image/png,image/webp"
              type="file"
              style={{ display: 'none' }}
              onChange={(e) => {
                onUpload(e.target.files?.[0] ?? null);
                e.target.value = '';
              }}
            />
          )}
        </div>
      )}
    </div>
  );
}

function ActionRow({ children }: { children: ReactNode }) {
  return <div className="rooming-house-editor__actions">{children}</div>;
}

function getAmenityIcon(name: string) {
  const n = name.toLowerCase();
  if (n.includes('wifi') || n.includes('internet'))
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M12 20h.01" /><path d="M8.5 16.5a5 5 0 0 1 7 0" />
        <path d="M5 13a10 10 0 0 1 14 0" /><path d="M1.5 9.5a15 15 0 0 1 21 0" />
      </svg>
    );
  if (n.includes('camera') || n.includes('an ninh'))
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z" />
        <circle cx="12" cy="13" r="4" />
      </svg>
    );
  if (n.includes('xe') || n.includes('đỗ xe') || n.includes('gửi xe'))
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <rect x="1" y="3" width="15" height="13" rx="2" />
        <path d="M16 8h4l3 5v3h-7V8z" /><circle cx="5.5" cy="18.5" r="2.5" /><circle cx="18.5" cy="18.5" r="2.5" />
      </svg>
    );
  if (n.includes('điều hòa') || n.includes('lạnh') || n.includes('ac'))
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M2 12h20M12 2v20M20 7l-3.5 3.5M4 17l3.5-3.5M17 17l-3.5-3.5M7 7l3.5 3.5" />
      </svg>
    );
  if (n.includes('máy giặt') || n.includes('giặt'))
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M3 6V3a1 1 0 0 1 1-1h16a1 1 0 0 1 1 1v3M3 6v15a1 1 0 0 0 1 1h16a1 1 0 0 0 1-1V6M3 6h18" />
        <circle cx="12" cy="14" r="4" /><path d="M12 12a2.5 2.5 0 0 0 0 4" />
      </svg>
    );
  if (n.includes('tủ lạnh'))
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <rect x="5" y="2" width="14" height="20" rx="2" />
        <line x1="5" y1="10" x2="19" y2="10" /><line x1="9" y1="6" x2="9" y2="8" /><line x1="9" y1="14" x2="9" y2="18" />
      </svg>
    );
  if (n.includes('bếp') || n.includes('nấu ăn'))
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M6 18h12M6 6h12M6 10h12M6 14h12" />
      </svg>
    );
  if (n.includes('wc') || n.includes('khép kín') || n.includes('vệ sinh') || n.includes('phòng tắm'))
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M4 4h16v16H4zM10 8h4v4h-4zM6 16h12" />
      </svg>
    );
  if (n.includes('ban công') || n.includes('cửa sổ'))
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M3 3h18v18H3zM9 9h6v12H9z" />
      </svg>
    );
  if (n.includes('gác') || n.includes('lửng'))
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <polyline points="22 17 13.5 8.5 8.5 13.5 2 7" /><polyline points="16 17 22 17 22 11" />
      </svg>
    );
  // Default
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" /><line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  );
}

function AmenityEditor({
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
  function toggle(id: number) {
    onChange(
      selectedIds.includes(id)
        ? selectedIds.filter((x) => x !== id)
        : [...selectedIds, id]
    );
  }

  return (
    <div className="rooming-house-editor__stack">
      {/* Section header */}
      <div className="amenity-section-header">
        <div>
          <h3 className="amenity-section-title">Tiện ích</h3>
          <p className="amenity-section-subtitle">Chọn các tiện ích hiện có tại khu trọ để người thuê dễ dàng nắm bắt.</p>
        </div>
        {selectedIds.length > 0 && (
          <span className="amenity-selected-badge">
            {selectedIds.length} đã chọn
          </span>
        )}
      </div>

      {/* Card grid */}
      <div className="amenity-card-grid">
        {amenities.map((amenity) => {
          const isSelected = selectedIds.includes(amenity.id);
          return (
            <button
              key={amenity.id}
              type="button"
              className={`amenity-card ${isSelected ? 'amenity-card--selected' : ''}`}
              onClick={() => toggle(amenity.id)}
            >
              {/* Checkmark */}
              <span className={`amenity-card__check ${isSelected ? 'amenity-card__check--visible' : ''}`}>
                <svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="20 6 9 17 4 12" />
                </svg>
              </span>

              {/* Icon */}
              <span className="amenity-card__icon">
                {getAmenityIcon(amenity.name)}
              </span>

              {/* Label */}
              <span className="amenity-card__label">{amenity.name}</span>
            </button>
          );
        })}
      </div>

      {/* Save action */}
      <ActionRow>
        <button
          className="rooming-house-editor__primary"
          type="button"
          onClick={onSave}
          style={{ minHeight: '38px', borderRadius: '8px', padding: '8px 24px' }}
        >
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '8px' }}>
            <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
            <polyline points="17 21 17 13 7 13 7 21" /><polyline points="7 3 7 8 15 8" />
          </svg>
          Lưu
        </button>
      </ActionRow>
    </div>
  );
}

