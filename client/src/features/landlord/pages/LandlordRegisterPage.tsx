import { useEffect, useMemo, useState, type ChangeEvent, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';
import { Input } from '../../../shared/components/ui/Input';
import { ErrorState } from '../../../shared/components/feedback/ErrorState';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import { landlordApi } from '../services/landlordApi';
import type {
  Amenity,
  PropertyImageItemRequest,
  Province,
  RoomingHouseDetail,
  RoomingHouseOnboarding,
  Ward
} from '../types/landlord.types';

type Step = 'basic' | 'amenities' | 'images' | 'legal' | 'review';
type LandlordSection = 'register' | 'result';

interface BasicFormState {
  name: string;
  description: string;
  addressLine: string;
  provinceCode: string;
  wardCode: string;
  latitude: string;
  longitude: string;
}

interface HouseImageDraft {
  objectKey: string;
  url: string;
  caption: string;
  isCover: boolean;
}

interface LegalFilesState {
  documentType: string;
  documentNumber: string;
  frontImageObjectKey: string;
  frontImageUrl: string;
  backImageObjectKey: string;
  backImageUrl: string;
  extraImageObjectKey: string;
  extraImageUrl: string;
}

const steps: Array<{ id: Step; label: string }> = [
  { id: 'basic', label: 'Thông tin' },
  { id: 'amenities', label: 'Tiện ích' },
  { id: 'images', label: 'Ảnh khu trọ' },
  { id: 'legal', label: 'Giấy tờ' },
  { id: 'review', label: 'Gửi duyệt' }
];

const emptyBasicForm: BasicFormState = {
  name: '',
  description: '',
  addressLine: '',
  provinceCode: '',
  wardCode: '',
  latitude: '',
  longitude: ''
};

const emptyLegalFiles: LegalFilesState = {
  documentType: 'LAND_USE_CERTIFICATE',
  documentNumber: '',
  frontImageObjectKey: '',
  frontImageUrl: '',
  backImageObjectKey: '',
  backImageUrl: '',
  extraImageObjectKey: '',
  extraImageUrl: ''
};

export function LandlordRegisterPage() {
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [successMessage, setSuccessMessage] = useState('');
  const [activeSection, setActiveSection] = useState<LandlordSection>('register');
  const [step, setStep] = useState<Step>('basic');
  const [onboarding, setOnboarding] = useState<RoomingHouseOnboarding | null>(null);
  const [roomingHouse, setRoomingHouse] = useState<RoomingHouseDetail | null>(null);
  const [provinces, setProvinces] = useState<Province[]>([]);
  const [wards, setWards] = useState<Ward[]>([]);
  const [amenities, setAmenities] = useState<Amenity[]>([]);
  const [basicForm, setBasicForm] = useState<BasicFormState>(emptyBasicForm);
  const [selectedAmenityIds, setSelectedAmenityIds] = useState<number[]>([]);
  const [houseImages, setHouseImages] = useState<HouseImageDraft[]>([]);
  const [legalFiles, setLegalFiles] = useState<LegalFilesState>(emptyLegalFiles);

  const canEdit = !onboarding || onboarding.status === 'None' || onboarding.canEdit;
  const hasEnoughImages = houseImages.length >= 3 && houseImages.filter((image) => image.isCover).length === 1;
  const hasLegalDocument = Boolean(legalFiles.frontImageObjectKey && legalFiles.backImageObjectKey && legalFiles.documentNumber.trim());

  const selectedProvinceName = useMemo(
    () => provinces.find((province) => province.code === basicForm.provinceCode)?.name ?? '',
    [basicForm.provinceCode, provinces]
  );

  const selectedWardName = useMemo(
    () => wards.find((ward) => ward.code === basicForm.wardCode)?.name ?? '',
    [basicForm.wardCode, wards]
  );

  useEffect(() => {
    let isMounted = true;

    async function bootstrap() {
      setIsLoading(true);
      setError('');

      try {
        const [provinceResult, amenityResult, onboardingResult] = await Promise.all([
          landlordApi.getProvinces(),
          landlordApi.getHouseAmenities(),
          landlordApi.getOnboarding()
        ]);

        if (!isMounted) {
          return;
        }

        setProvinces(provinceResult);
        setAmenities(amenityResult.data);
        setOnboarding(onboardingResult.data);

        if (onboardingResult.data.roomingHouse) {
          applyRoomingHouse(onboardingResult.data.roomingHouse);
        }

        if (onboardingResult.data.status === 'Pending' || onboardingResult.data.status === 'Approved') {
          setActiveSection('result');
        }
      } catch (err) {
        if (isMounted) {
          setError(getApiErrorMessage(err, 'Không thể tải dữ liệu đăng ký chủ trọ.'));
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    }

    bootstrap();

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => {
    let isMounted = true;

    async function loadWards() {
      if (!basicForm.provinceCode) {
        setWards([]);
        return;
      }

      try {
        const result = await landlordApi.getWards(basicForm.provinceCode);
        if (isMounted) {
          setWards(result);
        }
      } catch (err) {
        if (isMounted) {
          setError(getApiErrorMessage(err, 'Không thể tải danh sách phường/xã.'));
        }
      }
    }

    loadWards();

    return () => {
      isMounted = false;
    };
  }, [basicForm.provinceCode]);

  function applyRoomingHouse(detail: RoomingHouseDetail) {
    setRoomingHouse(detail);
    setBasicForm({
      name: detail.name,
      description: detail.description ?? '',
      addressLine: detail.addressLine,
      provinceCode: detail.provinceCode,
      wardCode: detail.wardCode,
      latitude: detail.latitude?.toString() ?? '',
      longitude: detail.longitude?.toString() ?? ''
    });
    setSelectedAmenityIds(detail.amenities.map((amenity) => amenity.id));
    setHouseImages(
      detail.images
        .sort((a, b) => a.sortOrder - b.sortOrder)
        .map((image) => ({
          objectKey: image.objectKey,
          url: buildAssetUrl(image.imageUrl),
          caption: image.caption ?? '',
          isCover: image.isCover
        }))
    );

    if (detail.legalDocument) {
      setLegalFiles({
        documentType: detail.legalDocument.documentType,
        documentNumber: detail.legalDocument.documentNumberMasked,
        frontImageObjectKey: detail.legalDocument.frontImageObjectKey,
        frontImageUrl: '',
        backImageObjectKey: detail.legalDocument.backImageObjectKey,
        backImageUrl: '',
        extraImageObjectKey: detail.legalDocument.extraImageObjectKey ?? '',
        extraImageUrl: ''
      });
    }
  }

  function buildAssetUrl(url: string) {
    return toAssetUrl(url);
  }

  function updateBasicField(field: keyof BasicFormState, value: string) {
    setBasicForm((current) => ({
      ...current,
      [field]: value,
      ...(field === 'provinceCode' ? { wardCode: '' } : {})
    }));
  }

  async function reloadOnboarding(nextStep?: Step) {
    const result = await landlordApi.getOnboarding();
    setOnboarding(result.data);

    if (result.data.roomingHouse) {
      applyRoomingHouse(result.data.roomingHouse);
    }

    if (nextStep) {
      setStep(nextStep);
    }
  }

  function buildBasicPayload() {
    return {
      name: basicForm.name.trim(),
      description: basicForm.description.trim() || null,
      addressLine: basicForm.addressLine.trim(),
      provinceCode: basicForm.provinceCode,
      wardCode: basicForm.wardCode,
      latitude: basicForm.latitude ? Number(basicForm.latitude) : null,
      longitude: basicForm.longitude ? Number(basicForm.longitude) : null
    };
  }

  async function handleSaveBasic(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    setSuccessMessage('');

    if (!basicForm.name.trim() || !basicForm.addressLine.trim() || !basicForm.provinceCode || !basicForm.wardCode) {
      setError('Vui lòng nhập đầy đủ tên khu trọ, địa chỉ, tỉnh/thành và phường/xã.');
      return;
    }

    setIsSubmitting(true);

    try {
      const payload = buildBasicPayload();
      const response = roomingHouse
        ? await landlordApi.updateBasicInfo(roomingHouse.id, payload)
        : await landlordApi.createDraft(payload);

      applyRoomingHouse(response.data);
      await reloadOnboarding('amenities');
      setSuccessMessage(response.message ?? 'Đã lưu thông tin khu trọ.');
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể lưu thông tin khu trọ.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleSaveAmenities() {
    if (!roomingHouse) {
      setError('Vui lòng lưu thông tin khu trọ trước.');
      setStep('basic');
      return;
    }

    setIsSubmitting(true);
    setError('');
    setSuccessMessage('');

    try {
      const response = await landlordApi.updateAmenities(roomingHouse.id, selectedAmenityIds);
      applyRoomingHouse(response.data);
      await reloadOnboarding('images');
      setSuccessMessage(response.message ?? 'Đã lưu tiện ích khu trọ.');
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể lưu tiện ích khu trọ.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleUploadHouseImage(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';

    if (!file) {
      return;
    }

    if (houseImages.length >= 8) {
      setError('Tạm thời chỉ tải tối đa 8 ảnh khu trọ.');
      return;
    }

    setIsSubmitting(true);
    setError('');

    try {
      const response = await landlordApi.uploadImage(file, 'RoomingHouse');
      setHouseImages((current) => [
        ...current,
        {
          objectKey: response.data.objectKey,
          url: buildAssetUrl(response.data.url),
          caption: '',
          isCover: current.length === 0
        }
      ]);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tải ảnh khu trọ.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleSaveImages() {
    if (!roomingHouse) {
      setError('Vui lòng lưu thông tin khu trọ trước.');
      setStep('basic');
      return;
    }

    if (!hasEnoughImages) {
      setError('Khu trọ cần ít nhất 3 ảnh và đúng 1 ảnh đại diện.');
      return;
    }

    setIsSubmitting(true);
    setError('');
    setSuccessMessage('');

    try {
      const images: PropertyImageItemRequest[] = houseImages.map((image, index) => ({
        objectKey: image.objectKey,
        caption: image.caption.trim() || null,
        isCover: image.isCover,
        sortOrder: index + 1
      }));

      const response = await landlordApi.updateImages(roomingHouse.id, images);
      applyRoomingHouse(response.data);
      await reloadOnboarding('legal');
      setSuccessMessage(response.message ?? 'Đã lưu ảnh khu trọ.');
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể lưu ảnh khu trọ.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleUploadLegalImage(field: 'front' | 'back' | 'extra', event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';

    if (!file) {
      return;
    }

    setIsSubmitting(true);
    setError('');

    try {
      const response = await landlordApi.uploadImage(file, 'LegalDocument');
      setLegalFiles((current) => ({
        ...current,
        [`${field}ImageObjectKey`]: response.data.objectKey,
        [`${field}ImageUrl`]: buildAssetUrl(response.data.url)
      }));
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tải ảnh giấy tờ.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleSaveLegal() {
    if (!roomingHouse) {
      setError('Vui lòng lưu thông tin khu trọ trước.');
      setStep('basic');
      return;
    }

    if (!hasLegalDocument) {
      setError('Vui lòng nhập số giấy tờ và tải ảnh mặt trước, mặt sau giấy tờ.');
      return;
    }

    setIsSubmitting(true);
    setError('');
    setSuccessMessage('');

    try {
      const response = await landlordApi.updateLegalDocument(roomingHouse.id, {
        documentType: legalFiles.documentType,
        documentNumber: legalFiles.documentNumber.trim(),
        frontImageObjectKey: legalFiles.frontImageObjectKey,
        backImageObjectKey: legalFiles.backImageObjectKey,
        extraImageObjectKey: legalFiles.extraImageObjectKey || null
      });

      applyRoomingHouse(response.data);
      await reloadOnboarding('review');
      setSuccessMessage(response.message ?? 'Đã lưu giấy tờ pháp lý.');
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể lưu giấy tờ pháp lý.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleSubmitForReview() {
    if (!roomingHouse) {
      setError('Vui lòng tạo hồ sơ khu trọ trước.');
      return;
    }

    setIsSubmitting(true);
    setError('');
    setSuccessMessage('');

    try {
      const response = await landlordApi.submit(roomingHouse.id);
      applyRoomingHouse(response.data);
      await reloadOnboarding('review');
      setSuccessMessage(response.message ?? 'Đã gửi hồ sơ khu trọ cho admin duyệt.');
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể gửi hồ sơ khu trọ.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  function toggleAmenity(id: number) {
    setSelectedAmenityIds((current) =>
      current.includes(id) ? current.filter((item) => item !== id) : [...current, id]
    );
  }

  function updateHouseImage(index: number, patch: Partial<HouseImageDraft>) {
    setHouseImages((current) =>
      current.map((image, imageIndex) => {
        if (imageIndex !== index) {
          return patch.isCover ? { ...image, isCover: false } : image;
        }

        return { ...image, ...patch };
      })
    );
  }

  function removeHouseImage(index: number) {
    setHouseImages((current) => {
      const next = current.filter((_, imageIndex) => imageIndex !== index);
      if (next.length > 0 && !next.some((image) => image.isCover)) {
        next[0] = { ...next[0], isCover: true };
      }

      return next;
    });
  }

  if (isLoading) {
    return (
      <main className="auth-page">
        <section className="auth-panel landlord-panel">
          <LoadingState message="Đang tải hồ sơ đăng ký chủ trọ..." />
        </section>
      </main>
    );
  }

  if (error && !onboarding && !roomingHouse) {
    return (
      <main className="auth-page">
        <section className="auth-panel landlord-panel">
          <ErrorState message={error} />
          <div className="auth-actions">
            <Button type="button" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
              Về home
            </Button>
          </div>
        </section>
      </main>
    );
  }

  const pendingReview = onboarding?.status === 'Pending';
  const approved = onboarding?.status === 'Approved';

  return (
    <main className="auth-page">
      <section className="auth-panel landlord-panel">
        <p className="eyebrow">Landlord onboarding</p>
        <h1>Đăng ký làm chủ trọ</h1>
        <p className="subtle">
          Hoàn thiện hồ sơ khu trọ đầu tiên, tải ảnh và giấy tờ pháp lý để admin duyệt quyền chủ trọ.
        </p>

        {pendingReview && (
          <Alert type="info">
            Hồ sơ khu trọ đang chờ admin duyệt. Bạn chưa thể chỉnh sửa cho đến khi có kết quả.
          </Alert>
        )}

        {approved && (
          <Alert type="success">
            Hồ sơ khu trọ đã được duyệt. Tài khoản có thể sử dụng chức năng chủ trọ.
          </Alert>
        )}

        {onboarding?.status === 'Rejected' && onboarding.roomingHouse?.rejectedReason && (
          <Alert type="error">Lý do từ chối: {onboarding.roomingHouse.rejectedReason}</Alert>
        )}

        {error && <Alert type="error">{error}</Alert>}
        {successMessage && <Alert type="success">{successMessage}</Alert>}

        <div className="landlord-section-tabs" aria-label="Chức năng đăng ký chủ trọ">
          <button
            type="button"
            className={`landlord-section-tab ${activeSection === 'register' ? 'active' : ''}`}
            onClick={() => setActiveSection('register')}
          >
            Gửi đăng ký làm chủ trọ
          </button>
          <button
            type="button"
            className={`landlord-section-tab ${activeSection === 'result' ? 'active' : ''}`}
            onClick={() => setActiveSection('result')}
          >
            Xem kết quả đã gửi
          </button>
        </div>

        {activeSection === 'result' && (
          <section className="auth-form">
            <div className="result-card">
              <p className="eyebrow">Kết quả hồ sơ</p>
              <h2>{getStatusTitle(onboarding?.status)}</h2>
              <p className="subtle">{getStatusDescription(onboarding?.status)}</p>

              {roomingHouse ? (
                <dl className="user-summary">
                  <div>
                    <dt>Khu trọ</dt>
                    <dd>{roomingHouse.name}</dd>
                  </div>
                  <div>
                    <dt>Địa chỉ</dt>
                    <dd>{roomingHouse.addressDisplay}</dd>
                  </div>
                  <div>
                    <dt>Trạng thái</dt>
                    <dd>{roomingHouse.approvalStatus}</dd>
                  </div>
                  <div>
                    <dt>Hiển thị</dt>
                    <dd>{roomingHouse.visibilityStatus}</dd>
                  </div>
                  <div>
                    <dt>Ảnh</dt>
                    <dd>{roomingHouse.images.length} ảnh</dd>
                  </div>
                  <div>
                    <dt>Giấy tờ</dt>
                    <dd>{roomingHouse.legalDocument ? 'Đã gửi' : 'Chưa gửi'}</dd>
                  </div>
                  {roomingHouse.rejectedReason && (
                    <div>
                      <dt>Lý do từ chối</dt>
                      <dd>{roomingHouse.rejectedReason}</dd>
                    </div>
                  )}
                </dl>
              ) : (
                <div className="compact-summary">
                  Bạn chưa có hồ sơ đăng ký chủ trọ. Hãy chuyển sang phần gửi đăng ký để tạo hồ sơ khu trọ đầu tiên.
                </div>
              )}
            </div>

            <div className="auth-actions">
              {(onboarding?.status === 'None' || onboarding?.status === 'Draft' || onboarding?.status === 'Rejected') && (
                <Button type="button" onClick={() => setActiveSection('register')}>
                  {onboarding?.status === 'Rejected' ? 'Chỉnh sửa và gửi lại' : 'Tiếp tục đăng ký'}
                </Button>
              )}
              <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
                Về home
              </Button>
            </div>
          </section>
        )}

        {activeSection === 'register' && (
          <>
        <div className="landlord-stepper" aria-label="Các bước đăng ký chủ trọ">
          {steps.map((item) => (
            <button
              key={item.id}
              type="button"
              className={`landlord-step ${step === item.id ? 'active' : ''}`}
              onClick={() => setStep(item.id)}
            >
              {item.label}
            </button>
          ))}
        </div>

        {step === 'basic' && (
          <form className="auth-form" onSubmit={handleSaveBasic}>
            <FormField label="Tên khu trọ" htmlFor="house-name">
              <Input
                id="house-name"
                value={basicForm.name}
                disabled={!canEdit}
                onChange={(event) => updateBasicField('name', event.target.value)}
                placeholder="Nhà trọ Hoa Sen"
              />
            </FormField>

            <FormField label="Mô tả" htmlFor="house-description">
              <textarea
                id="house-description"
                className="ui-input ui-textarea"
                value={basicForm.description}
                disabled={!canEdit}
                onChange={(event) => updateBasicField('description', event.target.value)}
                placeholder="Khu trọ sạch sẽ, an ninh, gần trường học..."
              />
            </FormField>

            <FormField label="Địa chỉ chi tiết" htmlFor="house-address">
              <Input
                id="house-address"
                value={basicForm.addressLine}
                disabled={!canEdit}
                onChange={(event) => updateBasicField('addressLine', event.target.value)}
                placeholder="Số nhà, đường"
              />
            </FormField>

            <div className="landlord-grid">
              <FormField label="Tỉnh/thành phố" htmlFor="house-province">
                <select
                  id="house-province"
                  className="ui-input"
                  value={basicForm.provinceCode}
                  disabled={!canEdit}
                  onChange={(event) => updateBasicField('provinceCode', event.target.value)}
                >
                  <option value="">Chọn tỉnh/thành phố</option>
                  {provinces.map((province) => (
                    <option key={province.code} value={province.code}>
                      {province.name}
                    </option>
                  ))}
                </select>
              </FormField>

              <FormField label="Phường/xã" htmlFor="house-ward">
                <select
                  id="house-ward"
                  className="ui-input"
                  value={basicForm.wardCode}
                  disabled={!canEdit || !basicForm.provinceCode}
                  onChange={(event) => updateBasicField('wardCode', event.target.value)}
                >
                  <option value="">Chọn phường/xã</option>
                  {wards.map((ward) => (
                    <option key={ward.code} value={ward.code}>
                      {ward.name}
                    </option>
                  ))}
                </select>
              </FormField>
            </div>

            <div className="landlord-grid">
              <FormField label="Vĩ độ" htmlFor="house-latitude">
                <Input
                  id="house-latitude"
                  type="number"
                  step="0.0000001"
                  value={basicForm.latitude}
                  disabled={!canEdit}
                  onChange={(event) => updateBasicField('latitude', event.target.value)}
                  placeholder="10.762622"
                />
              </FormField>

              <FormField label="Kinh độ" htmlFor="house-longitude">
                <Input
                  id="house-longitude"
                  type="number"
                  step="0.0000001"
                  value={basicForm.longitude}
                  disabled={!canEdit}
                  onChange={(event) => updateBasicField('longitude', event.target.value)}
                  placeholder="106.660172"
                />
              </FormField>
            </div>

            <div className="compact-summary">
              Địa chỉ: {basicForm.addressLine || '...'}
              {selectedWardName ? `, ${selectedWardName}` : ''}
              {selectedProvinceName ? `, ${selectedProvinceName}` : ''}
            </div>

            <div className="auth-actions">
              <Button type="submit" disabled={!canEdit || isSubmitting}>
                {roomingHouse ? 'Lưu thông tin' : 'Tạo hồ sơ khu trọ'}
              </Button>
              <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
                Về home
              </Button>
            </div>
          </form>
        )}

        {step === 'amenities' && (
          <section className="auth-form">
            <div className="amenity-grid">
              {amenities.map((amenity) => (
                <label key={amenity.id} className="checkbox-card">
                  <input
                    type="checkbox"
                    checked={selectedAmenityIds.includes(amenity.id)}
                    disabled={!canEdit}
                    onChange={() => toggleAmenity(amenity.id)}
                  />
                  <span>{amenity.name}</span>
                </label>
              ))}
            </div>

            <div className="auth-actions">
              <Button type="button" disabled={!canEdit || isSubmitting} onClick={handleSaveAmenities}>
                Lưu tiện ích
              </Button>
              <Button type="button" variant="secondary" onClick={() => setStep('basic')}>
                Quay lại
              </Button>
            </div>
          </section>
        )}

        {step === 'images' && (
          <section className="auth-form">
            <FormField label="Tải ảnh khu trọ" htmlFor="house-images">
              <Input
                id="house-images"
                type="file"
                className="file-input"
                accept="image/png,image/jpeg,image/webp"
                disabled={!canEdit || isSubmitting}
                onChange={handleUploadHouseImage}
              />
            </FormField>

            <div className="image-list">
              {houseImages.map((image, index) => (
                <div key={image.objectKey} className="image-item">
                  {image.url && <img src={image.url} alt={image.caption || `Ảnh khu trọ ${index + 1}`} />}
                  <Input
                    value={image.caption}
                    disabled={!canEdit}
                    onChange={(event) => updateHouseImage(index, { caption: event.target.value })}
                    placeholder="Chú thích ảnh"
                  />
                  <label className="inline-check">
                    <input
                      type="radio"
                      name="coverImage"
                      checked={image.isCover}
                      disabled={!canEdit}
                      onChange={() => updateHouseImage(index, { isCover: true })}
                    />
                    Ảnh đại diện
                  </label>
                  <Button type="button" variant="secondary" disabled={!canEdit} onClick={() => removeHouseImage(index)}>
                    Xóa
                  </Button>
                </div>
              ))}
            </div>

            <div className="compact-summary">
              Đã có {houseImages.length}/3 ảnh tối thiểu. Cần đúng 1 ảnh đại diện.
            </div>

            <div className="auth-actions">
              <Button type="button" disabled={!canEdit || isSubmitting} onClick={handleSaveImages}>
                Lưu ảnh khu trọ
              </Button>
              <Button type="button" variant="secondary" onClick={() => setStep('amenities')}>
                Quay lại
              </Button>
            </div>
          </section>
        )}

        {step === 'legal' && (
          <section className="auth-form">
            <FormField label="Loại giấy tờ" htmlFor="legal-type">
              <select
                id="legal-type"
                className="ui-input"
                value={legalFiles.documentType}
                disabled={!canEdit}
                onChange={(event) => setLegalFiles((current) => ({ ...current, documentType: event.target.value }))}
              >
                <option value="LAND_USE_CERTIFICATE">Giấy chứng nhận quyền sử dụng đất</option>
                <option value="BUSINESS_LICENSE">Giấy phép kinh doanh</option>
                <option value="OTHER">Giấy tờ khác</option>
              </select>
            </FormField>

            <FormField label="Số giấy tờ" htmlFor="document-number">
              <Input
                id="document-number"
                value={legalFiles.documentNumber}
                disabled={!canEdit}
                onChange={(event) => setLegalFiles((current) => ({ ...current, documentNumber: event.target.value }))}
                placeholder="Nhập số giấy tờ pháp lý"
              />
            </FormField>

            <div className="landlord-grid">
              <LegalUpload
                label="Ảnh mặt trước"
                inputId="legal-front"
                imageUrl={legalFiles.frontImageUrl}
                objectKey={legalFiles.frontImageObjectKey}
                disabled={!canEdit || isSubmitting}
                onChange={(event) => void handleUploadLegalImage('front', event)}
              />
              <LegalUpload
                label="Ảnh mặt sau"
                inputId="legal-back"
                imageUrl={legalFiles.backImageUrl}
                objectKey={legalFiles.backImageObjectKey}
                disabled={!canEdit || isSubmitting}
                onChange={(event) => void handleUploadLegalImage('back', event)}
              />
            </div>

            <LegalUpload
              label="Ảnh bổ sung"
              inputId="legal-extra"
              imageUrl={legalFiles.extraImageUrl}
              objectKey={legalFiles.extraImageObjectKey}
              disabled={!canEdit || isSubmitting}
              onChange={(event) => void handleUploadLegalImage('extra', event)}
            />

            <div className="auth-actions">
              <Button type="button" disabled={!canEdit || isSubmitting} onClick={handleSaveLegal}>
                Lưu giấy tờ
              </Button>
              <Button type="button" variant="secondary" onClick={() => setStep('images')}>
                Quay lại
              </Button>
            </div>
          </section>
        )}

        {step === 'review' && (
          <section className="auth-form">
            <div className="compact-summary">
              <strong>{roomingHouse?.name || 'Chưa có tên khu trọ'}</strong>
              <br />
              {roomingHouse?.addressDisplay || 'Chưa hoàn thiện địa chỉ'}
              <br />
              Trạng thái: {onboarding?.status ?? 'None'}
            </div>

            <ul className="requirement-list">
              <li className={roomingHouse ? 'done' : ''}>Thông tin cơ bản</li>
              <li className={hasEnoughImages ? 'done' : ''}>Ít nhất 3 ảnh khu trọ và 1 ảnh đại diện</li>
              <li className={hasLegalDocument || Boolean(roomingHouse?.legalDocument) ? 'done' : ''}>Giấy tờ pháp lý</li>
            </ul>

            <div className="auth-actions">
              <Button
                type="button"
                disabled={!canEdit || isSubmitting || !roomingHouse}
                onClick={handleSubmitForReview}
              >
                Gửi admin duyệt
              </Button>
              <Button type="button" variant="secondary" onClick={() => setStep('legal')}>
                Quay lại
              </Button>
            </div>
          </section>
        )}
          </>
        )}
      </section>
    </main>
  );
}

function getStatusTitle(status?: string) {
  switch (status) {
    case 'Pending':
      return 'Hồ sơ đang chờ duyệt';
    case 'Approved':
      return 'Hồ sơ đã được duyệt';
    case 'Rejected':
      return 'Hồ sơ bị từ chối';
    case 'Draft':
      return 'Hồ sơ đang lưu nháp';
    default:
      return 'Chưa gửi hồ sơ';
  }
}

function getStatusDescription(status?: string) {
  switch (status) {
    case 'Pending':
      return 'Admin sẽ kiểm tra thông tin khu trọ, ảnh và giấy tờ pháp lý trước khi cấp quyền chủ trọ.';
    case 'Approved':
      return 'Bạn đã có thể tiếp tục quản lý khu trọ và phòng trọ theo quyền chủ trọ.';
    case 'Rejected':
      return 'Vui lòng xem lý do từ chối, chỉnh sửa hồ sơ và gửi lại để admin duyệt.';
    case 'Draft':
      return 'Hồ sơ đã tạo nhưng chưa gửi duyệt. Bạn có thể tiếp tục hoàn thiện.';
    default:
      return 'Bạn chưa tạo hồ sơ đăng ký làm chủ trọ.';
  }
}

interface LegalUploadProps {
  label: string;
  inputId: string;
  imageUrl: string;
  objectKey: string;
  disabled: boolean;
  onChange: (event: ChangeEvent<HTMLInputElement>) => void;
}

function LegalUpload({ label, inputId, imageUrl, objectKey, disabled, onChange }: LegalUploadProps) {
  return (
    <FormField label={label} htmlFor={inputId}>
      <Input
        id={inputId}
        type="file"
        className="file-input"
        accept="image/png,image/jpeg,image/webp"
        disabled={disabled}
        onChange={onChange}
      />
      {imageUrl && <img className="legal-preview" src={imageUrl} alt={label} />}
      {!imageUrl && objectKey && <p className="auth-note">Đã lưu: {objectKey}</p>}
    </FormField>
  );
}
