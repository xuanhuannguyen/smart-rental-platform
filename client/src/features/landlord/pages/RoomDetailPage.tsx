import { useState, useEffect, type FormEvent } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Button } from '../../../shared/components/ui/Button';
import { formatStatus, getStatusToneClass } from '../../../shared/utils/status';
import { contractApi } from '../../contracts/api';
import { getRoomingHouseDetail } from '../../rooming-houses/api';
import {
  createRoom,
  getRoomDetail,
  submitRoom,
  updateRoom,
  updateRoomAmenities,
  updateRoomImages,
  updateRoomPriceTiers,
  getActiveContractByRoomId,
  getActiveTenantsByRoomId,
} from '../../rooms/api';
import type { RoomingHouseDetail } from '../../rooming-houses/types';
import type { Room, CreateRoomRequest, RoomPriceTierRequest } from '../../rooms/types';
import type {
  ContractAppendixChangeRequest,
  ContractAppendixResponse,
  ContractDetailResponse,
  ContractFileResponse,
  ContractOccupantResponse
} from '../../contracts/types';
import type { Amenity, PropertyImageRequest } from '../../rooming-houses/types';
import { getAmenities } from '../../rooming-houses/api';
import PropertyImageEditor from '../../rooming-houses/components/PropertyImageEditor';
import { cleanImages, toImageRequests } from '../../rooming-houses/utils/imageRequests';
import { formatDateVi, formatMoneyString, parseMoneyString } from '../../../shared/utils/format';
import { TerminateContractModal } from '../../rental-history/pages/TerminateContractModal';
import { AppendixPreviewModal } from '../../rental-history/components/AppendixPreviewModal';
import '../../rental-history/pages/TenantRentalHistoryDetailPage.css';

type RoomTab = 'basic' | 'images' | 'amenities' | 'price';
type RoomMainTab = 'room-info' | 'tenants' | 'contracts' | 'invoices';

const emptyRoomForm: CreateRoomRequest = {
  roomNumber: '',
  floor: 1,
  areaM2: null,
  maxOccupants: 1,
  isTieredPricing: false,
  description: '',
};

