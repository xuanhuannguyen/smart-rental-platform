import { useEffect, useState } from 'react';
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
import { cleanImages, toImageRequests } from '../utils/imageRequests';
import './RoomingHouseEditor.css';

type RoomingHouseTab = 'basic' | 'images' | 'amenities' | 'legal' | 'rental-policy';
type LegalImageFieldName =
  | 'frontImageObjectKey'
  | 'backImageObjectKey'
  | 'extraImageObjectKey';

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
};

const emptyLegalForm: UpdateLegalDocumentRequest = {
  documentType: '',
  frontImageObjectKey: '',
  backImageObjectKey: '',
  extraImageObjectKey: '',
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
  };
}

function buildLegalForm(detail: RoomingHouseDetail | null): UpdateLegalDocumentRequest {
  if (!detail) return emptyLegalForm;
  return {
    documentType: detail.legalDocument?.documentType ?? '',
    frontImageObjectKey: detail.legalDocument?.frontImageObjectKey ?? '',
    backImageObjectKey: detail.legalDocument?.backImageObjectKey ?? '',
    extraImageObjectKey: detail.legalDocument?.extraImageObjectKey ?? '',
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
  const [message, setMessage] = useState('');
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
        setMessage(getApiErrorMessage(error, 'Không thể tải dữ liệu biểu mẫu.'));
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
        setMessage(getApiErrorMessage(error, 'Không thể tải danh sách phường/xã.'));
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
      setMessage('Vui lòng lưu thông tin cơ bản trước.');
      return;
    }
    await saveChange(
      () => updateRoomingHouseImages(roomingHouse.id, cleanImages(images)),
      'Đã lưu ảnh khu trọ.'
    );
  }

  async function saveAmenities() {
    if (!roomingHouse) {
      setMessage('Vui lòng lưu thông tin cơ bản trước.');
      return;
    }
    await saveChange(
      () => updateRoomingHouseAmenities(roomingHouse.id, amenityIds),
      'Đã lưu tiện ích.'
    );
  }

  async function saveLegalDocument() {
    if (!roomingHouse) {
      setMessage('Vui lòng lưu thông tin cơ bản trước.');
      return;
    }
    if (!canEditLegalDocument) {
      setMessage('Giấy tờ pháp lý chỉ được chỉnh sửa khi khu trọ là bản nháp hoặc bị từ chối.');
      return;
    }
    await saveChange(
      () => updateRoomingHouseLegalDocument(roomingHouse.id, legalForm),
      'Đã lưu giấy tờ pháp lý.'
    );
  }

  async function saveRentalPolicy() {
    if (!roomingHouse) {
      setMessage('Vui lòng lưu thông tin cơ bản trước.');
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
      setMessage('Vui lòng lưu thông tin cơ bản trước khi gửi duyệt.');
      return;
    }

    setSaving(true);
    setMessage('');

    try {
      const submitted = await submitRoomingHouse(roomingHouse.id);
      setRoomingHouse(submitted);
      onSubmitSuccess?.(submitted);
      setMessage('Đã gửi khu trọ để quản trị viên xét duyệt.');
    } catch (error) {
      setMessage(getApiErrorMessage(error, 'Không thể gửi duyệt khu trọ.'));
    } finally {
      setSaving(false);
    }
  }

  async function saveChange(
    request: () => Promise<RoomingHouseDetail>,
    successMessage: string
  ) {
    setSaving(true);
    setMessage('');
    try {
      const saved = await request();
      setRoomingHouse(saved);
      hydrateForms(saved);
      onChange?.(saved);
      setMessage(successMessage);
    } catch (error) {
      setMessage(getApiErrorMessage(error, 'Không thể lưu thông tin khu trọ.'));
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

  async function uploadLegalImage(fieldName: LegalImageFieldName, file: File | null) {
    if (!file || !canEditLegalDocument) return;

    setSaving(true);
    setMessage('');

    try {
      const uploaded = await uploadImage(file, 'LegalDocument');
      setLegalForm((current) => ({ ...current, [fieldName]: uploaded.objectKey }));
    } catch (error) {
      setMessage(
        getApiErrorMessage(error, 'Không thể tải ảnh giấy tờ lên.')
      );
    } finally {
      setSaving(false);
    }
  }

  function removeLegalImage(fieldName: LegalImageFieldName) {
    if (!canEditLegalDocument) return;
    setLegalForm((current) => ({ ...current, [fieldName]: '' }));
  }

  return (
    <div className="rooming-house-editor">
      <header className="rooming-house-editor__header">
        <h1>{title}</h1>
        <p>{subtitle}</p>
        {roomingHouse && (
          <p className="rooming-house-editor__status">
            Trạng thái: {formatStatus(roomingHouse.approvalStatus)}
          </p>
        )}
      </header>

      {message && <p className="rooming-house-editor__message">{message}</p>}
      {saving && <p className="rooming-house-editor__message">Đang xử lý...</p>}

      <section className="rooming-house-editor__panel">
        <div className="rooming-house-editor__tabs">
          <TabButton activeTab={activeTab} tab="basic" onSelect={setActiveTab}>
            Thông tin cơ bản
          </TabButton>
          <TabButton activeTab={activeTab} tab="images" disabled={!hasDraft} onSelect={setActiveTab}>
            Ảnh
          </TabButton>
          <TabButton activeTab={activeTab} tab="amenities" disabled={!hasDraft} onSelect={setActiveTab}>
            Tiện ích
          </TabButton>
          <TabButton activeTab={activeTab} tab="legal" disabled={!hasDraft} onSelect={setActiveTab}>
            Giấy tờ pháp lý
          </TabButton>
          {isApproved && (
            <TabButton activeTab={activeTab} tab="rental-policy" onSelect={setActiveTab}>
              Chính sách thuê
            </TabButton>
          )}
        </div>

        {activeTab === 'basic' && (
          <div className="rooming-house-editor__form-grid">
            <TextField
              label="Tên khu trọ"
              value={basicForm.name}
              onChange={(value) => setBasicForm({ ...basicForm, name: value })}
            />
            <TextField
              label="Mô tả"
              value={basicForm.description ?? ''}
              onChange={(value) => setBasicForm({ ...basicForm, description: value })}
            />
            <TextField
              label="Địa chỉ"
              value={basicForm.addressLine}
              onChange={(value) => setBasicForm({ ...basicForm, addressLine: value })}
            />
            <SelectField label="Tỉnh/Thành phố" value={basicForm.provinceCode} onChange={selectProvince}>
              <option value="">Chọn tỉnh/thành phố</option>
              {provinces.map((province) => (
                <option key={province.code} value={province.code}>
                  {province.name}
                </option>
              ))}
            </SelectField>
            <SelectField
              label="Phường/Xã"
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
            <NumberField
              label="Vĩ độ"
              value={basicForm.latitude}
              onChange={(value) => setBasicForm({ ...basicForm, latitude: value })}
            />
            <NumberField
              label="Kinh độ"
              value={basicForm.longitude}
              onChange={(value) => setBasicForm({ ...basicForm, longitude: value })}
            />
            <ActionRow>
              <button className="rooming-house-editor__primary" disabled={saving} onClick={saveBasicInfo}>
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
          <div className="rooming-house-editor__form-grid">
            {!canEditLegalDocument && (
              <p className="rooming-house-editor__readonly-note">
                Giấy tờ pháp lý của khu trọ đã duyệt chỉ được xem, không thể chỉnh sửa.
              </p>
            )}
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
            <LegalImageField
              label="Ảnh mặt trước"
              objectKey={legalForm.frontImageObjectKey}
              readOnly={!canEditLegalDocument}
              onUpload={(file) => void uploadLegalImage('frontImageObjectKey', file)}
              onRemove={() => removeLegalImage('frontImageObjectKey')}
            />
            <LegalImageField
              label="Ảnh mặt sau"
              objectKey={legalForm.backImageObjectKey}
              readOnly={!canEditLegalDocument}
              onUpload={(file) => void uploadLegalImage('backImageObjectKey', file)}
              onRemove={() => removeLegalImage('backImageObjectKey')}
            />
            <LegalImageField
              label="Ảnh bổ sung"
              objectKey={legalForm.extraImageObjectKey ?? ''}
              readOnly={!canEditLegalDocument}
              onUpload={(file) => void uploadLegalImage('extraImageObjectKey', file)}
              onRemove={() => removeLegalImage('extraImageObjectKey')}
            />
            {canEditLegalDocument && (
              <ActionRow>
                <button className="rooming-house-editor__primary" disabled={saving} onClick={saveLegalDocument}>
                  Lưu
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

      {allowSubmit && canSubmitForReview && (
        <div className="rooming-house-editor__actions">
          <button
            className="rooming-house-editor__primary"
            disabled={saving}
            onClick={submitForReview}
          >
            {submitLabel}
          </button>
        </div>
      )}
    </div>
  );
}

// ─── Sub-components ──────────────────────────────────────────────────────────

function TabButton({
  activeTab,
  tab,
  disabled,
  children,
  onSelect,
}: {
  activeTab: RoomingHouseTab;
  tab: RoomingHouseTab;
  disabled?: boolean;
  children: string;
  onSelect: (tab: RoomingHouseTab) => void;
}) {
  return (
    <button
      className={activeTab === tab ? 'active' : ''}
      disabled={disabled}
      onClick={() => onSelect(tab)}
    >
      {children}
    </button>
  );
}

function TextField({
  label,
  value,
  readOnly,
  onChange,
}: {
  label: string;
  value: string;
  readOnly?: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <label className="rooming-house-editor__field">
      <span>{label}</span>
      <input
        readOnly={readOnly}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function SelectField({
  label,
  value,
  disabled,
  children,
  onChange,
}: {
  label: string;
  value: string;
  disabled?: boolean;
  children: ReactNode;
  onChange: (value: string) => void;
}) {
  return (
    <label className="rooming-house-editor__field">
      <span>{label}</span>
      <select
        disabled={disabled}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        {children}
      </select>
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
  objectKey,
  readOnly,
  onUpload,
  onRemove,
}: {
  label: string;
  objectKey: string;
  readOnly: boolean;
  onUpload: (file: File | null) => void;
  onRemove: () => void;
}) {
  return (
    <div className="rooming-house-editor__legal-image">
      <span>{label}</span>
      {objectKey ? (
        <img alt={label} src={toAssetUrl(objectKey)} />
      ) : (
        <div className="rooming-house-editor__image-placeholder">Chưa có ảnh</div>
      )}
      {!readOnly && (
        <div className="rooming-house-editor__legal-image-actions">
          <label className="property-image-editor__upload">
            <span>{objectKey ? 'Thay ảnh' : 'Tải ảnh lên'}</span>
            <input
              accept="image/jpeg,image/png,image/webp"
              type="file"
              onChange={(event) => {
                onUpload(event.target.files?.[0] ?? null);
                event.target.value = '';
              }}
            />
          </label>
          {objectKey && (
            <button className="property-image-editor__delete" onClick={onRemove}>
              Xóa ảnh
            </button>
          )}
        </div>
      )}
    </div>
  );
}

function ActionRow({ children }: { children: ReactNode }) {
  return <div className="rooming-house-editor__actions">{children}</div>;
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
  return (
    <div className="rooming-house-editor__stack">
      <div className="rooming-house-editor__amenities">
        {amenities.map((amenity) => (
          <label className="rooming-house-editor__checkbox" key={amenity.id}>
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
      <ActionRow>
        <button className="rooming-house-editor__primary" onClick={onSave}>
          Lưu
        </button>
      </ActionRow>
    </div>
  );
}

