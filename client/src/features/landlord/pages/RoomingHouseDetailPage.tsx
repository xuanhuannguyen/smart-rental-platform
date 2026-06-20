import { useState, useEffect, useMemo, ReactNode, FormEvent } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { formatDateVi, formatMoneyString, parseMoneyString } from '../../../shared/utils/format';
import { formatStatus, getStatusToneClass } from '../../../shared/utils/status';
import {
  getAmenities,
  getRoomingHouseDetail,
  updateRoomingHouseAmenities,
  updateRoomingHouseBasicInfo,
  updateRoomingHouseImages,
  updateRoomingHouseRentalPolicy,
  updateRoomingHouseVisibility,
} from '../../rooming-houses/api';
import {
  getRoomsByRoomingHouse,
  createRoom,
} from '../../rooms/api';
import { billingApi } from '../../billing/api';
import type {
  BillingServiceType,
  ServicePrice,
  CreateServicePriceRequest,
  BillingServiceCode,
  BillingMethod,
  PricingUnit,
} from '../../billing/types';
import type {
  RoomingHouseDetail,
  Amenity,
  PropertyImageRequest,
  RoomingHouseBasicInfoRequest,
  UpdateRentalPolicyRequest,
} from '../../rooming-houses/types';
import type { Room, CreateRoomRequest } from '../../rooms/types';
import { getProvinces, getWardsByProvince } from '../../administrative/api';
import type { Province, Ward } from '../../administrative/types';
import PropertyImageEditor from '../../rooming-houses/components/PropertyImageEditor';
import LeafletLocationPicker from '../../rooming-houses/components/LeafletLocationPicker';
import RoomingHouseRuleEditor from '../../rooming-houses/components/RoomingHouseRuleEditor';
import { cleanImages, toImageRequests } from '../../rooming-houses/utils/imageRequests';
import './RoomingHouseDetailPage.css';

type MainTab = 'basic' | 'images' | 'amenities' | 'legal' | 'house-rule' | 'rental-policy' | 'service-prices' | 'rooms' | 'create-room';

const serviceOptions: { code: BillingServiceCode; label: string; method: BillingMethod; unit: string; defaultPrice: number }[] = [
  { code: 'Electric', label: 'Điện', method: 'Metered', unit: 'kWh', defaultPrice: 3500 },
  { code: 'Water', label: 'Nước', method: 'PerPerson', unit: 'người/tháng', defaultPrice: 100000 },
  { code: 'Wifi', label: 'Internet', method: 'PerMonth', unit: 'tháng', defaultPrice: 100000 },
  { code: 'Trash', label: 'Rác', method: 'PerPerson', unit: 'người/tháng', defaultPrice: 50000 },
];

const nextPeriodStart = new Date(new Date().getFullYear(), new Date().getMonth() + 1, 1).toISOString().split('T')[0];

const defaultServicePriceDrafts: CreateServicePriceRequest[] = [];

const emptyRoomForm: CreateRoomRequest = {
  roomNumber: '',
  floor: 1,
  areaM2: null,
  maxOccupants: 1,
  isTieredPricing: false,
  description: '',
};

const emptyRentalPolicyForm: UpdateRentalPolicyRequest = {
  minRentalMonths: 1,
  maxRentalMonths: 12,
  allowShortTermRenewal: false,
  renewalNoticeDays: 30,
  depositMonths: 1,
  defaultPaymentDay: 5,
};