export default function RoomDetailPage() {
  const { id, roomId } = useParams<{ id: string; roomId?: string }>();
  const navigate = useNavigate();
  const { currentUser } = useAuth();

  const [house, setHouse] = useState<RoomingHouseDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [message, setMessage] = useState('');

  const location = useLocation();

  const [roomActiveTab, setRoomActiveTab] = useState<RoomTab>(
    (location.state as any)?.initialTab || 'basic'
  );
  const [selectedRoom, setSelectedRoom] = useState<Room | null>(null);
  const [roomAmenities, setRoomAmenities] = useState<Amenity[]>([]);

  // Form phòng
  const [roomForm, setRoomForm] = useState<CreateRoomRequest>(emptyRoomForm);
  const [roomImages, setRoomImages] = useState<PropertyImageRequest[]>([]);
  const [roomAmenityIds, setRoomAmenityIds] = useState<number[]>([]);
  const [priceTiers, setPriceTiers] = useState<RoomPriceTierRequest[]>([]);

  const [mainTab, setMainTab] = useState<RoomMainTab>('room-info');

  const [activeContract, setActiveContract] = useState<ContractDetailResponse | null>(null);
  const [activeTenants, setActiveTenants] = useState<ContractOccupantResponse[]>([]);
  const [occupantFilter, setOccupantFilter] = useState<'all' | 'active' | 'left'>('all');
  const [tabLoading, setTabLoading] = useState(false);
  const [contractActionError, setContractActionError] = useState<string | null>(null);
  const [isFileActionLoading, setIsFileActionLoading] = useState(false);
  const [isTerminateModalOpen, setIsTerminateModalOpen] = useState(false);
  const [appendices, setAppendices] = useState<ContractAppendixResponse[] | null>(null);
  const [appendicesError, setAppendicesError] = useState<string | null>(null);
  const [isAppendixModalOpen, setIsAppendixModalOpen] = useState(false);
  const [editingAppendix, setEditingAppendix] = useState<ContractAppendixResponse | null>(null);
  const [signingAppendixId, setSigningAppendixId] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    loadData();
  }, [id, roomId]);

  useEffect(() => {
    if (!roomId || !selectedRoom) return;

    if (mainTab !== 'tenants' && mainTab !== 'contracts') return;

    if (selectedRoom.status !== 'Occupied') {
      setActiveContract(null);
      setActiveTenants([]);
      setAppendices(null);
      return;
    }

    let isCancelled = false;

    async function loadActiveRoomContractData() {
      setTabLoading(true);
      setMessage('');

      try {
        if (mainTab === 'tenants') {
          const [contract, tenants] = await Promise.all([
            getActiveContractByRoomId(roomId!),
            getActiveTenantsByRoomId(roomId!),
          ]);

          if (isCancelled) return;
          setActiveContract(contract);
          setActiveTenants(tenants);
          return;
        }

        const contract = await getActiveContractByRoomId(roomId!);

        if (isCancelled) return;
        setActiveContract(contract);
        await refreshAppendices(contract.id);
      } catch (err) {
        if (isCancelled) return;
        setActiveContract(null);
        setActiveTenants([]);
        setAppendices([]);
        setAppendicesError(getApiErrorMessage(err, 'Không thể tải danh sách phụ lục.'));
        setMessage(getApiErrorMessage(err, 'Không thể tải dữ liệu hợp đồng đang active của phòng.'));
      } finally {
        if (!isCancelled) {
          setTabLoading(false);
        }
      }
    }

    void loadActiveRoomContractData();

    return () => {
      isCancelled = true;
    };
  }, [mainTab, roomId, selectedRoom]);

  async function loadData() {
    setLoading(true);
    setMessage('');
    try {
      const houseData = await getRoomingHouseDetail(id!);
      
      if (houseData.approvalStatus !== 'Approved') {
        setMessage('Khu trọ này chưa được quản trị viên phê duyệt. Không thể truy cập quản lý phòng.');
        setHouse(null);
        return;
      }

      if (!houseData.rentalPolicy || !houseData.houseRule) {
        setMessage('Vui lòng hoàn thành Chính Sách Thuê và Luật Khu Trọ trước khi truy cập trang này.');
        setHouse(houseData);
        return;
      }

      setHouse(houseData);

      // Load tiện ích phòng
      const amenitiesList = await getAmenities('Room');
      setRoomAmenities(amenitiesList);

      if (roomId) {
        const roomDetail = await getRoomDetail(roomId);
        setSelectedRoom(roomDetail);
        
        setRoomForm({
          roomNumber: roomDetail.roomNumber,
          floor: roomDetail.floor,
          areaM2: roomDetail.areaM2 ?? null,
          maxOccupants: roomDetail.maxOccupants,
          isTieredPricing: roomDetail.isTieredPricing,
          description: roomDetail.description ?? '',
        });
        setRoomImages(toImageRequests(roomDetail.images));
        setRoomAmenityIds(roomDetail.amenities.map((a) => a.id));
        setPriceTiers(
          roomDetail.priceTiers.map((tier) => ({
            occupantCount: tier.occupantCount,
            monthlyRent: tier.monthlyRent,
            isActive: tier.isActive,
          }))
        );
      }
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể tải thông tin phòng.'));
    } finally {
      setLoading(false);
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
        setMessage('Đã lưu thông tin cơ bản phòng.');
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
      setMessage('Phòng đã được hiển thị hoạt động và sẵn sàng cho thuê.');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không thể hiển thị hoạt động phòng.'));
    } finally {
      setActionLoading(false);
    }
  }

  async function refreshAppendices(contractId: string) {
    try {
      const response = await contractApi.getAppendices(contractId);
      setAppendices(response.data ?? []);
      setAppendicesError(null);
    } catch (err) {
      setAppendices([]);
      setAppendicesError(getApiErrorMessage(err, 'Không thể tải danh sách phụ lục.'));
    }
  }

  async function handleViewContract() {
    await openContractFile('view');
  }

  async function handleDownloadContract() {
    await openContractFile('download');
  }

  async function openContractFile(mode: 'view' | 'download') {
    if (!activeContract) return;

    try {
      setContractActionError(null);
      setIsFileActionLoading(true);

      const file = await resolveRawContractFile(activeContract.id);
      const blob = await contractApi.downloadContractFile(activeContract.id, file.id);
      const url = URL.createObjectURL(blob);

      if (mode === 'view') {
        window.open(url, '_blank', 'noopener,noreferrer');
        window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
        return;
      }

      const link = document.createElement('a');
      link.href = url;
      link.download = `${activeContract.contractNumber}-${file.fileVariant.toLowerCase()}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      setContractActionError(getApiErrorMessage(err, 'Không thể tải file hợp đồng.'));
    } finally {
      setIsFileActionLoading(false);
    }
  }

  const visibleAppendices = appendices?.filter((appendix) => {
    if (!currentUser) return false;
    return shouldShowAppendixToCurrentUser(appendix, currentUser.userId);
  }) ?? null;

  const hasBlockingAppendix = appendices?.some(isBlockingAppendix) ?? false;

  if (loading) {
    return (
      <div className="landlord-dashboard">
        <aside className="dashboard-sidebar">
          <h1>Chủ trọ</h1>
        </aside>
        <main className="dashboard-main">
          <div className="empty-panel">Đang tải thông tin phòng...</div>
        </main>
      </div>
    );
  }

  if (!house || !selectedRoom) {
    return (
      <div className="landlord-dashboard">
        <aside className="dashboard-sidebar">
          <h1>Chủ trọ</h1>
        </aside>
        <main className="dashboard-main">
          <div className="empty-panel">
            <h2>Lỗi truy cập</h2>
            <p>{message || 'Không thể truy cập thông tin phòng.'}</p>
            <button className="primary-action" onClick={() => navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSE_DETAIL(id!))}>
              Quay lại danh sách phòng
            </button>
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
          onClick={() => navigate(ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS)}
        >
          Yêu cầu thuê
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
        <section className="overview-band">
          <div className="overview-header-title-area">
            <button
              type="button"
              className="back-icon-btn"
              onClick={() => navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSE_DETAIL(id!))}
              title="Quay về danh sách phòng"
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <line x1="19" y1="12" x2="5" y2="12" />
                <polyline points="12 19 5 12 12 5" />
              </svg>
            </button>
            <div className="overview-left">
              <p className="eyebrow">{house.addressDisplay}</p>
              <h2>
                {`Phòng ${selectedRoom?.roomNumber} - ${house.name}`}
              </h2>
              <p className="overview-description">
                {`Diện tích: ${selectedRoom?.areaM2 ? `${selectedRoom.areaM2} m²` : 'Chưa nhập'} | Tầng: ${selectedRoom?.floor} | Tối đa: ${selectedRoom?.maxOccupants} người`}
              </p>
            </div>
          </div>
          
          <div className="overview-right">
            <div className="overview-stats" style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '12px' }}>
              <span className={`status-pill ${getStatusToneClass(selectedRoom.status)}`} style={{ padding: '8px 16px', fontSize: '15px', fontWeight: 'bold', border: '1px solid currentColor' }}>
                {formatStatus(selectedRoom.status)}
              </span>
          </div>

          <div className="main-tabs" style={{ marginTop: '20px' }}>
            <button
              className={`tab-btn ${mainTab === 'room-info' ? 'active' : ''}`}
              onClick={() => setMainTab('room-info')}
            >
              Thông tin phòng
            </button>
            <button
              className={`tab-btn ${mainTab === 'tenants' ? 'active' : ''}`}
              onClick={() => setMainTab('tenants')}
              disabled={selectedRoom.status !== 'Occupied'}
              title={selectedRoom.status !== 'Occupied' ? 'Phòng phải ở trạng thái Đang cho thuê mới có thể quản lý người ở' : ''}
            >
              Người ở
            </button>
            <button
              className={`tab-btn ${mainTab === 'contracts' ? 'active' : ''}`}
              onClick={() => setMainTab('contracts')}
              disabled={selectedRoom.status !== 'Occupied'}
              title={selectedRoom.status !== 'Occupied' ? 'Phòng phải ở trạng thái Đang cho thuê mới có hợp đồng active' : ''}
            >
              Hợp đồng
            </button>
            <button
              className={`tab-btn ${mainTab === 'invoices' ? 'active' : ''}`}
              onClick={() => setMainTab('invoices')}
              disabled={selectedRoom.status !== 'Occupied'}
              title={selectedRoom.status !== 'Occupied' ? 'Phòng phải ở trạng thái Đang cho thuê mới có thể quản lý hóa đơn' : ''}
            >
              Hóa đơn
            </button>
          </div>
          </div>
        </section>

        {message && <p className="dashboard-message">{message}</p>}
        {actionLoading && <p className="dashboard-message" style={{ background: '#dbeafe', color: '#1e40af' }}>Đang lưu thay đổi...</p>}

        {mainTab === 'room-info' && (
          <div className="editor-panel" style={{ marginTop: '20px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <div className="tabs" style={{ marginBottom: 0 }}>
                <button className={roomActiveTab === 'basic' ? 'active' : ''} onClick={() => setRoomActiveTab('basic')}>
                  Thông tin cơ bản
                </button>
                <button
                  className={roomActiveTab === 'images' ? 'active' : ''}
                  onClick={() => setRoomActiveTab('images')}
                >
                  Ảnh phòng
                </button>
                <button
                  className={roomActiveTab === 'amenities' ? 'active' : ''}
                  onClick={() => setRoomActiveTab('amenities')}
                >
                  Tiện ích phòng
                </button>
                <button
                  className={roomActiveTab === 'price' ? 'active' : ''}
                  onClick={() => setRoomActiveTab('price')}
                >
                  Bảng giá
                </button>
              </div>

              {selectedRoom && selectedRoom.status === 'Hidden' && (
                <button className="primary-action" onClick={handlePublishRoom}>
                  Hiển thị phòng (Hoạt động)
                </button>
              )}
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

        {/* TAB 2: NGƯỜI Ở */}
        {mainTab === 'tenants' && (
          <div className="history-detail-content" style={{ marginTop: '20px' }}>
            {tabLoading && <p>Đang tải danh sách người ở...</p>}
            {!tabLoading && (
              <div>
                <div className="occupants-filter">
                  <button
                    className={`occupant-filter-btn ${occupantFilter === 'all' ? 'active' : ''}`}
                    onClick={() => setOccupantFilter('all')}
                  >
                    Tất cả ({activeTenants.length})
                  </button>
                  <button
                    className={`occupant-filter-btn ${occupantFilter === 'active' ? 'active' : ''}`}
                    onClick={() => setOccupantFilter('active')}
                  >
                    Đang ở ({activeTenants.filter((occupant) => occupant.status === 'Active').length})
                  </button>
                  <button
                    className={`occupant-filter-btn ${occupantFilter === 'left' ? 'active' : ''}`}
                    onClick={() => setOccupantFilter('left')}
                  >
                    Đã rời đi ({activeTenants.filter((occupant) => occupant.status !== 'Active' || occupant.moveOutDate).length})
                  </button>
                </div>

                <div className="occupant-list">
                  {activeTenants
                    .filter((occupant) => {
                      if (occupantFilter === 'active') return occupant.status === 'Active';
                      if (occupantFilter === 'left') return occupant.status !== 'Active' || occupant.moveOutDate !== null;
                      return true;
                    })
                    .map((occupant) => (
                      <div key={occupant.id} className="occupant-item">
                        <div className="occupant-info">
                          <h4>{occupant.fullName} - {formatOccupantRole(Boolean(activeContract && occupant.userId === activeContract.mainTenantUserId))}</h4>
                          <div className="occupant-role">Email: {formatOptionalContact(occupant.email)}</div>
                          <div className="occupant-role">Số điện thoại: {formatOptionalContact(occupant.phoneNumber)}</div>
                        </div>
                        <div className="occupant-dates">
                          <div style={{ marginBottom: '6px' }}>
                            <span className={`status-badge ${occupant.status === 'Active' ? 'success' : 'danger'}`} style={{ padding: '2px 8px', fontSize: '0.75rem' }}>
                              {occupant.status === 'Active' ? 'Đang ở' : 'Đã rời đi'}
                            </span>
                          </div>
                          <div><strong>Vào:</strong> {formatDateVi(occupant.moveInDate)}</div>
                          {occupant.moveOutDate && (
                            <div style={{ color: '#94a3b8' }}><strong>Rời đi:</strong> {formatDateVi(occupant.moveOutDate)}</div>
                          )}
                        </div>
                      </div>
                    ))}
                  {activeTenants.length === 0 && (
                    <div style={{ textAlign: 'center', padding: '20px', color: '#64748b' }}>Không có người ở nào được ghi nhận.</div>
                  )}
                </div>
              </div>
            )}
          </div>
        )}

        {/* TAB 3: HỢP ĐỒNG */}
        {mainTab === 'contracts' && (
          <div className="history-detail-content" style={{ marginTop: '20px' }}>
            {tabLoading && <p>Đang tải thông tin hợp đồng...</p>}
            {!tabLoading && activeContract && (
              <div>
                <div className="contract-info-grid">
                  <div className="contract-info-block">
                    <h3>Thông tin cơ bản</h3>
                    <div className="contract-info-item">
                      <span className="label">Người thuê chính:</span>
                      <span className="value">{activeContract.mainTenantName}</span>
                    </div>
                    <div className="contract-info-item">
                      <span className="label">Ngày bắt đầu:</span>
                      <span className="value">{formatDateVi(activeContract.startDate)}</span>
                    </div>
                    <div className="contract-info-item">
                      <span className="label">Ngày kết thúc:</span>
                      <span className="value">{formatDateVi(activeContract.endDate)}</span>
                    </div>
                  </div>
                  <div className="contract-info-block">
                    <h3>Thông tin tài chính</h3>
                    <div className="contract-info-item">
                      <span className="label">Tiền thuê hằng tháng:</span>
                      <span className="value">{formatMoneyString(activeContract.monthlyRent)} đ</span>
                    </div>
                    <div className="contract-info-item">
                      <span className="label">Tiền cọc:</span>
                      <span className="value">{formatMoneyString(activeContract.depositAmount)} đ</span>
                    </div>
                    <div className="contract-info-item">
                      <span className="label">Ngày thanh toán hằng tháng:</span>
                      <span className="value">Ngày {activeContract.paymentDay}</span>
                    </div>
                  </div>
                </div>

                <div className="contract-actions">
                  <Button variant="outline" onClick={handleDownloadContract} disabled={isFileActionLoading}>
                    Tải hợp đồng
                  </Button>
                  <Button variant="outline" onClick={handleViewContract} disabled={isFileActionLoading}>
                    Xem hợp đồng
                  </Button>
                  <Button variant="danger" onClick={() => setIsTerminateModalOpen(true)}>
                    Chấm dứt hợp đồng
                  </Button>
                </div>

                {contractActionError && (
                  <div style={{ color: '#b91c1c', marginTop: '12px' }}>
                    {contractActionError}
                  </div>
                )}

                <div className="appendices-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '28px', marginBottom: '16px' }}>
                  <h3 style={{ margin: 0 }}>Danh sách phụ lục</h3>
                  <Button
                    style={{ whiteSpace: 'nowrap' }}
                    onClick={() => setIsAppendixModalOpen(true)}
                    disabled={hasBlockingAppendix}
                    title={hasBlockingAppendix ? 'Không thể tạo phụ lục mới khi đang có phụ lục chờ ký hoặc đang yêu cầu sửa.' : ''}
                  >
                    Tạo phụ lục
                  </Button>
                </div>

                {appendicesError && (
                  <div style={{ color: '#b91c1c', marginBottom: '16px', padding: '12px', background: '#fef2f2', borderRadius: '8px' }}>
                    {appendicesError}
                  </div>
                )}

                <div className="appendices-list">
                  {visibleAppendices === null ? (
                    <div style={{ padding: '20px', color: '#64748b' }}>Đang tải danh sách phụ lục...</div>
                  ) : visibleAppendices.length === 0 ? (
                    <div style={{ textAlign: 'center', padding: '20px', color: '#64748b', background: '#f8fafc', borderRadius: '8px' }}>Hợp đồng này chưa có phụ lục nào.</div>
                  ) : (
                    visibleAppendices.map((appendix) => (
                      <div key={appendix.id} className="appendix-item">
                        <div className="appendix-info">
                          <h4>Phụ lục số {appendix.appendixNumber}</h4>
                          <div className="appendix-dates" style={{ textAlign: 'left', marginTop: '4px' }}>
                            <div><strong>Ngày hiệu lực:</strong> {formatDateVi(appendix.effectiveDate)}</div>
                          </div>
                          <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginTop: '10px' }}>
                            {appendix.status === 'TenantRevisionRequested' && (
                              <Button
                                style={{ padding: '6px 12px', fontSize: '0.85rem' }}
                                onClick={() => setEditingAppendix(appendix)}
                              >
                                Sửa phụ lục
                              </Button>
                            )}
                            {canLandlordOpenAppendixForSigning(appendix) && (
                              <Button
                                style={{ padding: '6px 12px', fontSize: '0.85rem' }}
                                onClick={() => setSigningAppendixId(appendix.id)}
                              >
                                Xem và ký phụ lục
                              </Button>
                            )}
                          </div>
                        </div>
                        <div className="appendix-status-badge" style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '8px' }}>
                          <span className={`status-badge ${getAppendixStatusClass(appendix.status)}`} style={{ padding: '6px 16px', fontSize: '0.85rem' }}>
                            {formatAppendixStatus(appendix)}
                          </span>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            )}
            {!tabLoading && !activeContract && (
              <div className="empty-panel">
                <p>Không tìm thấy hợp đồng đang active cho phòng này.</p>
              </div>
            )}
          </div>
        )}

        {/* TAB 4: HÓA ĐƠN */}
        {mainTab === 'invoices' && (
          <div className="empty-panel" style={{ marginTop: '20px' }}>
            <h2>Hóa đơn của hợp đồng hiện tại</h2>
            <p>Tính năng hiển thị danh sách hóa đơn hàng tháng thuộc về hợp đồng đang active đang được phát triển.</p>
          </div>
        )}
      </main>

      {isTerminateModalOpen && activeContract && (
        <TerminateContractModal
          contract={activeContract}
          terminationActor="Landlord"
          onClose={() => setIsTerminateModalOpen(false)}
          onTerminated={() => {
            setActiveContract(null);
            setActiveTenants([]);
            setSelectedRoom((current) => current ? { ...current, status: 'Available' } : current);
            setMainTab('room-info');
            setIsTerminateModalOpen(false);
            setMessage('Đã chấm dứt hợp đồng.');
          }}
        />
      )}

      {isAppendixModalOpen && activeContract && (
        <LandlordCreateAppendixModalV2
          contract={activeContract}
          onClose={() => setIsAppendixModalOpen(false)}
          onCreated={() => {
            setIsAppendixModalOpen(false);
            void refreshAppendices(activeContract.id);
          }}
        />
      )}

      {editingAppendix && activeContract && (
        <LandlordCreateAppendixModalV2
          contract={activeContract}
          appendix={editingAppendix}
          onClose={() => setEditingAppendix(null)}
          onCreated={() => {
            setEditingAppendix(null);
            void refreshAppendices(activeContract.id);
          }}
        />
      )}

      {signingAppendixId && activeContract && (
        <AppendixPreviewModal
          contractId={activeContract.id}
          appendixId={signingAppendixId}
          isCreator={appendices?.find((appendix) => appendix.id === signingAppendixId)?.createdByUserId === currentUser?.userId}
          hasNoSignatures={appendices?.find((appendix) => appendix.id === signingAppendixId)?.signatures.length === 0}
          onClose={() => setSigningAppendixId(null)}
          onSuccess={() => {
            setSigningAppendixId(null);
            void refreshAppendices(activeContract.id);
          }}
        />
      )}
    </div>
  );
}

// ─── Subcomponents ──────────────────────────────────────────────────────────

async function resolveRawContractFile(contractId: string): Promise<ContractFileResponse> {
  const response = await contractApi.getContractFiles(contractId);
  let file = findContractFile(response.data ?? [], 'Raw');

  if (!file) {
    await contractApi.generateContractFile(contractId);
    const refreshedResponse = await contractApi.getContractFiles(contractId);
    file = findContractFile(refreshedResponse.data ?? [], 'Raw');
  }

  if (!file) {
    throw new Error('Chưa có file hợp đồng phù hợp để xem hoặc tải.');
  }

  return file;
}

function findContractFile(files: ContractFileResponse[], fileVariant: string) {
  return files
    .filter((file) => !file.rentalContractAppendixId && file.fileVariant === fileVariant)
    .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0];
}

type LandlordAppendixChangeMode = 'monthlyRent' | 'paymentDay';

interface LandlordAppendixChangeForm {
  id: string;
  mode: LandlordAppendixChangeMode;
  value: string;
}

function LandlordCreateAppendixModalV2({
  contract,
  appendix,
  onClose,
  onCreated,
}: {
  contract: ContractDetailResponse;
  appendix?: ContractAppendixResponse;
  onClose: () => void;
  onCreated: (appendix: ContractAppendixResponse) => void;
}) {
  const today = new Date().toISOString().slice(0, 10);
  const [effectiveDate, setEffectiveDate] = useState(appendix?.effectiveDate ?? today);
  const [changes, setChanges] = useState<LandlordAppendixChangeForm[]>(() =>
    buildLandlordAppendixChangeForms(appendix, contract)
  );
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function addChange() {
    setChanges((current) => {
      const mode: LandlordAppendixChangeMode = current.some((item) => item.mode === 'monthlyRent')
        ? 'paymentDay'
        : 'monthlyRent';

      return [...current, createLandlordAppendixChange(mode, getLandlordAppendixDefaultValue(mode, contract))];
    });
  }

  function removeChange(id: string) {
    setChanges((current) => current.filter((item) => item.id !== id));
  }

  function updateChange(id: string, patch: Partial<LandlordAppendixChangeForm>) {
    setChanges((current) => current.map((item) => item.id === id ? { ...item, ...patch } : item));
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    const payloadChanges: ContractAppendixChangeRequest[] = [];
    const seenModes = new Set<LandlordAppendixChangeMode>();

    for (let index = 0; index < changes.length; index++) {
      const change = changes[index];
      const number = index + 1;

      if (seenModes.has(change.mode)) {
        setError(`Thay đổi #${number}: loại thay đổi bị trùng.`);
        return;
      }

      seenModes.add(change.mode);

      if (change.mode === 'monthlyRent') {
        const monthlyRent = Number.parseFloat(change.value);
        if (!Number.isFinite(monthlyRent) || monthlyRent <= 0) {
          setError(`Thay đổi #${number}: giá thuê mới phải lớn hơn 0.`);
          return;
        }

        if (!appendix && monthlyRent === contract.monthlyRent) {
          setError(`Thay đổi #${number}: giá thuê mới phải khác giá thuê hiện tại.`);
          return;
        }

        payloadChanges.push({
          changeType: 'Update',
          targetType: 'Contract',
          fieldName: 'monthlyRent',
          newValue: String(monthlyRent),
        });
        continue;
      }

      const paymentDay = Number.parseInt(change.value, 10);
      if (!Number.isInteger(paymentDay) || paymentDay < 1 || paymentDay > 28) {
        setError(`Thay đổi #${number}: ngày thanh toán mới phải nằm trong khoảng 1 đến 28.`);
        return;
      }

      if (!appendix && paymentDay === contract.paymentDay) {
        setError(`Thay đổi #${number}: ngày thanh toán mới phải khác ngày hiện tại.`);
        return;
      }

      payloadChanges.push({
        changeType: 'Update',
        targetType: 'Contract',
        fieldName: 'paymentDay',
        newValue: String(paymentDay),
      });
    }

    if (payloadChanges.length === 0) {
      setError('Vui lòng thêm ít nhất một thay đổi trước khi gửi phụ lục.');
      return;
    }

    try {
      setIsSubmitting(true);
      setError(null);

      const payload = {
        effectiveDate,
        changes: payloadChanges,
      };
      const response = appendix
        ? await contractApi.updateAppendix(contract.id, appendix.id, payload)
        : await contractApi.createAppendix(contract.id, payload);

      onCreated(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tạo phụ lục. Vui lòng kiểm tra lại thông tin.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="occupants-setup-overlay">
      <div className="occupants-setup-container-modal">
        <div className="occupants-setup-header">
          <h2>{appendix ? 'Sửa phụ lục' : 'Tạo phụ lục'}</h2>
          <button type="button" className="occupants-setup-close-btn" onClick={onClose}>&times;</button>
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', flex: 1, overflow: 'hidden' }}>
          <div className="occupants-setup-modal-content">
            <div className="form-group" style={{ marginBottom: '24px' }}>
              <label>Ngày hiệu lực</label>
              <input
                type="date"
                value={effectiveDate}
                min={today}
                onChange={(event) => setEffectiveDate(event.target.value)}
                required
              />
            </div>

            <div className="occupants-list">
              {changes.map((change, index) => (
                <div key={change.id} className="occupant-card">
                  <div className="occupant-card-header">
                    <h3 style={{ margin: 0, fontSize: '1rem' }}>Thay đổi #{index + 1}</h3>
                    {changes.length > 1 && (
                      <button type="button" className="remove-btn" onClick={() => removeChange(change.id)}>
                        Xóa
                      </button>
                    )}
                  </div>

                  <div className="form-group">
                    <label>Loại thay đổi</label>
                    <select
                      value={change.mode}
                      onChange={(event) => {
                        const mode = event.target.value as LandlordAppendixChangeMode;
                        updateChange(change.id, {
                          mode,
                          value: getLandlordAppendixDefaultValue(mode, contract)
                        });
                      }}
                    >
                      <option value="monthlyRent">Thay đổi giá thuê</option>
                      <option value="paymentDay">Thay đổi ngày thanh toán</option>
                    </select>
                  </div>

                  {change.mode === 'monthlyRent' ? (
                    <div className="form-group">
                      <label>Giá thuê hàng tháng</label>
                      <input
                        type="number"
                        value={change.value}
                        onChange={(event) => updateChange(change.id, { value: event.target.value })}
                        required
                      />
                    </div>
                  ) : (
                    <div className="form-group">
                      <label>Ngày thanh toán hàng tháng</label>
                      <input
                        type="number"
                        min="1"
                        max="28"
                        value={change.value}
                        onChange={(event) => updateChange(change.id, { value: event.target.value })}
                        required
                      />
                    </div>
                  )}
                </div>
              ))}
            </div>

            {error && (
              <p style={{ color: '#dc2626', marginTop: '12px', fontSize: '0.95rem' }}>
                {error}
              </p>
            )}
          </div>

          <div className="setup-actions" style={{ padding: '16px 24px', backgroundColor: '#fff', borderRadius: '0 0 16px 16px', columnGap: '12px' }}>
            <Button
              variant="secondary"
              type="button"
              onClick={addChange}
              disabled={isSubmitting || changes.length >= 2}
              style={{ marginRight: 'auto' }}
            >
              + Thêm thay đổi
            </Button>
            <Button variant="outline" type="button" onClick={onClose} disabled={isSubmitting}>Hủy</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'Đang lưu...' : 'Gửi phụ lục'}</Button>
          </div>
        </form>
      </div>
    </div>
  );
}

