import { useState, useEffect, useMemo, ReactNode } from 'react';
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
} from '../../rooming-houses/api';
import {
  createRoom,
  getRoomDetail,
  getRoomsByRoomingHouse,
  submitRoom,
  updateRoom,
  updateRoomAmenities,
  updateRoomImages,
  updateRoomPriceTiers,
} from '../../rooms/api';
import type {
  RoomingHouseDetail,
  Amenity,
  PropertyImageRequest,
  RoomingHouseBasicInfoRequest,
  UpdateRentalPolicyRequest,
} from '../../rooming-houses/types';
import type { Room, CreateRoomRequest, RoomPriceTierRequest } from '../../rooms/types';
import { getProvinces, getWardsByProvince } from '../../administrative/api';
import type { Province, Ward } from '../../administrative/types';
import PropertyImageEditor from '../../rooming-houses/components/PropertyImageEditor';
import LeafletLocationPicker from '../../rooming-houses/components/LeafletLocationPicker';
import RoomingHouseRuleEditor from '../../rooming-houses/components/RoomingHouseRuleEditor';
import { cleanImages, toImageRequests } from '../../rooming-houses/utils/imageRequests';
import './RoomingHouseDetailPage.css';

type MainTab = 'basic' | 'images' | 'amenities' | 'legal' | 'house-rule' | 'rental-policy' | 'rooms';
type RoomTab = 'basic' | 'images' | 'amenities' | 'price';

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
  const [selectedRoom, setSelectedRoom] = useState<Room | null>(null);
  const [roomAmenities, setRoomAmenities] = useState<Amenity[]>([]);
  const [roomEditorMode, setRoomEditorMode] = useState<'list' | 'edit' | 'create'>('list');
  const [roomActiveTab, setRoomActiveTab] = useState<RoomTab>('basic');

  // Form phòng
  const [roomForm, setRoomForm] = useState<CreateRoomRequest>(emptyRoomForm);
  const [roomImages, setRoomImages] = useState<PropertyImageRequest[]>([]);
  const [roomAmenityIds, setRoomAmenityIds] = useState<number[]>([]);
  const [priceTiers, setPriceTiers] = useState<RoomPriceTierRequest[]>([]);

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
      getProvinces().then(setProvinces).catch(() => {});
    }
    if (activeTab === 'amenities' && houseAmenities.length === 0) {
      getAmenities('House').then(setHouseAmenities).catch(() => {});
    }
  }, [activeTab]);

  // Tải Phường/Xã theo Tỉnh/Thành
  useEffect(() => {
    if (!basicForm.provinceCode) {
      setWards([]);
      return;
    }
    getWardsByProvince(basicForm.provinceCode).then(setWards).catch(() => {});
  }, [basicForm.provinceCode]);

  // Thống kê phòng
  const roomStats = useMemo(() => ({
    total: rooms.length,
    available: rooms.filter((r) => r.status === 'Available').length,
    occupied: rooms.filter((r) => r.status === 'Occupied').length,
    hidden: rooms.filter((r) => r.status === 'Hidden').length,
  }), [rooms]);
  const selectedProvinceName =
    provinces.find((province) => province.code === basicForm.provinceCode)?.name ?? '';
  const selectedWardName = wards.find((ward) => ward.code === basicForm.wardCode)?.name ?? '';
  const canCreateRoom = Boolean(house?.rentalPolicy && house?.houseRule);

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

  // ─── Tab 5: Logic Quản lý phòng ──────────────────────────────────────────
  async function openRoom(roomId: string) {
    setActionLoading(true);
    setMessage('');
    try {
      const roomDetail = await getRoomDetail(roomId);
      setSelectedRoom(roomDetail);
      setRoomEditorMode('edit');
      setRoomActiveTab('basic');

      // Fill form phòng
      setRoomForm({
        roomNumber: roomDetail.roomNumber,
        floor: roomDetail.floor,
        areaM2: roomDetail.areaM2 ?? null,
        maxOccupants: roomDetail.maxOccupants,
        isTieredPricing: roomDetail.isTieredPricing,
        description: roomDetail.description ?? '',
      });
      setRoomImages(toImageRequests(roomDetail.images));
      setRoomAmenityIds(roomDetail.amenities.map((amenity) => amenity.id));
      setPriceTiers(
        roomDetail.priceTiers.map((tier) => ({
          occupantCount: tier.occupantCount,
          monthlyRent: tier.monthlyRent,
          isActive: tier.isActive,
        }))
      );

      // Load tiện ích phòng
      if (roomAmenities.length === 0) {
        const amenitiesList = await getAmenities('Room');
        setRoomAmenities(amenitiesList);
      }
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể tải thông tin phòng.'));
    } finally {
      setActionLoading(false);
    }
  }

  async function openCreateRoom() {
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

    setSelectedRoom(null);
    setRoomEditorMode('create');
    setRoomActiveTab('basic');
    setRoomForm(emptyRoomForm);
    setRoomImages([]);
    setRoomAmenityIds([]);
    setPriceTiers([{ occupantCount: 1, monthlyRent: 0, isActive: true }]);
    setMessage('');

    try {
      if (roomAmenities.length === 0) {
        const amenitiesList = await getAmenities('Room');
        setRoomAmenities(amenitiesList);
      }
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể tải tiện ích phòng.'));
    }
  }

  async function handleSaveRoomBasic() {
    if (!house) return;
    setActionLoading(true);
    setMessage('');
    try {
      if (selectedRoom) {
        const updated = await updateRoom(selectedRoom.id, roomForm);
        setSelectedRoom(updated);
        setRooms(current => current.map(r => r.id === updated.id ? updated : r));
        setMessage('Đã lưu thông tin cơ bản phòng.');
      } else {
        const created = await createRoom(house.id, roomForm);
        setSelectedRoom(created);
        setRooms(current => [...current, created]);
        setRoomEditorMode('edit');
        setRoomActiveTab('images'); // chuyển sang tab ảnh
        setMessage('Tạo phòng thành công. Hãy tải lên ảnh minh họa cho phòng.');
      }
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể lưu thông tin phòng.'));
    } finally {
      setActionLoading(false);
    }
  }

  async function handleSaveRoomImages() {
    if (!selectedRoom) return;
    setActionLoading(true);
    setMessage('');
    try {
      const updated = await updateRoomImages(selectedRoom.id, cleanImages(roomImages));
      setSelectedRoom(updated);
      setRoomImages(toImageRequests(updated.images));
      setRooms(current => current.map(r => r.id === updated.id ? updated : r));
      setMessage('Đã lưu ảnh phòng thành công.');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể lưu ảnh phòng.'));
    } finally {
      setActionLoading(false);
    }
  }

  async function handleSaveRoomAmenities() {
    if (!selectedRoom) return;
    setActionLoading(true);
    setMessage('');
    try {
      const updated = await updateRoomAmenities(selectedRoom.id, roomAmenityIds);
      setSelectedRoom(updated);
      setRoomAmenityIds(updated.amenities.map(a => a.id));
      setRooms(current => current.map(r => r.id === updated.id ? updated : r));
      setMessage('Đã cập nhật tiện ích phòng thành công.');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể lưu tiện ích phòng.'));
    } finally {
      setActionLoading(false);
    }
  }

  async function handleSaveRoomPrice() {
    if (!selectedRoom) return;
    setActionLoading(true);
    setMessage('');
    try {
      const updated = await updateRoomPriceTiers(selectedRoom.id, priceTiers);
      setSelectedRoom(updated);
      setPriceTiers(updated.priceTiers.map(t => ({ occupantCount: t.occupantCount, monthlyRent: t.monthlyRent, isActive: t.isActive })));
      setRooms(current => current.map(r => r.id === updated.id ? updated : r));
      setMessage('Đã cập nhật bảng giá phòng thành công.');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể lưu bảng giá phòng.'));
    } finally {
      setActionLoading(false);
    }
  }

  async function handlePublishRoom() {
    if (!selectedRoom) return;
    setActionLoading(true);
    setMessage('');
    try {
      const updated = await submitRoom(selectedRoom.id);
      setSelectedRoom(updated);
      setRooms(current => current.map(r => r.id === updated.id ? updated : r));
      setMessage('Phòng đã được hiển thị hoạt động và sẵn sàng cho thuê.');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể hiển thị hoạt động phòng.'));
    } finally {
      setActionLoading(false);
    }
  }

  // ─── Renders ─────────────────────────────────────────────────────────────
  const formatDate = formatDateVi;

  if (loading) {
    return (
      <div className="landlord-dashboard">
        <aside className="dashboard-sidebar">
          <h1>Chủ trọ</h1>
          <button className="sidebar-item active">Quản lý khu trọ</button>
        </aside>
        <main className="dashboard-main">
          <div className="empty-panel">Đang tải thông tin khu trọ...</div>
        </main>
      </div>
    );
  }

  if (!house) {
    return (
      <div className="landlord-dashboard">
        <aside className="dashboard-sidebar">
          <h1>Chủ trọ</h1>
          <button className="sidebar-item" onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}>
            Quay lại Dashboard
          </button>
        </aside>
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
    <div className="landlord-dashboard rooming-house-detail-page">
      <aside className="dashboard-sidebar">
        <h1>Chủ trọ</h1>
        <button
          className="sidebar-item active"
          onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}
        >
          Quản lý khu trọ
        </button>
        <button
          className="sidebar-item"
          onClick={() => navigate(ROUTE_PATHS.LANDLORD.VIEWING_APPOINTMENTS)}
        >
          Lịch hẹn xem phòng
        </button>
        <button className="sidebar-item" disabled style={{ opacity: 0.6, cursor: 'not-allowed' }}>
          Quản lý doanh thu (Sau này)
        </button>
        <button className="sidebar-item sidebar-back-btn" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
          ← Quay lại trang chủ
        </button>
      </aside>

      <main className="dashboard-main">
        {/* Banner Tổng quan */}
        {activeTab === 'rooms' && (
          <section className="overview-band">
            <div className="overview-header-title-area">
              <button
                type="button"
                className="back-icon-btn"
                onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}
                title="Quay về quản lý khu trọ"
              >
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="19" y1="12" x2="5" y2="12" />
                  <polyline points="12 19 5 12 12 5" />
                </svg>
              </button>
              <div className="overview-left">
                <p className="eyebrow">{house.addressDisplay}</p>
                <h2>{house.name}</h2>
                <p className="overview-description">
                  Thời gian duyệt: {house.createdAt ? formatDate(house.createdAt) : ''}
                </p>
              </div>
            </div>
            
            <div className="overview-right">
              <div className="overview-actions">
                {roomEditorMode === 'list' && (
                  <>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => setActiveTab('basic')}
                    >
                      Xem thông tin khu trọ
                    </button>
                    <button
                      className="primary-action"
                      onClick={openCreateRoom}
                      disabled={!canCreateRoom}
                      title={!canCreateRoom ? "Vui lòng cấu hình chính sách thuê và Luật khu trọ trước khi tạo phòng." : undefined}
                      style={!canCreateRoom ? { opacity: 0.5, cursor: 'not-allowed' } : undefined}
                    >
                      Tạo phòng mới
                    </button>
                  </>
                )}
              </div>
              
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
            </div>
          </section>
        )}

        {message && <p className="dashboard-message">{message}</p>}
        {actionLoading && <p className="dashboard-message" style={{ background: '#dbeafe', color: '#1e40af' }}>Đang lưu thay đổi...</p>}



        {/* Hệ thống Tab Cấp 2 */}
        {activeTab !== 'rooms' && (
          <div className="tabs" style={{ display: 'flex', alignItems: 'center' }}>
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
            <button
              type="button"
              className="primary-action"
              style={{ marginLeft: 'auto', minHeight: '38px', padding: '8px 14px' }}
              onClick={() => {
                setActiveTab('rooms');
                setRoomEditorMode('list');
              }}
            >
              Quay lại danh sách phòng
            </button>
          </div>
        )}

        {/* Nội dung Tab */}
        <div className="tab-content" style={{ marginTop: '16px' }}>
          {/* TAB 1: THÔNG TIN CƠ BẢN */}
          {activeTab === 'basic' && (
            <div className="editor-panel form-grid">
              <label className="field">
                <span>Tên khu trọ</span>
                <input value={basicForm.name} onChange={e => setBasicForm({ ...basicForm, name: e.target.value })} />
              </label>

              <label className="field">
                <span>Địa chỉ chi tiết</span>
                <input value={basicForm.addressLine} onChange={e => setBasicForm({ ...basicForm, addressLine: e.target.value })} />
              </label>

              <label className="field">
                <span>Tỉnh / Thành phố</span>
                <select
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
                  disabled={!basicForm.provinceCode}
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
                  setBasicForm((current) => ({ ...current, addressLine }))
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

          {/* TAB 5: QUẢN LÝ PHÒNG */}
          {activeTab === 'rooms' && (
            <div>
              {roomEditorMode === 'list' && (
                <>
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
                        <button key={room.id} className="dashboard-card" onClick={() => openRoom(room.id)}>
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
                </>
              )}

              {(roomEditorMode === 'edit' || roomEditorMode === 'create') && (
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
              )}
            </div>
          )}
        </div>
      </main>
    </div>
  );
}

// ─── Subcomponents ──────────────────────────────────────────────────────────

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


