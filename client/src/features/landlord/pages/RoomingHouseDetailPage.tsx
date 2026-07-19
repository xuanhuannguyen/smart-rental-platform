import { useState, useEffect, useMemo, ReactNode, FormEvent } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Toast } from '../../../shared/components/ui/Toast';
import { Tabs } from '../../../shared/components/ui/Tabs';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { PrivateMediaImage } from '../../../shared/components/media/PrivateMediaImage';
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
import { ServicePriceEditorBatch, normalizePricingUnit, getDefaultServicePricingUnit, getServicePricingUnitDisplayUnit } from '../components/ServicePriceEditorBatch';
import RoomingHouseRuleEditor from '../../rooming-houses/components/RoomingHouseRuleEditor';
import { cleanImages, toImageRequests } from '../../rooming-houses/utils/imageRequests';
import './RoomingHouseDetailPage.css';

type MainTab = 'basic' | 'images' | 'amenities' | 'legal' | 'house-rule' | 'rental-policy' | 'service-prices' | 'rooms' | 'create-room';

function getLocalDateString(year: number, month: number, day: number): string {
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

function getNextMonth1st(): string {
  const now = new Date();
  const month = now.getMonth() + 1; // 1-based
  const nextMonth = month === 12 ? 1 : month + 1;
  const nextYear = month === 12 ? now.getFullYear() + 1 : now.getFullYear();
  return getLocalDateString(nextYear, nextMonth, 1);
}

function getCurrentMonth1st(): string {
  const now = new Date();
  return getLocalDateString(now.getFullYear(), now.getMonth() + 1, 1);
}

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
  const [toast, setToast] = useState<{ message: string, type: 'success' | 'error' } | null>(null);
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
  const [isFirstTimeServicePrice, setIsFirstTimeServicePrice] = useState(true);

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

      const isFirstTime = activePriceByServiceTypeId.size === 0;
      setIsFirstTimeServicePrice(isFirstTime);
      const effectiveFromDate = isFirstTime ? getCurrentMonth1st() : getNextMonth1st();

      setServicePriceDrafts(activeServiceTypes.map((serviceType) => {
        const activePrice = activePriceByServiceTypeId.get(serviceType.id);
        const pricingUnit = normalizePricingUnit(activePrice?.pricingUnit ?? getDefaultServicePricingUnit(serviceType));

        return {
          serviceTypeId: serviceType.id,
          pricingUnit,
          unitName: getServicePricingUnitDisplayUnit(pricingUnit, serviceType),
          unitPrice: activePrice?.unitPrice ?? 0,
          effectiveFrom: effectiveFromDate,
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

        const pricingUnit = normalizePricingUnit(patch.pricingUnit ?? draft.pricingUnit ?? 'PerMonth');

        return {
          ...draft,
          ...patch,
          pricingUnit,
          effectiveFrom: isFirstTimeServicePrice ? getCurrentMonth1st() : getNextMonth1st(),
        };
      })
    );
  }

  async function handleSaveServicePrices(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!house) return;

    setActionLoading(true);
    setToast(null)
    try {
      for (const draft of servicePriceDrafts) {
        await billingApi.createServicePrice(house.id, {
          ...draft,
          unitPrice: Number(draft.unitPrice) || 0,
          effectiveFrom: isFirstTimeServicePrice ? getCurrentMonth1st() : getNextMonth1st(),
          note: servicePriceNote.trim() || null,
        });
      }

      await loadServicePrices();
      setToast({ message: 'Đã lưu tất cả giá dịch vụ thành công.', type: 'success' })
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể lưu giá dịch vụ.'), type: 'error' })
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
    setToast(null)
    try {
      const data = await getRoomingHouseDetail(id!);

      // Chỉ cho phép khu trọ đã duyệt vào trang này
      if (data.approvalStatus !== 'Approved') {
        setToast({ message: 'Khu trọ này chưa được quản trị viên phê duyệt. Không thể truy cập quản lý phòng.', type: 'error' })
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
      }

      try {
        const roomsData = await getRoomsByRoomingHouse(id!);
        setRooms(roomsData);
      } catch (err) {
        console.warn('Lỗi tải phòng:', err);
        setRooms([]);
      }
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể tải thông tin chi tiết khu trọ.'), type: 'error' })
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
    setToast(null)
    try {
      const updated = await updateRoomingHouseBasicInfo(house.id, basicForm);
      setHouse(updated);
      setToast({ message: 'Đã cập nhật thông tin cơ bản khu trọ thành công.', type: 'success' })
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể cập nhật thông tin cơ bản.'), type: 'error' })
    } finally {
      setActionLoading(false);
    }
  }

  // ─── Tab 2: Lưu ảnh khu trọ ──────────────────────────────────────────────
  async function handleToggleVisibility() {
    if (!house) return;

    const nextVisibility = house.visibilityStatus === 'Visible' ? 'Hidden' : 'Visible';
    setActionLoading(true);
    setToast(null)
    try {
      const updated = await updateRoomingHouseVisibility(house.id, nextVisibility);
      setHouse(updated);
      setToast({
        message: nextVisibility === 'Hidden'
          ? 'Khu trọ đã được ẩn khỏi trang công khai.'
          : 'Khu trọ đã được hiển thị công khai.', type: 'success'
      });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể cập nhật trạng thái hiển thị khu trọ.'), type: 'error' })
    } finally {
      setActionLoading(false);
    }
  }

  async function handleSaveImages() {
    if (!house) return;
    setActionLoading(true);
    setToast(null)
    try {
      const updated = await updateRoomingHouseImages(house.id, cleanImages(houseImages));
      setHouse(updated);
      setHouseImages(toImageRequests(updated.images));
      setToast({ message: 'Đã lưu ảnh minh họa khu trọ thành công.', type: 'success' })
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể cập nhật ảnh khu trọ.'), type: 'error' })
    } finally {
      setActionLoading(false);
    }
  }

  // ─── Tab 3: Lưu tiện ích khu trọ ─────────────────────────────────────────
  async function handleSaveAmenities() {
    if (!house) return;
    setActionLoading(true);
    setToast(null)
    try {
      const updated = await updateRoomingHouseAmenities(house.id, selectedAmenityIds);
      setHouse(updated);
      setSelectedAmenityIds(updated.amenities.map(a => a.id));
      setToast({ message: 'Đã cập nhật tiện ích khu trọ thành công.', type: 'success' })
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể cập nhật tiện ích khu trọ.'), type: 'error' })
    } finally {
      setActionLoading(false);
    }
  }

  // ─── Tab 4: Lưu chính sách thuê khu trọ ────────────────────────────────────
  async function handleSaveRentalPolicy() {
    if (!house) return;
    setActionLoading(true);
    setToast(null)
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
      setToast({ message: 'Đã cập nhật chính sách thuê thành công.', type: 'success' })
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể cập nhật chính sách thuê.'), type: 'error' })
    } finally {
      setActionLoading(false);
    }
  }

  function handleHouseRuleSaved(savedRule: NonNullable<RoomingHouseDetail['houseRule']>) {
    setHouse((current) => current ? { ...current, houseRule: savedRule } : current);
    setToast({ message: 'Đã cập nhật luật khu trọ thành công.', type: 'success' })
  }



  function handleCreateRoomClick() {
    if (!house?.rentalPolicy) {
      setToast({ message: 'Vui lòng hoàn thành chính sách cho thuê trước khi tạo phòng.', type: 'error' })
      setActiveTab('rental-policy');
      return;
    }

    if (!house.houseRule) {
      setToast({ message: 'Vui lòng hoàn thành Luật khu trọ trước khi tạo phòng đầu tiên.', type: 'error' })
      setActiveTab('house-rule');
      return;
    }

    const hasPrices = activeServicePrices.length > 0;
    if (!hasPrices) {
      setToast({ message: 'Vui lòng cấu hình bảng giá dịch vụ trước khi tạo phòng.', type: 'error' })
      setActiveTab('service-prices');
      return;
    }
    setActiveTab('create-room');
    setNewRoomForm(emptyRoomForm);
  }

  async function handleCreateRoomSubmit() {
    if (!house) return;
    setActionLoading(true);
    setToast(null)
    try {
      const createdRoom = await createRoom(house.id, newRoomForm);
      setToast({ message: 'Tạo phòng mới thành công. Hãy tiếp tục cập nhật các thông tin khác.', type: 'success' })
      setNewRoomForm(emptyRoomForm);
      // Chuyển hướng sang trang chi tiết phòng vừa tạo để có thể edit Ảnh, Tiện ích, Bảng giá
      navigate(ROUTE_PATHS.LANDLORD.ROOM_DETAIL(house.id, createdRoom.id), { state: { initialTab: 'images' } });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể tạo phòng.'), type: 'error' })
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
            <p>{toast?.message || 'Không thể truy cập thông tin khu trọ này.'}</p>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="rooming-house-detail-page" style={{ display: 'contents' }}>
      <main className="dashboard-main">
        {/* Banner Tổng quan */}
        <PageHeader
          className="page-header-band--flat-bottom"
          onBack={() => navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSES)}
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#2563eb" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
              <polyline points="9 22 9 12 15 12 15 22" />
              <rect x="10" y="14" width="4" height="4" />
            </svg>
          }
          eyebrow={
            <div className="overview-address" style={{ display: 'flex', alignItems: 'center', color: '#2563eb', fontWeight: 700, fontSize: '12px', textTransform: 'uppercase', marginBottom: '4px' }}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#2563eb" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }}>
                <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                <circle cx="12" cy="10" r="3" />
              </svg>
              <span>{house.addressDisplay}</span>
            </div>
          }
          title={house.name}
          description={
            <div className="overview-description" style={{ display: 'flex', alignItems: 'center', margin: 0 }}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#2563eb" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }}>
                <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                <line x1="16" y1="2" x2="16" y2="6" />
                <line x1="8" y1="2" x2="8" y2="6" />
                <line x1="3" y1="10" x2="21" y2="10" />
              </svg>
              Thời gian duyệt: <span style={{ color: '#2563eb', fontWeight: 600, marginLeft: '4px' }}>{house.createdAt ? formatDate(house.createdAt) : ''}</span>
            </div>
          }
          rightContent={
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
                {house.approvalStatus === 'Approved' && (
                  <button
                    className="action-btn-outline"
                    onClick={handleToggleVisibility}
                    disabled={actionLoading}
                  >
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                      <circle cx="12" cy="12" r="3" />
                    </svg>
                    {house.visibilityStatus === 'Visible' ? 'Ẩn khu trọ' : 'Hiển thị khu trọ'}
                  </button>
                )}
                <button
                  className="action-btn-primary"
                  onClick={handleCreateRoomClick}
                  disabled={!canCreateRoom}
                  title={!canCreateRoom ? "Vui lòng cấu hình chính sách thuê và Luật khu trọ trước khi tạo phòng." : "Tạo phòng mới"}
                  style={!canCreateRoom ? { opacity: 0.5, cursor: 'not-allowed' } : undefined}
                >
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <line x1="12" y1="5" x2="12" y2="19" />
                    <line x1="5" y1="12" x2="19" y2="12" />
                  </svg>
                  Tạo phòng mới
                </button>
              </div>
            </div>
          }
        />

        {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
        {actionLoading && <p className="dashboard-message" style={{ background: '#dbeafe', color: '#1e40af' }}>Đang lưu thay đổi...</p>}



        {/* Hệ thống Tab Cấp 1 (Main Tabs) */}
        <div style={{ marginBottom: '32px' }}>
          <Tabs
            className="attached-top"
            variant="segmented-primary"
            activeId={(activeTab === 'rooms' || activeTab === 'create-room') ? 'rooms' : 'house_info'}
            onChange={(id) => {
              if (id === 'rooms') setActiveTab('rooms');
              else if (id === 'house_info') setActiveTab('basic');
            }}
            items={[
              { id: 'rooms', label: 'Danh sách phòng', icon: <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="8" y1="6" x2="21" y2="6" /><line x1="8" y1="12" x2="21" y2="12" /><line x1="8" y1="18" x2="21" y2="18" /><line x1="3" y1="6" x2="3.01" y2="6" /><line x1="3" y1="12" x2="3.01" y2="12" /><line x1="3" y1="18" x2="3.01" y2="18" /></svg> },
              { id: 'house_info', label: 'Thông tin khu trọ', icon: <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" /><polyline points="9 22 9 12 15 12 15 22" /><rect x="10" y="14" width="4" height="4" /></svg> }
            ]}
          />
        </div>

        {/* Nội dung Tab */}
        <div className="tab-content" style={{ marginTop: '24px' }}>
          {/* Hệ thống Tab Cấp 2 (Sub Tabs) */}
          {(activeTab !== 'rooms' && activeTab !== 'create-room') && (
            <div>
              <Tabs
                className="attached-bottom"
                variant="segmented-secondary"
                activeId={activeTab}
                onChange={(id) => setActiveTab(id as any)}
                items={[
                  { id: 'basic', label: 'Cơ bản', icon: <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" /><polyline points="9 22 9 12 15 12 15 22" /></svg> },
                  { id: 'images', label: 'Ảnh', icon: <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="3" width="18" height="18" rx="2" ry="2" /><circle cx="8.5" cy="8.5" r="1.5" /><polyline points="21 15 16 10 5 21" /></svg> },
                  { id: 'amenities', label: 'Tiện ích', icon: <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" /></svg> },
                  { id: 'legal', label: 'Pháp lý', icon: <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><polyline points="14 2 14 8 20 8" /><line x1="16" y1="13" x2="8" y2="13" /><line x1="16" y1="17" x2="8" y2="17" /><polyline points="10 9 9 9 8 9" /></svg> },
                  { id: 'house-rule', label: 'Luật khu trọ', icon: <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><polyline points="14 2 14 8 20 8" /><line x1="16" y1="13" x2="8" y2="13" /><line x1="16" y1="17" x2="8" y2="17" /></svg> },
                  { id: 'rental-policy', label: 'Quy định thuê', icon: <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="4" width="18" height="18" rx="2" ry="2" /><line x1="16" y1="2" x2="16" y2="6" /><line x1="8" y1="2" x2="8" y2="6" /><line x1="3" y1="10" x2="21" y2="10" /></svg> },
                  { id: 'service-prices', label: 'Bảng giá dịch vụ', icon: <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="1" x2="12" y2="23" /><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" /></svg> }
                ]}
              />
            </div>
          )}
          {/* TAB 1: THÔNG TIN CƠ BẢN */}
          {activeTab === 'basic' && (
            <div className="subtab-card tab-attached-panel tab-attached-panel--compact">
              <div className="subtab-header">
                <div className="subtab-header-icon">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                    <polyline points="9 22 9 12 15 12 15 22" />
                  </svg>
                </div>
                <div>
                  <h4>Thông tin cơ bản</h4>
                  <p>Quản lý các thông tin hành chính, địa chỉ và mô tả chung của khu trọ.</p>
                </div>
              </div>

              {addressLocked && (
                <div className="subtab-alert" style={{ marginBottom: '20px' }}>
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="12" cy="12" r="10" />
                    <line x1="12" y1="16" x2="12" y2="12" />
                    <line x1="12" y1="8" x2="12.01" y2="8" />
                  </svg>
                  <span>Địa chỉ hành chính của khu trọ đã được duyệt nên không thể chỉnh sửa. Bạn vẫn có thể cập nhật tọa độ và link Google Maps.</span>
                </div>
              )}

              <div className="form-grid">
                <label className="field">
                  <span>
                    Tên khu trọ
                    <span className="label-info-icon" title="Tên hiển thị công khai của khu trọ">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" /></svg>
                    </span>
                  </span>
                  <input value={basicForm.name} onChange={e => setBasicForm({ ...basicForm, name: e.target.value })} placeholder="Nhập tên khu trọ..." />
                  <span className="helper-text">Tên khu trọ hiển thị với khách thuê</span>
                </label>

                <label className="field">
                  <span>
                    Địa chỉ chi tiết
                    <span className="label-info-icon" title="Địa chỉ số nhà, tên đường">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" /></svg>
                    </span>
                  </span>
                  <input disabled={addressLocked} value={basicForm.addressLine} onChange={e => setBasicForm({ ...basicForm, addressLine: e.target.value })} placeholder="VD: 144 Trần Đại Nghĩa" />
                  <span className="helper-text">Số nhà, ngõ/hẻm, tên đường</span>
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
                  <span className="helper-text">Chọn Tỉnh/Thành phố trực thuộc</span>
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
                  <span className="helper-text">Chọn Phường/Xã hành chính</span>
                </label>

                <div style={{ gridColumn: '1 / -1', marginTop: '10px' }}>
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
                </div>

                <label className="field" style={{ gridColumn: '1 / -1' }}>
                  <span>Mô tả khu trọ</span>
                  <textarea
                    value={basicForm.description ?? ''}
                    onChange={e => setBasicForm({ ...basicForm, description: e.target.value })}
                    placeholder="Mô tả các đặc điểm nổi bật của khu trọ..."
                  />
                  <span className="helper-text">Thông tin chi tiết giới thiệu về khu trọ tới khách thuê</span>
                </label>
              </div>

              <div className="subtab-footer">
                <div className="shield-info">
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                  </svg>
                  <span>Thông tin này giúp khách thuê tìm kiếm khu trọ trên bản đồ chính xác.</span>
                </div>
                <button className="primary-action" onClick={handleSaveBasicInfo} disabled={actionLoading}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }}>
                    <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                    <polyline points="17 21 17 13 7 13 7 21" />
                    <polyline points="7 3 7 8 15 8" />
                  </svg>
                  {actionLoading ? 'Đang lưu...' : 'Lưu thông tin cơ bản'}
                </button>
              </div>
            </div>
          )}

          {/* TAB 2: ẢNH KHU TRỌ */}
          {activeTab === 'images' && (
            <div className="subtab-card tab-attached-panel tab-attached-panel--compact">
              <div className="subtab-header">
                <div className="subtab-header-icon">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                    <circle cx="8.5" cy="8.5" r="1.5" />
                    <polyline points="21 15 16 10 5 21" />
                  </svg>
                </div>
                <div>
                  <h4>Ảnh khu trọ</h4>
                  <p>Quản lý hình ảnh đại diện, không gian chung và chi tiết của khu trọ.</p>
                </div>
              </div>
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
            <div className="subtab-card tab-attached-panel tab-attached-panel--compact">
              <div className="subtab-header">
                <div className="subtab-header-icon">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />
                  </svg>
                </div>
                <div>
                  <h4>Tiện nghi khu trọ</h4>
                  <p>Lựa chọn các tiện ích, dịch vụ chung được cung cấp tại khu trọ này.</p>
                </div>
              </div>
              <AmenityEditor
                amenities={houseAmenities}
                selectedIds={selectedAmenityIds}
                onChange={setSelectedAmenityIds}
                onSave={handleSaveAmenities}
                actionLoading={actionLoading}
              />
            </div>
          )}

          {/* TAB 4: GIẤY TỜ PHÁP LÝ (READ ONLY) */}
          {activeTab === 'legal' && (
            <div className="subtab-card tab-attached-panel tab-attached-panel--compact">
              <div className="subtab-header">
                <div className="subtab-header-icon">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="16" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="4" />
                    <line x1="8" y1="2" x2="8" y2="4" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                </div>
                <div>
                  <h4>Giấy tờ pháp lý</h4>
                  <p>Các giấy tờ chứng minh quyền sở hữu và vận hành khu trọ hợp pháp.</p>
                </div>
              </div>

              <div className="subtab-alert" style={{ marginBottom: '20px' }}>
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="16" x2="12" y2="12" />
                  <line x1="12" y1="8" x2="12.01" y2="8" />
                </svg>
                <span>Giấy tờ pháp lý của khu trọ đã được ban quản trị duyệt và khóa. Bạn không thể tự ý chỉnh sửa thông tin này.</span>
              </div>

              <div className="legal-info-list" style={{ marginBottom: '24px' }}>
                <div className="legal-info-item">
                  <span className="legal-info-label">Loại giấy tờ pháp lý</span>
                  <span className="legal-info-value">{house.legalDocument?.documentType ?? 'Không xác định'}</span>
                </div>

                <div className="legal-info-item">
                  <span className="legal-info-label">Số giấy tờ (Đã ẩn)</span>
                  <span className="legal-info-value">{house.legalDocument?.documentNumberMasked ?? 'Không xác định'}</span>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '20px' }}>
                <div>
                  <h4 style={{ fontSize: '14px', fontWeight: 600, color: '#334155', marginBottom: '10px' }}>Mặt trước giấy tờ</h4>
                  {house.legalDocument?.frontImageUrl ? (
                    <PrivateMediaImage
                      source={house.legalDocument.frontImageUrl}
                      alt="Front"
                      style={{ width: '100%', maxHeight: '240px', objectFit: 'contain', border: '1px solid #e2e8f0', borderRadius: '12px', background: '#f8fafc', padding: '6px' }}
                    />
                  ) : (
                    <p style={{ color: '#94a3b8', fontStyle: 'italic', fontSize: '13.5px' }}>Chưa tải lên ảnh</p>
                  )}
                </div>

                <div>
                  <h4 style={{ fontSize: '14px', fontWeight: 600, color: '#334155', marginBottom: '10px' }}>Mặt sau giấy tờ</h4>
                  {house.legalDocument?.backImageUrl ? (
                    <PrivateMediaImage
                      source={house.legalDocument.backImageUrl}
                      alt="Back"
                      style={{ width: '100%', maxHeight: '240px', objectFit: 'contain', border: '1px solid #e2e8f0', borderRadius: '12px', background: '#f8fafc', padding: '6px' }}
                    />
                  ) : (
                    <p style={{ color: '#94a3b8', fontStyle: 'italic', fontSize: '13.5px' }}>Chưa tải lên ảnh</p>
                  )}
                </div>

                {house.legalDocument?.extraImageUrl && (
                  <div>
                    <h4 style={{ fontSize: '14px', fontWeight: 600, color: '#334155', marginBottom: '10px' }}>Ảnh bổ sung</h4>
                    <PrivateMediaImage
                      source={house.legalDocument.extraImageUrl}
                      alt="Extra"
                      style={{ width: '100%', maxHeight: '240px', objectFit: 'contain', border: '1px solid #e2e8f0', borderRadius: '12px', background: '#f8fafc', padding: '6px' }}
                    />
                  </div>
                )}
              </div>
            </div>
          )}

          {/* TAB 4.5: LUẬT KHU TRỌ */}
          {activeTab === 'house-rule' && (
            <div className="subtab-card tab-attached-panel tab-attached-panel--compact">
              <div className="subtab-header">
                <div className="subtab-header-icon">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                    <polyline points="14 2 14 8 20 8" />
                    <line x1="16" y1="13" x2="8" y2="13" />
                    <line x1="16" y1="17" x2="8" y2="17" />
                  </svg>
                </div>
                <div>
                  <h4>Luật & Quy định khu trọ</h4>
                  <p>Thiết lập nội quy bắt buộc đối với tất cả khách thuê nhằm bảo đảm an ninh trật tự.</p>
                </div>
              </div>
              <RoomingHouseRuleEditor
                roomingHouseId={house.id}
                houseRule={house.houseRule}
                onSaved={handleHouseRuleSaved}
              />
            </div>
          )}

          {/* TAB 4.5: CHÍNH SÁCH THUÊ */}
          {activeTab === 'rental-policy' && (
            <div className="subtab-card tab-attached-panel tab-attached-panel--compact">
              <div className="subtab-header">
                <div className="subtab-header-icon">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                </div>
                <div>
                  <h4>Chính sách thuê</h4>
                  <p>Thiết lập thời hạn hợp đồng, tiền cọc và chu kỳ thanh toán định kỳ của khu trọ.</p>
                </div>
              </div>

              <div className="form-grid" style={{ marginBottom: '20px' }}>
                <label className="field">
                  <span>
                    Số tháng thuê tối thiểu
                    <span className="label-info-icon" title="Thời gian hợp đồng thuê tối thiểu khách phải ký">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" /></svg>
                    </span>
                  </span>
                  <div className="input-wrapper">
                    <input
                      type="number"
                      min="1"
                      value={rentalPolicyForm.minRentalMonths}
                      onChange={(e) =>
                        setRentalPolicyForm({ ...rentalPolicyForm, minRentalMonths: Number(e.target.value) || 1 })
                      }
                    />
                    <span className="input-suffix">tháng</span>
                  </div>
                  <span className="helper-text">Thời hạn hợp đồng tối thiểu</span>
                </label>

                <label className="field">
                  <span>
                    Số tháng thuê tối đa
                    <span className="label-info-icon" title="Thời gian hợp đồng tối đa cho mỗi lần ký">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" /></svg>
                    </span>
                  </span>
                  <div className="input-wrapper">
                    <input
                      type="number"
                      min="1"
                      value={rentalPolicyForm.maxRentalMonths}
                      onChange={(e) =>
                        setRentalPolicyForm({ ...rentalPolicyForm, maxRentalMonths: Number(e.target.value) || 1 })
                      }
                    />
                    <span className="input-suffix">tháng</span>
                  </div>
                  <span className="helper-text">Thời hạn hợp đồng tối đa</span>
                </label>

                <label className="field">
                  <span>
                    Số ngày báo trước khi gia hạn
                    <span className="label-info-icon" title="Khách thuê cần báo trước số ngày này nếu muốn dọn đi hoặc gia hạn">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" /></svg>
                    </span>
                  </span>
                  <div className="input-wrapper">
                    <input
                      type="number"
                      min="0"
                      value={rentalPolicyForm.renewalNoticeDays}
                      onChange={(e) =>
                        setRentalPolicyForm({ ...rentalPolicyForm, renewalNoticeDays: Number(e.target.value) || 0 })
                      }
                    />
                    <span className="input-suffix">ngày</span>
                  </div>
                  <span className="helper-text">Hạn báo trước khi hết hợp đồng</span>
                </label>

                <label className="field">
                  <span>
                    Số tháng tiền cọc
                    <span className="label-info-icon" title="Số tháng tiền phòng khách cần đặt cọc trước khi dọn vào">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" /></svg>
                    </span>
                  </span>
                  <div className="input-wrapper">
                    <input
                      type="number"
                      min="0"
                      value={rentalPolicyForm.depositMonths}
                      onChange={(e) =>
                        setRentalPolicyForm({ ...rentalPolicyForm, depositMonths: Number(e.target.value) || 0 })
                      }
                    />
                    <span className="input-suffix">tháng</span>
                  </div>
                  <span className="helper-text">Khoản tiền cọc đảm bảo hợp đồng</span>
                </label>

                <label className="field">
                  <span>
                    Ngày thanh toán mặc định
                    <span className="label-info-icon" title="Ngày thanh toán tiền phòng cố định hàng tháng">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" /></svg>
                    </span>
                  </span>
                  <div className="input-wrapper">
                    <input
                      type="number"
                      min="1"
                      max="28"
                      value={rentalPolicyForm.defaultPaymentDay}
                      onChange={(e) =>
                        setRentalPolicyForm({ ...rentalPolicyForm, defaultPaymentDay: Number(e.target.value) || 5 })
                      }
                    />
                    <span className="input-suffix">hàng tháng</span>
                  </div>
                  <span className="helper-text">Hạn thanh toán hóa đơn định kỳ (từ ngày 1 đến ngày 28)</span>
                </label>

                <div style={{ gridColumn: '1 / -1', marginTop: '10px' }}>
                  <div className="checkbox-card-panel">
                    <div className="checkbox-card-left">
                      <input
                        type="checkbox"
                        className="checkbox-card-input"
                        checked={rentalPolicyForm.allowShortTermRenewal}
                        onChange={(e) =>
                          setRentalPolicyForm({ ...rentalPolicyForm, allowShortTermRenewal: e.target.checked })
                        }
                      />
                      <div className="checkbox-card-text">
                        <span className="checkbox-card-title">Cho phép gia hạn hợp đồng ngắn hạn</span>
                        <span className="checkbox-card-desc">Hỗ trợ khách thuê ký tiếp hợp đồng ngắn hạn (dưới 6 tháng) khi sắp hết hạn</span>
                      </div>
                    </div>
                    <div className="checkbox-card-icon">
                      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                        <line x1="16" y1="2" x2="16" y2="6" />
                        <line x1="8" y1="2" x2="8" y2="6" />
                        <line x1="3" y1="10" x2="21" y2="10" />
                      </svg>
                    </div>
                  </div>
                </div>
              </div>

              <div className="subtab-footer">
                <div className="shield-info">
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                  </svg>
                  <span>Chính sách thuê rõ ràng giúp giảm thiểu các tranh chấp không đáng có.</span>
                </div>
                <button className="primary-action" onClick={handleSaveRentalPolicy} disabled={actionLoading}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }}>
                    <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                    <polyline points="17 21 17 13 7 13 7 21" />
                    <polyline points="7 3 7 8 15 8" />
                  </svg>
                  {actionLoading ? 'Đang lưu...' : 'Lưu chính sách thuê'}
                </button>
              </div>
            </div>
          )}

          {/* TAB: TẠO PHÒNG MỚI */}
          {activeTab === 'create-room' && (
            <>
              <Tabs
                  className="attached-bottom"
                  variant="segmented-secondary"
                  activeId="basic"
                  onChange={() => undefined}
                  items={[
                    {
                      id: 'basic',
                      label: 'Thông tin cơ bản',
                      icon: (
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                          <polyline points="14 2 14 8 20 8" />
                          <line x1="16" y1="13" x2="8" y2="13" />
                          <line x1="16" y1="17" x2="8" y2="17" />
                        </svg>
                      ),
                    },
                    {
                      id: 'images',
                      label: 'Ảnh phòng',
                      disabled: true,
                      title: 'Tạo phòng trước khi cập nhật ảnh phòng',
                      icon: (
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                          <circle cx="8.5" cy="8.5" r="1.5" />
                          <polyline points="21 15 16 10 5 21" />
                        </svg>
                      ),
                    },
                    {
                      id: 'amenities',
                      label: 'Tiện ích phòng',
                      disabled: true,
                      title: 'Tạo phòng trước khi cập nhật tiện ích phòng',
                      icon: (
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <rect x="3" y="3" width="7" height="7" />
                          <rect x="14" y="3" width="7" height="7" />
                          <rect x="14" y="14" width="7" height="7" />
                          <rect x="3" y="14" width="7" height="7" />
                        </svg>
                      ),
                    },
                    {
                      id: 'price',
                      label: 'Bảng giá',
                      disabled: true,
                      title: 'Tạo phòng trước khi cập nhật bảng giá',
                      icon: (
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z" />
                          <line x1="7" y1="7" x2="7.01" y2="7" strokeWidth="2.5" />
                        </svg>
                      ),
                    },
                  ]}
              />

              <div className="editor-panel tab-attached-panel tab-attached-panel--compact" style={{ marginTop: 0, background: '#f8fafc', border: '1px solid #cbd5e1' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                  <h3 style={{ margin: 0, fontSize: '18px', color: '#1e293b' }}>Tạo phòng mới</h3>
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
            </>
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
              effectiveFromDate={isFirstTimeServicePrice ? getCurrentMonth1st() : getNextMonth1st()}
              onChangeDraft={handleChangeServicePriceDraft}
              onChangeNote={setServicePriceNote}
              onSubmit={handleSaveServicePrices}
              onReload={loadServicePrices}
            />
          )}

          {activeTab === 'rooms' && (
            <div>


              {!house.rentalPolicy ? (
                <div className="empty-panel-container" style={{ display: 'grid', gap: '16px' }}>
                  <div className="rooming-house-rule-editor__alert rooming-house-rule-editor__alert--warning">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                      <line x1="12" y1="9" x2="12" y2="13" />
                      <line x1="12" y1="17" x2="12.01" y2="17" />
                    </svg>
                    <span>Vui lòng hoàn thành Chính Sách Thuê ở phần thông tin khu trọ để được tạo phòng.</span>
                  </div>

                  <div className="empty-panel">
                    <svg width="240" height="180" viewBox="0 0 240 180" fill="none" xmlns="http://www.w3.org/2000/svg" style={{ marginBottom: '16px' }}>
                      <path d="M190 120c0-5.5-4.5-10-10-10h-5c-6.6 0-12 5.4-12 12s5.4 12 12 12h15c5.5 0 10-4.5 10-10z" fill="#f1f5f9" opacity="0.6" />
                      <path d="M50 110c0-6.6 5.4-12 12-12h8c8.8 0 16 7.2 16 16s-7.2 16-16 16h-8c-6.6 0-12-5.4-12-12z" fill="#f1f5f9" opacity="0.6" />
                      <path d="M120 160h60" stroke="#eff6ff" strokeWidth="6" strokeLinecap="round" />
                      <path d="M60 160h120" stroke="#3b82f6" strokeWidth="3" strokeLinecap="round" />
                      <rect x="94" y="24" width="72" height="96" rx="8" fill="#e2e8f0" />
                      <rect x="90" y="20" width="72" height="96" rx="8" fill="#ffffff" stroke="#cbd5e1" strokeWidth="2" />
                      <rect x="110" y="52" width="40" height="4" rx="2" fill="#e2e8f0" />
                      <rect x="110" y="68" width="40" height="4" rx="2" fill="#cbd5e1" />
                      <rect x="110" y="84" width="30" height="4" rx="2" fill="#e2e8f0" />
                      <rect x="100" y="50" width="8" height="8" rx="2" fill="#eff6ff" stroke="#3b82f6" strokeWidth="1.5" />
                      <path d="M102 54l1.5 1.5 3-3" stroke="#3b82f6" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                      <rect x="100" y="66" width="8" height="8" rx="2" fill="#eff6ff" stroke="#3b82f6" strokeWidth="1.5" />
                      <path d="M102 70l1.5 1.5 3-3" stroke="#3b82f6" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                      <rect x="100" y="82" width="8" height="8" rx="2" fill="#ffffff" stroke="#cbd5e1" strokeWidth="1.5" />
                      <rect x="112" y="12" width="28" height="12" rx="4" fill="#64748b" />
                      <circle cx="126" cy="18" r="3" fill="#cbd5e1" />
                      <path d="M60 80c0 15 15 25 25 30 10-5 25-15 25-30V60l-25-10-25 10v20z" fill="#3b82f6" />
                      <path d="M63 80c0 13 13 22 22 26V53l-22 9v18z" fill="#2563eb" />
                      <path d="M75 80l4 4 8-8" stroke="#ffffff" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
                      <circle cx="176" cy="74" r="10" stroke="#3b82f6" strokeWidth="3.5" fill="#ffffff" />
                      <circle cx="176" cy="74" r="3" fill="#3b82f6" />
                      <path d="M169 81l-14 14" stroke="#3b82f6" strokeWidth="4.5" strokeLinecap="round" />
                      <path d="M158 92l2 2" stroke="#3b82f6" strokeWidth="3.5" strokeLinecap="round" />
                      <path d="M161 89l2 2" stroke="#3b82f6" strokeWidth="3.5" strokeLinecap="round" />
                    </svg>

                    <h2>Chưa có chính sách cho thuê</h2>
                    <p>Vui lòng hoàn thành cấu hình chính sách cho thuê trước khi bắt đầu tạo phòng.</p>
                    <button className="action-btn-primary" onClick={() => setActiveTab('rental-policy')} style={{ padding: '8px 20px', minHeight: '40px' }}>
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '8px' }}>
                        <circle cx="12" cy="12" r="3" />
                        <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
                      </svg>
                      Cấu hình chính sách cho thuê
                    </button>
                  </div>
                </div>
              ) : !house.houseRule ? (
                <div className="empty-panel-container" style={{ display: 'grid', gap: '16px' }}>
                  <div className="rooming-house-rule-editor__alert rooming-house-rule-editor__alert--warning">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                      <line x1="12" y1="9" x2="12" y2="13" />
                      <line x1="12" y1="17" x2="12.01" y2="17" />
                    </svg>
                    <span>Vui lòng hoàn thành Luật khu trọ ở phần thông tin khu trọ để được tạo phòng.</span>
                  </div>

                  <div className="empty-panel">
                    <svg width="240" height="180" viewBox="0 0 240 180" fill="none" xmlns="http://www.w3.org/2000/svg" style={{ marginBottom: '16px' }}>
                      <path d="M190 120c0-5.5-4.5-10-10-10h-5c-6.6 0-12 5.4-12 12s5.4 12 12 12h15c5.5 0 10-4.5 10-10z" fill="#f1f5f9" opacity="0.6" />
                      <path d="M50 110c0-6.6 5.4-12 12-12h8c8.8 0 16 7.2 16 16s-7.2 16-16 16h-8c-6.6 0-12-5.4-12-12z" fill="#f1f5f9" opacity="0.6" />
                      <path d="M60 160h120" stroke="#3b82f6" strokeWidth="3" strokeLinecap="round" />
                      <rect x="94" y="24" width="72" height="96" rx="8" fill="#e2e8f0" />
                      <rect x="90" y="20" width="72" height="96" rx="8" fill="#ffffff" stroke="#cbd5e1" strokeWidth="2" />
                      <line x1="106" y1="46" x2="146" y2="46" stroke="#94a3b8" strokeWidth="2" strokeLinecap="round" />
                      <line x1="106" y1="58" x2="146" y2="58" stroke="#cbd5e1" strokeWidth="2" strokeLinecap="round" />
                      <line x1="106" y1="70" x2="146" y2="70" stroke="#cbd5e1" strokeWidth="2" strokeLinecap="round" />
                      <line x1="106" y1="82" x2="136" y2="82" stroke="#cbd5e1" strokeWidth="2" strokeLinecap="round" />
                      <line x1="106" y1="94" x2="146" y2="94" stroke="#cbd5e1" strokeWidth="2" strokeLinecap="round" />
                      <rect x="112" y="12" width="28" height="12" rx="4" fill="#3b82f6" />
                      <circle cx="126" cy="18" r="3" fill="#ffffff" />
                    </svg>

                    <h2>Chưa có Luật khu trọ</h2>
                    <p>Vui lòng hoàn thành Luật khu trọ trước khi bắt đầu tạo phòng.</p>
                    <button className="action-btn-primary" onClick={() => setActiveTab('house-rule')} style={{ padding: '8px 20px', minHeight: '40px' }}>
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '8px' }}>
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                        <polyline points="14 2 14 8 20 8" />
                        <line x1="16" y1="13" x2="8" y2="13" />
                        <line x1="16" y1="17" x2="8" y2="17" />
                      </svg>
                      Cấu hình Luật khu trọ
                    </button>
                  </div>
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

// ─── Subcomponents ──────────────────────────────────────────────────────────

function AmenityEditor({
  amenities,
  selectedIds,
  onChange,
  onSave,
  actionLoading,
}: {
  amenities: Amenity[];
  selectedIds: number[];
  onChange: (selectedIds: number[]) => void;
  onSave: () => void;
  actionLoading?: boolean;
}) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      <div className="amenity-card-grid">
        {amenities.map((amenity) => {
          const isSelected = selectedIds.includes(amenity.id);
          return (
            <label className={`amenity-card-item ${isSelected ? 'selected' : ''}`} key={amenity.id}>
              <input
                type="checkbox"
                className="amenity-card-checkbox"
                checked={isSelected}
                onChange={(event) =>
                  onChange(
                    event.target.checked
                      ? [...selectedIds, amenity.id]
                      : selectedIds.filter((id) => id !== amenity.id)
                  )
                }
              />
              <span className="amenity-card-name">{amenity.name}</span>
            </label>
          );
        })}
      </div>
      <div className="subtab-footer">
        <div className="shield-info">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
          </svg>
          <span>Tiện ích rõ ràng giúp tăng tỷ lệ tiếp cận khách thuê trọ.</span>
        </div>
        <button className="primary-action" onClick={onSave} disabled={actionLoading}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
            <polyline points="17 21 17 13 7 13 7 21" />
            <polyline points="7 3 7 8 15 8" />
          </svg>
          {actionLoading ? 'Đang lưu...' : 'Lưu tiện ích'}
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