function createLandlordAppendixChange(
  mode: LandlordAppendixChangeMode,
  value: string
): LandlordAppendixChangeForm {
  return {
    id: crypto.randomUUID(),
    mode,
    value,
  };
}

function buildLandlordAppendixChangeForms(
  appendix: ContractAppendixResponse | undefined,
  contract: ContractDetailResponse
): LandlordAppendixChangeForm[] {
  if (!appendix) {
    return [createLandlordAppendixChange('monthlyRent', String(contract.monthlyRent))];
  }

  const forms = appendix.changes
    .filter((change) => change.changeType === 'Update' && change.targetType === 'Contract')
    .map((change) => {
      const fieldName = normalizeLandlordAppendixFieldName(change.fieldName);
      if (fieldName === 'paymentday') {
        return createLandlordAppendixChange('paymentDay', parseAppendixScalarValue(change.newValue) || String(contract.paymentDay));
      }

      if (fieldName === 'monthlyrent') {
        return createLandlordAppendixChange('monthlyRent', parseAppendixScalarValue(change.newValue) || String(contract.monthlyRent));
      }

      return null;
    })
    .filter((change): change is LandlordAppendixChangeForm => Boolean(change));

  return forms.length > 0 ? forms : [createLandlordAppendixChange('monthlyRent', String(contract.monthlyRent))];
}