export default function RoomingHouseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { currentUser } = useAuth();

  // States của Khu trọ
  const [house, setHouse] = useState<RoomingHouseDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [activeTab, setActiveTab] = useState<MainTab>('rooms');
  const [newRoomForm, setNewRoomForm] = useState<CreateRoomRequest>(emptyRoomForm);


  // Lease Policy
  const [rentalPolicyForm, setRentalPolicyForm] = useState<UpdateRentalPolicyRequest>(emptyRentalPolicyForm);

  // Địa giới hành chính cho Tab 1
  const [provinces, setProvinces] = useState<Province[]>([]);
  const [wards, setWards] = useState<Ward[]>([]);
  const [basicForm, setBasicForm] = useState<RoomingHouseBasicInfoRequest>({
    name: '',
    description: '',
    addressLine: '',
    provinceCode: '',
    wardCode: '',
    latitude: null,
    longitude: null,
    googleMapUrl: '',
  });

  // Tiện ích cho Tab 3
  const [houseAmenities, setHouseAmenities] = useState<Amenity[]>([]);
  const [selectedAmenityIds, setSelectedAmenityIds] = useState<number[]>([]);

  // Ảnh cho Tab 2
  const [houseImages, setHouseImages] = useState<PropertyImageRequest[]>([]);

  // States của Quản lý Phòng (Tab 5)
  const [rooms, setRooms] = useState<Room[]>([]);

  // States của Bảng giá dịch vụ (Tab Service Prices)
  const [billingServiceTypes, setBillingServiceTypes] = useState<BillingServiceType[]>([]);
  const [servicePrices, setServicePrices] = useState<ServicePrice[]>([]);
  const [servicePricesLoading, setServicePricesLoading] = useState(false);
  const [servicePriceDrafts, setServicePriceDrafts] = useState<CreateServicePriceRequest[]>(defaultServicePriceDrafts);
  const [servicePriceNote, setServicePriceNote] = useState('');

  const activeServicePrices = useMemo(() => servicePrices.filter(p => p.isActive), [servicePrices]);
  const servicePriceHistory = useMemo(() => servicePrices, [servicePrices]);

  async function loadServicePrices() {
    if (!id) return;
    setServicePricesLoading(true);
    try {
      const [pricesResponse, serviceTypesResponse] = await Promise.all([
        billingApi.getServicePrices(id),
        billingApi.getServiceTypes(),
      ]);
      const activeServiceTypes = serviceTypesResponse.data
        .filter((serviceType) => serviceType.isActive)
        .sort((a, b) => a.name.localeCompare(b.name, 'vi'));
      const activePriceByServiceTypeId = new Map(
        pricesResponse.data
          .filter((price: ServicePrice) => price.isActive)
          .map((price: ServicePrice) => [price.serviceTypeId, price])
      );

      setBillingServiceTypes(activeServiceTypes);
      setServicePrices(pricesResponse.data);
      setServicePriceDrafts(activeServiceTypes.map((serviceType) => {
        const activePrice = activePriceByServiceTypeId.get(serviceType.id);
        const pricingUnit = normalizePricingUnit(activePrice?.pricingUnit ?? getDefaultServicePricingUnit(serviceType));

        return {
          serviceTypeId: serviceType.id,
          serviceCode: serviceType.id,
          pricingUnit,
          billingMethod: pricingUnit,
          unitName: getServicePricingUnitDisplayUnit(pricingUnit, serviceType),
          unitPrice: activePrice?.unitPrice ?? 0,
          effectiveFrom: nextPeriodStart,
        };
      }));
    } catch (err) {
      console.warn('Cannot load service prices:', err);
    } finally {
      setServicePricesLoading(false);
    }
  }

  function handleChangeServicePriceDraft(serviceTypeId: string, patch: Partial<CreateServicePriceRequest>) {
    setServicePriceDrafts((drafts) =>
      drafts.map((draft) => {
        if (draft.serviceTypeId !== serviceTypeId) {
          return draft;
        }

        const pricingUnit = normalizePricingUnit(patch.pricingUnit ?? patch.billingMethod ?? draft.pricingUnit ?? draft.billingMethod ?? 'PerMonth');

        return {
          ...draft,
          ...patch,
          pricingUnit,
          billingMethod: pricingUnit,
          effectiveFrom: nextPeriodStart,
        };
      })
    );
  }

  async function handleSaveServicePrices(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!house) return;

    setActionLoading(true);
    setMessage('');
    try {
      for (const draft of servicePriceDrafts) {
        await billingApi.createServicePrice(house.id, {
          ...draft,
          unitPrice: Number(draft.unitPrice) || 0,
          effectiveFrom: nextPeriodStart,
          note: servicePriceNote.trim() || null,
        });
      }

      await loadServicePrices();
      setMessage('Đã lưu tất cả giá dịch vụ thành công.');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể lưu giá dịch vụ.'));
    } finally {
      setActionLoading(false);
    }
  }

  // Tải thông tin khu trọ
  useEffect(() => {
    if (!id) return;
    loadHouseData();
  }, [id]);

  async function loadHouseData() {
    setLoading(true);
    setMessage('');
    try {
      const data = await getRoomingHouseDetail(id!);

      // Chỉ cho phép khu trọ đã duyệt vào trang này
      if (data.approvalStatus !== 'Approved') {
        setMessage('Khu trọ này chưa được quản trị viên phê duyệt. Không thể truy cập quản lý phòng.');
        setHouse(null);
        return;
      }

      setHouse(data);

      // Khởi tạo các form
      setBasicForm({
        name: data.name,
        description: data.description ?? '',
        addressLine: data.addressLine,
        provinceCode: data.provinceCode,
        wardCode: data.wardCode,
        latitude: data.latitude ?? null,
        longitude: data.longitude ?? null,
        googleMapUrl: data.googleMapUrl ?? '',
      });

      setHouseImages(toImageRequests(data.images));
      setSelectedAmenityIds(data.amenities.map(a => a.id));

      if (data.rentalPolicy) {
        setRentalPolicyForm({
          minRentalMonths: data.rentalPolicy.minRentalMonths,
          maxRentalMonths: data.rentalPolicy.maxRentalMonths,
          allowShortTermRenewal: data.rentalPolicy.allowShortTermRenewal,
          renewalNoticeDays: data.rentalPolicy.renewalNoticeDays,
          depositMonths: data.rentalPolicy.depositMonths,
          defaultPaymentDay: data.rentalPolicy.defaultPaymentDay,
        });
      } else {
        setRentalPolicyForm(emptyRentalPolicyForm);
        setMessage('Vui lòng Hoàn Thành Chính Sách Thuê Ở trong thông tin khu trọ để được tạo phòng');
      }

      // Tải danh sách phòng
      if (data.rentalPolicy && data.houseRule) {
        try {
          const roomsData = await getRoomsByRoomingHouse(id!);
          setRooms(roomsData);
        } catch (err) {
          console.warn('Lỗi tải phòng:', err);
        }
      } else {
        setRooms([]);
        if (data.rentalPolicy && !data.houseRule) {
          setMessage('Vui lòng hoàn thành Luật khu trọ trước khi tạo phòng đầu tiên.');
        }
      }
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể tải thông tin chi tiết khu trọ.'));
    } finally {
      setLoading(false);
    }
  }

  // Tải Tỉnh/Thành & Tiện ích
  useEffect(() => {
    if (activeTab === 'basic' && provinces.length === 0) {
      getProvinces().then(setProvinces).catch(() => { });
    }
    if (activeTab === 'amenities' && houseAmenities.length === 0) {
      getAmenities('House').then(setHouseAmenities).catch(() => { });
    }
    if (activeTab === 'service-prices' && house) {
      loadServicePrices();
    }
  }, [activeTab]);

  // Tải Phường/Xã theo Tỉnh/Thành
  useEffect(() => {
    if (!basicForm.provinceCode) {
      setWards([]);
      return;
    }
    getWardsByProvince(basicForm.provinceCode).then(setWards).catch(() => { });
  }, [basicForm.provinceCode]);

  // Thống kê phòng
  const roomStats = useMemo(() => ({
    total: rooms.length,
    available: rooms.filter((r) => r.status === 'Available').length,
    occupied: rooms.filter((r) => r.status === 'Occupied').length,
    hidden: rooms.filter((r) => r.status === 'Hidden' || r.status === 'Maintenance').length,
  }), [rooms]);
  const selectedProvinceName =
    provinces.find((province) => province.code === basicForm.provinceCode)?.name ?? '';
  const selectedWardName = wards.find((ward) => ward.code === basicForm.wardCode)?.name ?? '';
  const canCreateRoom = Boolean(house?.rentalPolicy && house?.houseRule);
  const addressLocked = house?.approvalStatus === 'Approved';

  // ─── Tab 1: Lưu thông tin cơ bản khu trọ ──────────────────────────────────
  async function handleSaveBasicInfo() {
    if (!house) return;
    setActionLoading(true);
    setMessage('');
    try {
      const updated = await updateRoomingHouseBasicInfo(house.id, basicForm);
      setHouse(updated);
      setMessage('Đã cập nhật thông tin cơ bản khu trọ thành công.');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể cập nhật thông tin cơ bản.'));
    } finally {
      setActionLoading(false);
    }
  }

  // ─── Tab 2: Lưu ảnh khu trọ ──────────────────────────────────────────────
  async function handleToggleVisibility() {
    if (!house) return;

    const nextVisibility = house.visibilityStatus === 'Visible' ? 'Hidden' : 'Visible';
    setActionLoading(true);
    setMessage('');
    try {
      const updated = await updateRoomingHouseVisibility(house.id, nextVisibility);
      setHouse(updated);
      setMessage(nextVisibility === 'Hidden'
        ? 'Khu trọ đã được ẩn khỏi trang công khai.'
        : 'Khu trọ đã được hiển thị công khai.');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể cập nhật trạng thái hiển thị khu trọ.'));
    } finally {
      setActionLoading(false);
    }
  }

  async function handleSaveImages() {
    if (!house) return;
    setActionLoading(true);
    setMessage('');
    try {
      const updated = await updateRoomingHouseImages(house.id, cleanImages(houseImages));
      setHouse(updated);
      setHouseImages(toImageRequests(updated.images));
      setMessage('Đã lưu ảnh minh họa khu trọ thành công.');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể cập nhật ảnh khu trọ.'));
    } finally {
      setActionLoading(false);
    }
  }

  // ─── Tab 3: Lưu tiện ích khu trọ ─────────────────────────────────────────
  async function handleSaveAmenities() {
    if (!house) return;
    setActionLoading(true);
    setMessage('');
    try {
      const updated = await updateRoomingHouseAmenities(house.id, selectedAmenityIds);
      setHouse(updated);
      setSelectedAmenityIds(updated.amenities.map(a => a.id));
      setMessage('Đã cập nhật tiện ích khu trọ thành công.');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể cập nhật tiện ích khu trọ.'));
    } finally {
      setActionLoading(false);
    }
  }

  // ─── Tab 4: Lưu chính sách thuê khu trọ ────────────────────────────────────
  async function handleSaveRentalPolicy() {
    if (!house) return;
    setActionLoading(true);
    setMessage('');
    try {
      await updateRoomingHouseRentalPolicy(house.id, rentalPolicyForm);
      const updated = await getRoomingHouseDetail(house.id);
      setHouse(updated);
      if (updated.rentalPolicy) {
        setRentalPolicyForm({
          minRentalMonths: updated.rentalPolicy.minRentalMonths,
          maxRentalMonths: updated.rentalPolicy.maxRentalMonths,
          allowShortTermRenewal: updated.rentalPolicy.allowShortTermRenewal,
          renewalNoticeDays: updated.rentalPolicy.renewalNoticeDays,
          depositMonths: updated.rentalPolicy.depositMonths,
          defaultPaymentDay: updated.rentalPolicy.defaultPaymentDay,
        });
      }
      setMessage('Đã cập nhật chính sách thuê thành công.');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể cập nhật chính sách thuê.'));
    } finally {
      setActionLoading(false);
    }
  }

  function handleHouseRuleSaved(savedRule: NonNullable<RoomingHouseDetail['houseRule']>) {
    setHouse((current) => current ? { ...current, houseRule: savedRule } : current);
    setMessage('Đã cập nhật luật khu trọ thành công.');
  }



  function handleCreateRoomClick() {
    if (!house?.rentalPolicy) {
      setMessage('Vui lòng hoàn thành chính sách cho thuê trước khi tạo phòng.');
      setActiveTab('rental-policy');
      return;
    }

    if (!house.houseRule) {
      setMessage('Vui lòng hoàn thành Luật khu trọ trước khi tạo phòng đầu tiên.');
      setActiveTab('house-rule');
      return;
    }
    setActiveTab('create-room');
    setNewRoomForm(emptyRoomForm);
  }

  async function handleCreateRoomSubmit() {
    if (!house) return;
    setActionLoading(true);
    setMessage('');
    try {
      const createdRoom = await createRoom(house.id, newRoomForm);
      setMessage('Tạo phòng mới thành công. Hãy tiếp tục cập nhật các thông tin khác.');
      setNewRoomForm(emptyRoomForm);
      // Chuyển hướng sang trang chi tiết phòng vừa tạo để có thể edit Ảnh, Tiện ích, Bảng giá
      navigate(ROUTE_PATHS.LANDLORD.ROOM_DETAIL(house.id, createdRoom.id), { state: { initialTab: 'images' } });
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể tạo phòng.'));
    } finally {
      setActionLoading(false);
    }
  }


  // ─── Renders ─────────────────────────────────────────────────────────────
  const formatDate = formatDateVi;

  if (loading) {
    return (
      <div className="rooming-house-detail-page" style={{ display: 'contents' }}>
        <main className="dashboard-main">
          <div className="empty-panel">Đang tải thông tin khu trọ...</div>
        </main>
      </div>
    );
  }

  if (!house) {
    return (
      <div className="rooming-house-detail-page" style={{ display: 'contents' }}>
        <main className="dashboard-main">
          <div className="empty-panel">
            <h2>Lỗi truy cập</h2>
            <p>{message || 'Không thể truy cập thông tin khu trọ này.'}</p>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="rooming-house-detail-page" style={{ display: 'contents' }}>
      <main className="dashboard-main">
        {/* Banner Tổng quan */}
        {/* Banner Tổng quan */}
        <section className="overview-band">
          <div className="overview-header-title-area">
            <button
              type="button"
              className="back-icon-btn"
              onClick={() => navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSES)}
              title="Quay về quản lý khu trọ"
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <line x1="19" y1="12" x2="5" y2="12" />
                <polyline points="12 19 5 12 12 5" />
              </svg>
            </button>
            <div className="overview-left">
              <p className="eyebrow">{house.addressDisplay}</p>
              <h2>
                {house.name}
              </h2>
              <p className="overview-description">
                Thời gian duyệt: {house.createdAt ? formatDate(house.createdAt) : ''}
              </p>
            </div>
          </div>

          <div className="overview-right" style={{ display: 'flex', flexDirection: 'column', gap: '16px', alignItems: 'flex-end' }}>
            <div className="overview-stats">
              <div className="stat-item stat-item--total">
                <HomeIcon />
                <span>Tổng số phòng</span>
                <strong className="stat-badge">{roomStats.total}</strong>
              </div>
              <div className="stat-item stat-item--approved">
                <CheckCircleIcon />
                <span>Số phòng trống</span>
                <strong className="stat-badge">{roomStats.available}</strong>
              </div>
              <div className="stat-item stat-item--pending">
                <UserCheckIcon />
                <span>Đang thuê</span>
                <strong className="stat-badge">{roomStats.occupied}</strong>
              </div>
              <div className="stat-item stat-item--rejected">
                <EyeOffIcon />
                <span>Đang ẩn</span>
                <strong className="stat-badge">{roomStats.hidden}</strong>
              </div>
            </div>

            <div className="overview-actions" style={{ display: 'flex', gap: '12px' }}>
              <button
                className="secondary-action"
                onClick={() => setActiveTab('rooms')}
                style={{
                  borderColor: activeTab === 'rooms' ? '#2563eb' : undefined,
                  color: activeTab === 'rooms' ? '#2563eb' : undefined,
                  backgroundColor: activeTab === 'rooms' ? '#f0f9ff' : undefined
                }}
              >
                Danh sách phòng
              </button>
              <button
                className="secondary-action"
                onClick={() => {
                  if (activeTab === 'rooms' || activeTab === 'create-room') setActiveTab('basic');
                }}
                style={{
                  borderColor: (activeTab !== 'rooms' && activeTab !== 'create-room') ? '#2563eb' : undefined,
                  color: (activeTab !== 'rooms' && activeTab !== 'create-room') ? '#2563eb' : undefined,
                  backgroundColor: (activeTab !== 'rooms' && activeTab !== 'create-room') ? '#f0f9ff' : undefined
                }}
              >
                Thông tin khu trọ
              </button>
              {house.approvalStatus === 'Approved' && (
                <button
                  className="secondary-action"
                  onClick={handleToggleVisibility}
                  disabled={actionLoading}
                >
                  {house.visibilityStatus === 'Visible' ? 'Ẩn khu trọ' : 'Hiển thị khu trọ'}
                </button>
              )}
              <button
                className="primary-action"
                onClick={handleCreateRoomClick}
                disabled={!canCreateRoom}
                title={!canCreateRoom ? "Vui lòng cấu hình chính sách thuê và Luật khu trọ trước khi tạo phòng." : "Tạo phòng mới"}
                style={!canCreateRoom ? { opacity: 0.5, cursor: 'not-allowed' } : undefined}
              >
                + Tạo phòng mới
              </button>
            </div>
          </div>
        </section>

        {message && <p className="dashboard-message">{message}</p>}
        {actionLoading && <p className="dashboard-message" style={{ background: '#dbeafe', color: '#1e40af' }}>Đang lưu thay đổi...</p>}

        {/* Hệ thống Tab Cấp 2 */}
        {(activeTab !== 'rooms' && activeTab !== 'create-room') && (
          <div className="tabs" style={{ display: 'flex', alignItems: 'center', marginTop: '16px' }}>
            <button className={activeTab === 'basic' ? 'active' : ''} onClick={() => setActiveTab('basic')}>
              Thông tin cơ bản
            </button>
            <button className={activeTab === 'images' ? 'active' : ''} onClick={() => setActiveTab('images')}>
              Ảnh khu trọ
            </button>
            <button className={activeTab === 'amenities' ? 'active' : ''} onClick={() => setActiveTab('amenities')}>
              Tiện nghi
            </button>
            <button className={activeTab === 'legal' ? 'active' : ''} onClick={() => setActiveTab('legal')}>
              Giấy tờ pháp lý
            </button>
            <button className={activeTab === 'house-rule' ? 'active' : ''} onClick={() => setActiveTab('house-rule')}>
              Luật khu trọ
            </button>
            <button className={activeTab === 'rental-policy' ? 'active' : ''} onClick={() => setActiveTab('rental-policy')}>
              Chính sách thuê
            </button>
            <button className={activeTab === 'service-prices' ? 'active' : ''} onClick={() => setActiveTab('service-prices')}>
              Bảng giá dịch vụ
            </button>
          </div>
        )}

        {/* Nội dung Tab */}
        <div className="tab-content" style={{ marginTop: '16px' }}>
          {/* TAB 1: THÔNG TIN CƠ BẢN */}
          {activeTab === 'basic' && (
            <div className="editor-panel form-grid">
              {addressLocked && (
                <div className="empty-panel compact" style={{ gridColumn: '1 / -1' }}>
                  Địa chỉ hành chính của khu trọ đã được duyệt nên không thể chỉnh sửa. Bạn vẫn có thể cập nhật tọa độ và link Google Maps.
                </div>
              )}
              <label className="field">
                <span>Tên khu trọ</span>
                <input value={basicForm.name} onChange={e => setBasicForm({ ...basicForm, name: e.target.value })} />
              </label>

              <label className="field">
                <span>Địa chỉ chi tiết</span>
                <input disabled={addressLocked} value={basicForm.addressLine} onChange={e => setBasicForm({ ...basicForm, addressLine: e.target.value })} />
              </label>

              <label className="field">
                <span>Tỉnh / Thành phố</span>
                <select
                  disabled={addressLocked}
                  value={basicForm.provinceCode}
                  onChange={e => setBasicForm({ ...basicForm, provinceCode: e.target.value, wardCode: '' })}
                >
                  <option value="">-- Chọn Tỉnh/Thành phố --</option>
                  {provinces.map(p => (
                    <option key={p.code} value={p.code}>{p.name}</option>
                  ))}
                </select>
              </label>

              <label className="field">
                <span>Phường / Xã</span>
                <select
                  value={basicForm.wardCode}
                  onChange={e => setBasicForm({ ...basicForm, wardCode: e.target.value })}
                  disabled={addressLocked || !basicForm.provinceCode}
                >
                  <option value="">-- Chọn Phường/Xã --</option>
                  {wards.map(w => (
                    <option key={w.code} value={w.code}>{w.name}</option>
                  ))}
                </select>
              </label>

              <LeafletLocationPicker
                addressLine={basicForm.addressLine}
                provinceName={selectedProvinceName}
                wardName={selectedWardName}
                latitude={basicForm.latitude}
                longitude={basicForm.longitude}
                googleMapUrl={basicForm.googleMapUrl}
                onAddressChange={(addressLine) =>
                  setBasicForm((current) => addressLocked ? current : { ...current, addressLine })
                }
                onLocationChange={(latitude, longitude) =>
                  setBasicForm((current) => ({ ...current, latitude, longitude }))
                }
                onGoogleMapUrlChange={(googleMapUrl) =>
                  setBasicForm((current) => ({ ...current, googleMapUrl }))
                }
              />

              <label className="field" style={{ gridColumn: '1 / -1' }}>
                <span>Mô tả khu trọ</span>
                <textarea
                  style={{ width: '100%', minHeight: '100px', padding: '10px', border: '1px solid #cbd5e1', borderRadius: '6px', font: 'inherit' }}
                  value={basicForm.description ?? ''}
                  onChange={e => setBasicForm({ ...basicForm, description: e.target.value })}
                />
              </label>

              <div className="save-row">
                <button className="primary-action" onClick={handleSaveBasicInfo}>Lưu thông tin</button>
              </div>
            </div>
          )}

          {/* TAB 2: ẢNH KHU TRỌ */}
          {activeTab === 'images' && (
            <div className="editor-panel">
              <PropertyImageEditor
                images={houseImages}
                scope="RoomingHouse"
                onChange={setHouseImages}
                onSave={handleSaveImages}
              />
            </div>
          )}

          {/* TAB 3: TIỆN NGHI KHU TRỌ */}
          {activeTab === 'amenities' && (
            <div className="editor-panel">
              <AmenityEditor
                amenities={houseAmenities}
                selectedIds={selectedAmenityIds}
                onChange={setSelectedAmenityIds}
                onSave={handleSaveAmenities}
              />
            </div>
          )}

          {/* TAB 4: GIẤY TỜ PHÁP LÝ (READ ONLY) */}
          {activeTab === 'legal' && (
            <div className="editor-panel">
              <h3>Thông tin giấy tờ pháp lý</h3>
              <p style={{ color: '#64748b', marginBottom: '20px' }}>Giấy tờ pháp lý của khu trọ đã được duyệt và không thể chỉnh sửa.</p>

              <div className="form-grid" style={{ marginBottom: '24px' }}>
                <label className="field">
                  <span>Loại giấy tờ</span>
                  <input value={house.legalDocument?.documentType ?? 'Không xác định'} readOnly style={{ background: '#f8fafc', color: '#64748b' }} />
                </label>

                <label className="field">
                  <span>Số giấy tờ (Đã ẩn)</span>
                  <input value={house.legalDocument?.documentNumberMasked ?? 'Không xác định'} readOnly style={{ background: '#f8fafc', color: '#64748b' }} />
                </label>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '20px' }}>
                <div>
                  <h4 style={{ marginBottom: '8px' }}>Mặt trước giấy tờ</h4>
                  {house.legalDocument?.frontImageObjectKey ? (
                    <img
                      src={toAssetUrl(house.legalDocument.frontImageObjectKey)}
                      alt="Front"
                      style={{ width: '100%', maxHeight: '240px', objectFit: 'contain', border: '1px solid #dbe3ef', borderRadius: '6px' }}
                    />
                  ) : (
                    <p style={{ color: '#94a3b8', fontStyle: 'italic' }}>Chưa tải lên ảnh</p>
                  )}
                </div>

                <div>
                  <h4 style={{ marginBottom: '8px' }}>Mặt sau giấy tờ</h4>
                  {house.legalDocument?.backImageObjectKey ? (
                    <img
                      src={toAssetUrl(house.legalDocument.backImageObjectKey)}
                      alt="Back"
                      style={{ width: '100%', maxHeight: '240px', objectFit: 'contain', border: '1px solid #dbe3ef', borderRadius: '6px' }}
                    />
                  ) : (
                    <p style={{ color: '#94a3b8', fontStyle: 'italic' }}>Chưa tải lên ảnh</p>
                  )}
                </div>

                {house.legalDocument?.extraImageObjectKey && (
                  <div>
                    <h4 style={{ marginBottom: '8px' }}>Ảnh bổ sung</h4>
                    <img
                      src={toAssetUrl(house.legalDocument.extraImageObjectKey)}
                      alt="Extra"
                      style={{ width: '100%', maxHeight: '240px', objectFit: 'contain', border: '1px solid #dbe3ef', borderRadius: '6px' }}
                    />
                  </div>
                )}
              </div>
            </div>
          )}

          {/* TAB 4.5: LUẬT KHU TRỌ */}
          {activeTab === 'house-rule' && (
            <div className="editor-panel">
              <h3 style={{ margin: '0 0 12px 0' }}>Luật khu trọ</h3>
              <RoomingHouseRuleEditor
                roomingHouseId={house.id}
                houseRule={house.houseRule}
                onSaved={handleHouseRuleSaved}
              />
            </div>
          )}

          {/* TAB 4.5: CHÍNH SÁCH THUÊ */}
          {activeTab === 'rental-policy' && (
            <div className="editor-panel" style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
              <h3 style={{ margin: '0 0 10px 0', borderBottom: '1px solid #e2e8f0', paddingBottom: '12px', color: '#1e293b', fontSize: '18px', fontWeight: 600 }}>
                Cấu hình chính sách cho thuê
              </h3>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '20px' }}>
                {/* Section 1: Điều khoản cơ bản */}
                <div style={{ background: '#f8fafc', padding: '20px', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
                  <h4 style={{ margin: '0 0 16px 0', color: '#334155', fontSize: '15px', fontWeight: 600 }}>Điều khoản cơ bản</h4>
                  <div className="form-grid">
                    <label className="field">
                      <span>Số tháng thuê tối thiểu</span>
                      <input
                        type="number"
                        min="1"
                        value={rentalPolicyForm.minRentalMonths}
                        onChange={(e) =>
                          setRentalPolicyForm({ ...rentalPolicyForm, minRentalMonths: Number(e.target.value) || 1 })
                        }
                      />
                    </label>

                    <label className="field">
                      <span>Số tháng thuê tối đa</span>
                      <input
                        type="number"
                        min="1"
                        value={rentalPolicyForm.maxRentalMonths}
                        onChange={(e) =>
                          setRentalPolicyForm({ ...rentalPolicyForm, maxRentalMonths: Number(e.target.value) || 1 })
                        }
                      />
                    </label>

                    <label className="field">
                      <span>Số ngày báo trước khi gia hạn</span>
                      <input
                        type="number"
                        min="0"
                        value={rentalPolicyForm.renewalNoticeDays}
                        onChange={(e) =>
                          setRentalPolicyForm({ ...rentalPolicyForm, renewalNoticeDays: Number(e.target.value) || 0 })
                        }
                      />
                    </label>

                    <label className="field">
                      <span>Số tháng tiền cọc</span>
                      <input
                        type="number"
                        min="0"
                        value={rentalPolicyForm.depositMonths}
                        onChange={(e) =>
                          setRentalPolicyForm({ ...rentalPolicyForm, depositMonths: Number(e.target.value) || 0 })
                        }
                      />
                    </label>

                    <label className="field">
                      <span>Ngày thanh toán mặc định</span>
                      <input
                        type="number"
                        min="1"
                        max="28"
                        value={rentalPolicyForm.defaultPaymentDay}
                        onChange={(e) =>
                          setRentalPolicyForm({ ...rentalPolicyForm, defaultPaymentDay: Number(e.target.value) || 5 })
                        }
                      />
                    </label>
                  </div>

                  <div style={{ marginTop: '16px' }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '10px', cursor: 'pointer', userSelect: 'none' }}>
                      <input
                        type="checkbox"
                        style={{ width: '18px', height: '18px', margin: 0, cursor: 'pointer' }}
                        checked={rentalPolicyForm.allowShortTermRenewal}
                        onChange={(e) =>
                          setRentalPolicyForm({ ...rentalPolicyForm, allowShortTermRenewal: e.target.checked })
                        }
                      />
                      <span style={{ fontSize: '14px', fontWeight: 600, color: '#475569' }}>
                        Cho phép gia hạn ngắn hạn (dưới 6 tháng)
                      </span>
                    </label>
                  </div>
                </div>
              </div>

              <div className="save-row" style={{ marginTop: '8px' }}>
                <button className="primary-action" onClick={handleSaveRentalPolicy}>Lưu chính sách thuê</button>
              </div>
            </div>
          )}

          {/* TAB: TẠO PHÒNG MỚI */}
          {activeTab === 'create-room' && (
            <div className="editor-panel" style={{ marginTop: '20px', background: '#f8fafc', border: '1px solid #cbd5e1' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <h3 style={{ margin: 0, fontSize: '18px', color: '#1e293b' }}>Tạo phòng mới</h3>
              </div>

              <div className="tabs" style={{ marginBottom: '16px', display: 'flex', alignItems: 'center' }}>
                <button className="active" style={{ fontWeight: 600 }}>Thông tin cơ bản</button>
                <button disabled style={{ opacity: 0.5, cursor: 'not-allowed' }}>Ảnh phòng</button>
                <button disabled style={{ opacity: 0.5, cursor: 'not-allowed' }}>Tiện ích phòng</button>
                <button disabled style={{ opacity: 0.5, cursor: 'not-allowed' }}>Bảng giá</button>
              </div>

              <div className="form-grid">
                <label className="field">
                  <span>Số phòng / Tên phòng</span>
                  <input value={newRoomForm.roomNumber} onChange={e => setNewRoomForm({ ...newRoomForm, roomNumber: e.target.value })} placeholder="VD: 101" />
                </label>

                <label className="field">
                  <span>Tầng</span>
                  <input type="number" value={newRoomForm.floor} onChange={e => setNewRoomForm({ ...newRoomForm, floor: Number(e.target.value) || 1 })} />
                </label>

                <label className="field">
                  <span>Diện tích (m²)</span>
                  <input type="number" value={newRoomForm.areaM2 ?? ''} onChange={e => setNewRoomForm({ ...newRoomForm, areaM2: e.target.value === '' ? null : Number(e.target.value) })} placeholder="VD: 25" />
                </label>

                <label className="field">
                  <span>Số khách tối đa</span>
                  <input type="number" value={newRoomForm.maxOccupants} onChange={e => setNewRoomForm({ ...newRoomForm, maxOccupants: Number(e.target.value) || 1 })} />
                </label>

                <label className="field checkbox-field" style={{ gridColumn: '1 / -1', display: 'flex', alignItems: 'center', gap: '8px', marginTop: '8px' }}>
                  <input type="checkbox" checked={newRoomForm.isTieredPricing} onChange={e => setNewRoomForm({ ...newRoomForm, isTieredPricing: e.target.checked })} style={{ width: '18px', height: '18px', margin: 0, cursor: 'pointer' }} />
                  <span style={{ fontSize: '14px', fontWeight: 600, color: '#475569' }}>Áp dụng giá thuê theo số lượng người ở (bảng giá thay đổi)</span>
                </label>

                <label className="field" style={{ gridColumn: '1 / -1' }}>
                  <span>Mô tả phòng</span>
                  <textarea style={{ width: '100%', minHeight: '80px', padding: '10px', border: '1px solid #cbd5e1', borderRadius: '6px', font: 'inherit' }} value={newRoomForm.description ?? ''} onChange={e => setNewRoomForm({ ...newRoomForm, description: e.target.value })} placeholder="Mô tả thêm về phòng..." />
                </label>

                <div className="save-row">
                  <button className="primary-action" onClick={handleCreateRoomSubmit}>Lưu thông tin</button>
                </div>
              </div>
            </div>
          )}

          {/* TAB 5: QUẢN LÝ PHÒNG */}
          {activeTab === 'service-prices' && (
            <ServicePriceEditorBatch
              serviceTypes={billingServiceTypes}
              prices={servicePrices}
              activePrices={activeServicePrices}
              priceHistory={servicePriceHistory}
              loading={servicePricesLoading}
              drafts={servicePriceDrafts}
              note={servicePriceNote}
              actionLoading={actionLoading}
              onChangeDraft={handleChangeServicePriceDraft}
              onChangeNote={setServicePriceNote}
              onSubmit={handleSaveServicePrices}
              onReload={loadServicePrices}
            />
          )}

          {activeTab === 'rooms' && (
            <div>


              {!house.rentalPolicy ? (
                <div className="empty-panel">
                  <h2>Chưa có chính sách cho thuê</h2>
                  <p>Vui lòng hoàn thành cấu hình chính sách cho thuê trước khi bắt đầu tạo phòng.</p>
                  <button className="primary-action" onClick={() => setActiveTab('rental-policy')}>
                    Cấu hình chính sách cho thuê
                  </button>
                </div>
              ) : !house.houseRule ? (
                <div className="empty-panel">
                  <h2>Chưa có Luật khu trọ</h2>
                  <p>Vui lòng hoàn thành Luật khu trọ trước khi bắt đầu tạo phòng.</p>
                  <button className="primary-action" onClick={() => setActiveTab('house-rule')}>
                    Cấu hình Luật khu trọ
                  </button>
                </div>
              ) : rooms.length === 0 ? (
                <div className="empty-panel">
                  <h2>Chưa có phòng nào</h2>
                  <p>Khu trọ của bạn chưa có phòng nào. Hãy tạo phòng mới bằng nút "Tạo phòng mới" ở góc phải phía trên.</p>
                </div>
              ) : (
                <div className="card-grid">
                  {rooms.map(room => (
                    <button key={room.id} className="dashboard-card" onClick={() => navigate(ROUTE_PATHS.LANDLORD.ROOM_DETAIL(id!, room.id))}>
                      <div className="card-header">
                        <h3>Phòng {room.roomNumber}</h3>
                        <span>Cập nhật: {formatDate(room.updatedAt)}</span>
                      </div>
                      <p style={{ margin: '8px 0 4px' }}>Tầng: {room.floor}</p>
                      <p style={{ margin: '0 0 8px' }}>
                        {room.areaM2 ? `${room.areaM2} m²` : 'Chưa nhập diện tích'} - Tối đa: {room.maxOccupants} người
                      </p>
                      <span className={`status-pill ${getStatusToneClass(room.status)}`}>
                        {formatStatus(room.status)}
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </main>
    </div>
  );
}

// ─── Subcomponents ──────────────────────────────────────────────────────────

function ServicePriceEditorBatch({
  serviceTypes,
  activePrices,
  priceHistory,
  loading,
  drafts,
  note,
  actionLoading,
  onChangeDraft,
  onChangeNote,
  onSubmit,
  onReload,
}: {
  serviceTypes: BillingServiceType[];
  prices: ServicePrice[];
  activePrices: ServicePrice[];
  priceHistory: ServicePrice[];
  loading: boolean;
  drafts: CreateServicePriceRequest[];
  note: string;
  actionLoading: boolean;
  onChangeDraft: (serviceTypeId: string, patch: Partial<CreateServicePriceRequest>) => void;
  onChangeNote: (note: string) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onReload: () => void;
}) {
  const activeByServiceTypeId = new Map(activePrices.map((price) => [price.serviceTypeId, price]));
  const historyGroups = buildServicePriceHistoryGroups(priceHistory, serviceTypes);

  return (
    <div className="editor-panel service-price-editor">
      <div className="service-price-header">
        <div>
          <h3>Giá dịch vụ của khu trọ</h3>
          <p>Chỉnh sửa tất cả rồi lưu một lần. Giá mới chỉ áp dụng từ đầu kỳ tiếp theo.</p>
        </div>
        <button type="button" className="secondary-action" onClick={onReload} disabled={loading}>
          Hoàn tác
        </button>
      </div>

      <form className="service-price-batch" onSubmit={onSubmit}>
        <div className="service-price-batch-table">
          <div className="service-price-batch-row service-price-batch-row--head">
            <span>Dịch vụ</span>
            <span>Giá hiện tại</span>
            <span>Cách tính phí</span>
            <span>Giá mới</span>
            <span>Từ ngày</span>
          </div>
          {serviceTypes.map((serviceType) => {
            const currentPrice = activeByServiceTypeId.get(serviceType.id);
            const defaultPricingUnit = getDefaultServicePricingUnit(serviceType);
            const draft = drafts.find((item) => item.serviceTypeId === serviceType.id) ?? {
              serviceTypeId: serviceType.id,
              serviceCode: serviceType.id,
              pricingUnit: defaultPricingUnit,
              billingMethod: defaultPricingUnit,
              unitName: getServicePricingUnitDisplayUnit(defaultPricingUnit, serviceType),
              unitPrice: 0,
              effectiveFrom: nextPeriodStart,
            };
            const methods = getServicePricingUnitOptions(serviceType);

            return (
              <div className="service-price-batch-row" key={serviceType.id}>
                <div className="service-price-service">
                  <span className={`service-price-icon service-price-icon--${getServiceTypeSlug(serviceType)}`}>{getServiceTypeIcon(serviceType)}</span>
                  <strong>{serviceType.name}</strong>
                </div>
                <div className="service-price-current">
                  <strong>{currentPrice ? `${formatMoneyString(currentPrice.unitPrice)} VND / ${currentPrice.unitName}` : 'Chưa có giá'}</strong>
                  <span>{currentPrice ? getBillingMethodLabel(currentPrice.billingMethod) : 'Chưa cấu hình'}</span>
                </div>
                <div className="service-price-methods">
                  {methods.length === 1 ? (
                    <span className="service-price-method-static">{getBillingMethodLabel(methods[0])}</span>
                  ) : (
                    <div className="service-price-segmented">
                      {methods.map((method) => (
                        <button
                          type="button"
                          key={method}
                          className={normalizeBillingMethod(draft.billingMethod ?? 'PerMonth') === method ? 'active' : ''}
                          onClick={() => onChangeDraft(serviceType.id, {
                            pricingUnit: method,
                            billingMethod: method,
                            unitName: getServicePricingUnitDisplayUnit(method, serviceType),
                          })}
                        >
                          {getBillingMethodShortLabel(method)}
                        </button>
                      ))}
                    </div>
                  )}
                  <small>{getServicePricingUnitHint(draft.billingMethod ?? 'PerMonth', serviceType)}</small>
                </div>
                <label className="service-price-inline-field">
                  <input
                    type="text"
                    value={formatMoneyString(draft.unitPrice)}
                    onChange={(event) => onChangeDraft(serviceType.id, { unitPrice: parseMoneyString(event.target.value) })}
                  />
                </label>
                <label className="service-price-inline-field">
                  <input type="date" value={nextPeriodStart} readOnly />
                </label>
              </div>
            );
          })}
        </div>

        <div className="service-price-batch-actions">
          <input
            value={note}
            onChange={(event) => onChangeNote(event.target.value)}
            placeholder="Ghi chú chung (ví dụ: điều chỉnh theo biểu giá tháng tới)"
          />
          <button type="submit" className="primary-action" disabled={actionLoading || serviceTypes.length === 0 || drafts.some((draft) => Number(draft.unitPrice) < 0)}>
            {actionLoading ? 'Đang lưu...' : 'Lưu tất cả'}
          </button>
        </div>
      </form>

      <section className="service-price-panel service-price-history">
        <div className="section-heading">
          <h4>Lịch sử thay đổi giá</h4>
          <span>{priceHistory.length} bản ghi</span>
        </div>
        {historyGroups.length === 0 ? (
          <div className="empty-panel compact">Chưa có lịch sử thay đổi giá.</div>
        ) : (
          <div className="service-price-history-groups">
            {historyGroups.map((group, index) => (
              <details className="service-price-history-group" key={group.key} open={index === 0}>
                <summary>
                  <span className="history-dot-row">
                    {group.serviceTypeIds.map((serviceTypeId) => (
                      <i key={serviceTypeId} className={`history-dot history-dot--${getHistoryServiceTypeSlug(serviceTypeId, serviceTypes)}`} />
                    ))}
                  </span>
                  <strong>{group.label}</strong>
                  <span>{group.items.length} dịch vụ</span>
                </summary>
                <div className="service-price-table service-price-table--monthly">
                  <div className="service-price-row service-price-row--head">
                    <span>Dịch vụ</span>
                    <span>Đơn giá</span>
                    <span>Đơn vị</span>
                    <span>Cách tính</span>
                    <span>Từ ngày</span>
                    <span>Đến ngày</span>
                  </div>
                  {group.items.map((price) => (
                    <div className="service-price-row" key={price.id}>
                      <span>{serviceTypes.find((serviceType) => serviceType.id === price.serviceTypeId)?.name ?? price.serviceName}</span>
                      <span>{formatMoneyString(price.unitPrice)} VND</span>
                      <span>{price.unitName}</span>
                      <span>{getBillingMethodLabel(price.billingMethod)}</span>
                      <span>{price.effectiveFrom}</span>
                      <span>{price.effectiveTo ?? 'Đang thay thế'}</span>
                    </div>
                  ))}
                </div>
              </details>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function ServicePriceEditor({
  activePrices,
  priceHistory,
  loading,
  form,
  actionLoading,
  onSelectService,
  onChangeForm,
  onSubmit,
  onReload,
}: {
  prices: ServicePrice[];
  activePrices: ServicePrice[];
  priceHistory: ServicePrice[];
  loading: boolean;
  form: CreateServicePriceRequest;
  actionLoading: boolean;
  onSelectService: (code: BillingServiceCode) => void;
  onChangeForm: (form: CreateServicePriceRequest) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onReload: () => void;
}) {
  const activeByCode = new Map(activePrices.map((price) => [price.serviceCode, price]));

  return (
    <div className="editor-panel service-price-editor">
      <div className="service-price-header">
        <div>
          <h3>Giá dịch vụ của khu trọ</h3>
          <p>Mỗi khu trọ có thể cấu hình giá điện, nước và các khoản cố định riêng.</p>
        </div>
        <button type="button" className="secondary-action" onClick={onReload} disabled={loading}>
          Làm mới
        </button>
      </div>

      <div className="service-price-grid">
        <section className="service-price-panel">
          <div className="section-heading">
            <h4>Giá đang áp dụng</h4>
            <span>{activePrices.length}/4 dịch vụ</span>
          </div>

          {loading ? (
            <div className="empty-panel compact">Đang tải bảng giá dịch vụ...</div>
          ) : (
            <div className="service-price-cards">
              {serviceOptions.map((service) => {
                const price = activeByCode.get(service.code);
                return (
                  <div className="service-price-card" key={service.code}>
                    <div>
                      <strong>{service.label}</strong>
                      <span>{price ? `${formatMoneyString(price.unitPrice)} VND / ${price.unitName}` : 'Chưa cấu hình'}</span>
                    </div>
                    <small>{price ? `Từ ${price.effectiveFrom}` : getBillingMethodLabel(service.method)}</small>
                  </div>
                );
              })}
            </div>
          )}
        </section>

        <section className="service-price-panel">
          <div className="section-heading">
            <h4>Cập nhật giá mới</h4>
          </div>

          <form className="service-price-form" onSubmit={onSubmit}>
            <label className="field">
              <span>Dịch vụ</span>
              <select value={form.serviceCode} onChange={(event) => onSelectService(event.target.value as BillingServiceCode)}>
                {serviceOptions.map((service) => (
                  <option key={service.code} value={service.code}>{service.label}</option>
                ))}
              </select>
            </label>

            <div className="form-grid">
              <label className="field">
                <span>Cách tính</span>
                <input value={getBillingMethodLabel(form.billingMethod ?? 'PerMonth')} readOnly />
              </label>
              <label className="field">
                <span>Đơn vị</span>
                <input value={form.unitName} onChange={(event) => onChangeForm({ ...form, unitName: event.target.value })} />
              </label>
            </div>

            <div className="form-grid">
              <label className="field">
                <span>Đơn giá (VND)</span>
                <input
                  type="text"
                  value={formatMoneyString(form.unitPrice)}
                  onChange={(event) => onChangeForm({ ...form, unitPrice: parseMoneyString(event.target.value) })}
                  placeholder="0"
                />
              </label>
              <label className="field">
                <span>Hiệu lực từ</span>
                <input type="date" value={form.effectiveFrom} onChange={(event) => onChangeForm({ ...form, effectiveFrom: event.target.value })} />
              </label>
            </div>

            <label className="field">
              <span>Ghi chú</span>
              <input value={form.note ?? ''} onChange={(event) => onChangeForm({ ...form, note: event.target.value })} placeholder="Ví dụ: điều chỉnh theo biểu giá tháng này" />
            </label>

            <div className="save-row">
              <button type="submit" className="primary-action" disabled={actionLoading || form.unitPrice <= 0}>
                {actionLoading ? 'Đang lưu...' : 'Lưu giá dịch vụ'}
              </button>
            </div>
          </form>
        </section>
      </div>

      <section className="service-price-panel service-price-history">
        <div className="section-heading">
          <h4>Lịch sử thay đổi giá</h4>
          <span>{priceHistory.length} bản ghi</span>
        </div>
        {priceHistory.length === 0 ? (
          <div className="empty-panel compact">Chưa có lịch sử thay đổi giá.</div>
        ) : (
          <div className="service-price-table">
            <div className="service-price-row service-price-row--head">
              <span>Dịch vụ</span>
              <span>Đơn giá</span>
              <span>Đơn vị</span>
              <span>Từ ngày</span>
              <span>Đến ngày</span>
            </div>
            {priceHistory.map((price) => (
              <div className="service-price-row" key={price.id}>
                <span>{serviceOptions.find((service) => service.code === price.serviceCode)?.label ?? price.serviceName}</span>
                <span>{formatMoneyString(price.unitPrice)} VND</span>
                <span>{price.unitName}</span>
                <span>{price.effectiveFrom}</span>
                <span>{price.effectiveTo ?? 'Đang thay thế'}</span>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function buildServicePriceHistoryGroups(prices: ServicePrice[], serviceTypes: BillingServiceType[] = []) {
  const formatter = new Intl.DateTimeFormat('vi-VN', { month: 'long', year: 'numeric' });
  const groups = new Map<string, ServicePrice[]>();
  const serviceTypeOrder = new Map(serviceTypes.map((serviceType, index) => [serviceType.id, index]));

  for (const price of prices) {
    const key = price.effectiveFrom.slice(0, 7);
    const items = groups.get(key) ?? [];
    groups.set(key, [...items, price]);
  }

  return Array.from(groups.entries())
    .sort(([a], [b]) => b.localeCompare(a))
    .map(([key, items]) => {
      const latestByService = new Map<string, ServicePrice>();
      for (const item of [...items].sort((a, b) =>
        b.effectiveFrom.localeCompare(a.effectiveFrom) ||
        b.updatedAt.localeCompare(a.updatedAt)
      )) {
        if (!latestByService.has(item.serviceTypeId)) {
          latestByService.set(item.serviceTypeId, item);
        }
      }

      const sortedItems = Array.from(latestByService.values())
        .sort((a, b) =>
          (serviceTypeOrder.get(a.serviceTypeId) ?? Number.MAX_SAFE_INTEGER) -
          (serviceTypeOrder.get(b.serviceTypeId) ?? Number.MAX_SAFE_INTEGER) ||
          a.serviceName.localeCompare(b.serviceName, 'vi')
        );
      const date = new Date(`${key}-01T00:00:00`);

      return {
        key,
        label: formatter.format(date),
        items: sortedItems,
        serviceTypeIds: sortedItems.map((item) => item.serviceTypeId),
      };
    });
}

function getNextPeriodStart() {
  const date = new Date();
  const nextPeriod = new Date(date.getFullYear(), date.getMonth() + 1, 1);
  const year = nextPeriod.getFullYear();
  const month = String(nextPeriod.getMonth() + 1).padStart(2, '0');
  return `${year}-${month}-01`;
}

function normalizeBillingMethod(method: BillingMethod): BillingMethod {
  if (method === 'Metered' || method === 'MeterReading') {
    return 'MeterBased';
  }

  if (method === 'Fixed') {
    return 'PerMonth';
  }

  if (method === 'PerPersonPerMonth') {
    return 'PerPerson';
  }

  return method;
}

function normalizePricingUnit(unit: PricingUnit | BillingMethod): PricingUnit {
  const normalized = normalizeBillingMethod(unit);

  if (normalized === 'MeterBased') {
    return 'MeterReading';
  }

  if (normalized === 'PerPerson') {
    return 'PerPersonPerMonth';
  }

  if (normalized === 'PerMonth') {
    return 'PerMonth';
  }

  return normalized;
}

function toApiBillingMethod(method: BillingMethod): BillingMethod {
  if (method === 'MeterBased') {
    return 'MeterReading';
  }

  if (method === 'Fixed' || method === 'PerMonth') {
    return 'PerMonth';
  }

  if (method === 'PerPerson' || method === 'PerPersonPerMonth') {
    return 'PerPersonPerMonth';
  }

  return method;
}

function getDefaultServicePricingUnit(serviceType: BillingServiceType): PricingUnit {
  return serviceType.supportsMeterReading ? 'MeterReading' : 'PerMonth';
}

function getServicePricingUnitOptions(serviceType: BillingServiceType): BillingMethod[] {
  return serviceType.supportsMeterReading
    ? ['MeterBased', 'PerMonth', 'PerPerson']
    : ['PerMonth', 'PerPerson'];
}

function getServicePricingUnitDisplayUnit(unit: PricingUnit | BillingMethod | string, serviceType: BillingServiceType) {
  const normalized = normalizeBillingMethod(unit as BillingMethod);

  if (normalized === 'PerPerson') {
    return 'người/tháng';
  }

  if (normalized === 'PerMonth') {
    return 'tháng';
  }

  return serviceType.meterUnitName ?? '';
}

function getServicePricingUnitHint(unit: PricingUnit | BillingMethod | string, serviceType: BillingServiceType) {
  const normalized = normalizeBillingMethod(unit as BillingMethod);

  if (normalized === 'PerPerson') {
    return 'VND / người / tháng';
  }

  if (normalized === 'PerMonth') {
    return 'VND / phòng / tháng';
  }

  return serviceType.meterUnitName ? `VND / ${serviceType.meterUnitName}` : 'VND / chỉ số';
}

function getServiceBillingMethods(serviceCode: BillingServiceCode): BillingMethod[] {
  if (serviceCode === 'Electric') {
    return ['MeterBased'];
  }

  if (serviceCode === 'Water') {
    return ['MeterBased', 'PerPerson', 'PerMonth'];
  }

  return ['PerMonth', 'PerPerson'];
}

function getServiceTypeSlug(serviceType: BillingServiceType) {
  return serviceType.name
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '') || 'service';
}

function getHistoryServiceTypeSlug(serviceTypeId: string, serviceTypes: BillingServiceType[]) {
  const serviceType = serviceTypes.find((item) => item.id === serviceTypeId);
  return serviceType ? getServiceTypeSlug(serviceType) : 'service';
}

function getServiceTypeIcon(serviceType: BillingServiceType) {
  const name = serviceType.name.trim();
  return name ? name.slice(0, 2).toUpperCase() : 'DV';
}

function getBillingMethodLabel(method: BillingMethod) {
  const normalized = normalizeBillingMethod(method);
  const labels: Record<BillingMethod, string> = {
    Metered: 'MeterBased',
    MeterBased: 'MeterBased',
    MeterReading: 'MeterBased',
    Fixed: 'Theo tháng',
    PerMonth: 'Theo tháng',
    PerPerson: 'Theo người/tháng',
    PerPersonPerMonth: 'Theo người/tháng',
  };

  return labels[normalized];
}

function getBillingMethodShortLabel(method: BillingMethod) {
  const normalized = normalizeBillingMethod(method);
  const labels: Record<BillingMethod, string> = {
    Metered: 'Chỉ số',
    MeterBased: 'Chỉ số',
    MeterReading: 'Chỉ số',
    Fixed: 'Tháng',
    PerMonth: 'Tháng',
    PerPerson: 'Người/tháng',
    PerPersonPerMonth: 'Người/tháng',
  };

  return labels[normalized];
}

function getUnitHint(method: BillingMethod) {
  const normalized = normalizeBillingMethod(method);
  if (normalized === 'PerPerson') {
    return 'VND / người / tháng';
  }

  if (normalized === 'PerMonth') {
    return 'VND / phòng / tháng';
  }

  return 'Theo số kWh/m3 tiêu thụ';
}

function getServiceIcon(serviceCode: BillingServiceCode) {
  const icons: Record<BillingServiceCode, string> = {
    Electric: 'E',
    Water: 'W',
    Wifi: 'Wi',
    Trash: 'R',
  };

  return icons[serviceCode];
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

// Icons
function HomeIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
      <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
      <polyline points="9 22 9 12 15 12 15 22" />
    </svg>
  );
}

function CheckCircleIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
      <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
      <polyline points="22 4 12 14.01 9 11.01" />
    </svg>
  );
}

function UserCheckIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
      <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
      <circle cx="8.5" cy="7" r="4" />
      <polyline points="17 11 19 13 23 9" />
    </svg>
  );
}

function EyeOffIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
      <line x1="1" y1="1" x2="23" y2="23" />
    </svg>
  );
}