function getLandlordAppendixDefaultValue(mode: LandlordAppendixChangeMode, contract: ContractDetailResponse) {
  return mode === 'monthlyRent' ? String(contract.monthlyRent) : String(contract.paymentDay);
}

function normalizeLandlordAppendixFieldName(value?: string | null) {
  return value?.replace(/_/g, '').toLowerCase() ?? '';
}

function parseAppendixScalarValue(value?: string | null) {
  if (!value) return '';

  try {
    const parsed = JSON.parse(value);
    return typeof parsed === 'string' ? parsed : '';
  } catch {
    return value;
  }
}

function LandlordCreateAppendixModal({
  contract,
  appendix,
  onClose,
  onCreated,
}: {
  contract: ContractDetailResponse;
  appendix?: ContractAppendixResponse;
  onClose: () => void;
  onCreated: (appendix: ContractAppendixResponse) => void;
}) {
  const today = new Date().toISOString().slice(0, 10);
  const [effectiveDate, setEffectiveDate] = useState(appendix?.effectiveDate ?? today);
  const [monthlyRent, setMonthlyRent] = useState(resolveAppendixFieldValue(appendix, 'monthlyRent') ?? String(contract.monthlyRent));
  const [paymentDay, setPaymentDay] = useState(resolveAppendixFieldValue(appendix, 'paymentDay') ?? String(contract.paymentDay));
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    const parsedMonthlyRent = Number.parseFloat(monthlyRent);
    const parsedPaymentDay = Number.parseInt(paymentDay, 10);
    const changes: ContractAppendixChangeRequest[] = [];

    if (Number.isFinite(parsedMonthlyRent) && (appendix || parsedMonthlyRent !== contract.monthlyRent)) {
      if (parsedMonthlyRent <= 0) {
        setError('Giá thuê mới phải lớn hơn 0.');
        return;
      }

      changes.push({
        changeType: 'Update',
        targetType: 'Contract',
        fieldName: 'monthlyRent',
        newValue: String(parsedMonthlyRent),
      });
    }

    if (Number.isInteger(parsedPaymentDay) && (appendix || parsedPaymentDay !== contract.paymentDay)) {
      if (parsedPaymentDay < 1 || parsedPaymentDay > 28) {
        setError('Ngày thanh toán mới phải nằm trong khoảng 1 đến 28.');
        return;
      }

      changes.push({
        changeType: 'Update',
        targetType: 'Contract',
        fieldName: 'paymentDay',
        newValue: String(parsedPaymentDay),
      });
    }

    if (changes.length === 0) {
      setError('Vui lòng thay đổi giá thuê hoặc ngày thanh toán trước khi gửi phụ lục.');
      return;
    }

    try {
      setIsSubmitting(true);
      setError(null);
      const payload = {
        effectiveDate,
        changes,
      };
      const response = appendix
        ? await contractApi.updateAppendix(contract.id, appendix.id, payload)
        : await contractApi.createAppendix(contract.id, payload);
      onCreated(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tạo phụ lục. Vui lòng kiểm tra lại thông tin.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="history-modal-overlay">
      <div className="history-modal-content">
        <div className="history-modal-header">
          <h2>{appendix ? 'Sửa phụ lục' : 'Tạo phụ lục'}</h2>
          <button className="history-modal-close" onClick={onClose}>&times;</button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="history-modal-body">
            <div className="history-form-group">
              <label>Ngày hiệu lực</label>
              <input
                type="date"
                className="ui-input"
                value={effectiveDate}
                min={today}
                onChange={(event) => setEffectiveDate(event.target.value)}
                required
              />
            </div>

            <div className="history-form-group">
              <label>Giá thuê hằng tháng</label>
              <input
                type="number"
                className="ui-input"
                value={monthlyRent}
                onChange={(event) => setMonthlyRent(event.target.value)}
              />
            </div>

            <div className="history-form-group">
              <label>Ngày thanh toán hằng tháng</label>
              <input
                type="number"
                min="1"
                max="28"
                className="ui-input"
                value={paymentDay}
                onChange={(event) => setPaymentDay(event.target.value)}
              />
            </div>

            {error && (
              <p style={{ color: '#dc2626', marginTop: '12px', fontSize: '0.95rem' }}>
                {error}
              </p>
            )}
          </div>

          <div className="history-modal-footer">
            <Button variant="outline" type="button" onClick={onClose} disabled={isSubmitting}>Hủy</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'Đang lưu...' : 'Gửi phụ lục'}</Button>
          </div>
        </form>
      </div>
    </div>
  );
}

function formatAppendixStatus(appendix: ContractAppendixResponse) {
  if (appendix.status === 'PendingSignature') {
    const hasTenantSigned = appendix.signatures.some((signature) => signature.signerRole === 'Tenant');
    const hasLandlordSigned = appendix.signatures.some((signature) => signature.signerRole === 'Landlord');

    if (!hasLandlordSigned) return 'Chờ bạn ký';
    if (!hasTenantSigned) return 'Chờ khách thuê ký';
    return 'Chờ ký';
  }

  if (appendix.status === 'LandlordRevisionRequested') return 'Đang chờ khách sửa';
  if (appendix.status === 'TenantRevisionRequested') return 'Đang chờ bạn sửa';

  switch (appendix.status) {
    case 'Active':
      return 'Đang hiệu lực';
    case 'Rejected':
      return 'Từ chối';
    case 'Cancelled':
      return 'Đã kết thúc';
    case 'LandlordRevisionRequested':
      return 'Yêu cầu sửa (Chủ nhà)';
    case 'TenantRevisionRequested':
      return 'Yêu cầu sửa (Khách thuê)';
    default:
      return appendix.status;
  }
}

function getAppendixStatusClass(status: string) {
  switch (status) {
    case 'Active':
      return 'active';
    case 'PendingSignature':
    case 'LandlordRevisionRequested':
    case 'TenantRevisionRequested':
      return 'pending';
    case 'Rejected':
    case 'Cancelled':
      return 'terminated';
    default:
      return '';
  }
}

function isBlockingAppendix(appendix: ContractAppendixResponse) {
  return appendix.status === 'PendingSignature' ||
    appendix.status === 'LandlordRevisionRequested' ||
    appendix.status === 'TenantRevisionRequested';
}

function shouldShowAppendixToCurrentUser(appendix: ContractAppendixResponse, currentUserId: string) {
  if (appendix.createdByUserId === currentUserId) return true;
  if (appendix.status !== 'PendingSignature') return true;
  return appendix.signatures.some((signature) => signature.signerUserId === appendix.createdByUserId);
}

function canLandlordOpenAppendixForSigning(appendix: ContractAppendixResponse) {
  return appendix.status === 'PendingSignature' &&
    !appendix.signatures.some((signature) => signature.signerRole === 'Landlord');
}

function resolveAppendixFieldValue(appendix: ContractAppendixResponse | undefined, fieldName: string) {
  const change = appendix?.changes.find((item) =>
    item.fieldName?.replace(/_/g, '').toLowerCase() === fieldName.toLowerCase()
  );

  return change?.newValue ?? null;
}

function formatOccupantRole(isMainTenant: boolean) {
  return isMainTenant ? 'Người thuê chính' : 'Người ở cùng';
}

function formatOptionalContact(value?: string | null) {
  return value?.trim() ? value.trim() : 'Chưa xác định';
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
