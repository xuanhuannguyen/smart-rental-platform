import { Alert } from '../../../shared/components/ui/Alert';
import { PrivateMediaImage } from '../../../shared/components/media/PrivateMediaImage';
import { useState, useEffect, useMemo, type FormEvent } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Button } from '../../../shared/components/ui/Button';
import { Tabs } from '../../../shared/components/ui/Tabs';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
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
  updateRoomStatus,
} from '../../rooms/api';
import type { RoomingHouseDetail } from '../../rooming-houses/types';
import type { Room, CreateRoomRequest, RoomPriceTierRequest } from '../../rooms/types';
import type {
  ContractAppendixChangeRequest,
  ContractAppendixResponse,
  ContractAppendixStatus,
  ContractDetailResponse,
  ContractFileResponse,
  ContractOccupantResponse
} from '../../contracts/types';
import {
  canLandlordOpenAppendixForSigning,
  formatAppendixStatus,
  isBlockingAppendix,
  shouldShowAppendixToCurrentUser,
} from '../../contracts/appendixRules';
import {
  findAccessibleAppendixFile,
  loadAccessibleContractFiles,
} from '../../contracts/appendixFiles';
import { AppendixFileActions } from '../../contracts/components/AppendixFileActions';
import { openContractFileForView } from '../../contracts/fileAccess';
import { LandlordCreateAppendixModalV2 } from '../../contracts/components/LandlordCreateAppendixModalV2';
import type { Amenity, PropertyImageRequest } from '../../rooming-houses/types';
import { getAmenities } from '../../rooming-houses/api';
import PropertyImageEditor from '../../rooming-houses/components/PropertyImageEditor';
import { cleanImages, toImageRequests } from '../../rooming-houses/utils/imageRequests';
import { formatDateVi, formatMoneyString, parseMoneyString } from '../../../shared/utils/format';
import { TerminateContractModal } from '../../rental-history/pages/TerminateContractModal';
import { AppendixPreviewModal } from '../../rental-history/components/AppendixPreviewModal';
import { billingApi } from '../../billing/api';
import { Toast } from '../../../shared/components/ui/Toast';
import type {
  FixedServicePreview,
  Invoice,
  MeteredServicePreview,
  MeterReadingInput,
  PricingUnit,
  RoomInvoicePreview,
  ServicePrice
} from '../../billing/types';
import '../../rental-history/pages/TenantRentalHistoryDetailPage.css';
import '../../billing/pages/BillingPages.css';
import './RoomingHouseDetailPage.css';

type RoomTab = 'basic' | 'images' | 'amenities' | 'price';
type RoomMainTab = 'room-info' | 'tenants' | 'contracts' | 'invoices';
type OccupantFilter = 'all' | 'active' | 'pending' | 'left';
const invoiceStatusTabs = ['', 'Issued', 'Paid', 'Overdue', 'Cancelled'];

const emptyRoomForm: CreateRoomRequest = {
  roomNumber: '',
  floor: 1,
  areaM2: null,
  maxOccupants: 1,
  isTieredPricing: false,
  description: '',
};

function getInvoiceStatusTabIcon(status: string) {
  const props = { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const };

  switch (status) {
    case 'Issued':
      return <svg {...props}><circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" /></svg>;
    case 'Paid':
      return <svg {...props}><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" /><polyline points="22 4 12 14.01 9 11.01" /></svg>;
    case 'Overdue':
      return <svg {...props}><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" /><line x1="12" y1="9" x2="12" y2="13" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg>;
    case 'Cancelled':
      return <svg {...props}><circle cx="12" cy="12" r="10" /><line x1="15" y1="9" x2="9" y2="15" /><line x1="9" y1="9" x2="15" y2="15" /></svg>;
    default:
      return <svg {...props}><line x1="8" y1="6" x2="21" y2="6" /><line x1="8" y1="12" x2="21" y2="12" /><line x1="8" y1="18" x2="21" y2="18" /><line x1="3" y1="6" x2="3.01" y2="6" /><line x1="3" y1="12" x2="3.01" y2="12" /><line x1="3" y1="18" x2="3.01" y2="18" /></svg>;
  }
}

function getOccupantFilterTabIcon(filter: OccupantFilter) {
  const props = {
    width: 15,
    height: 15,
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 2.2,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
  };

  switch (filter) {
    case 'active':
      return (
        <svg {...props}>
          <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
          <circle cx="9" cy="7" r="4" />
          <polyline points="16 11 18 13 22 9" />
        </svg>
      );
    case 'pending':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <polyline points="12 6 12 12 16 14" />
        </svg>
      );
    case 'left':
      return (
        <svg {...props}>
          <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
          <polyline points="16 17 21 12 16 7" />
          <line x1="21" y1="12" x2="9" y2="12" />
        </svg>
      );
    default:
      return (
        <svg {...props}>
          <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
          <circle cx="9" cy="7" r="4" />
          <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
          <path d="M16 3.13a4 4 0 0 1 0 7.75" />
        </svg>
      );
  }
}

export default function RoomDetailPage() {
  const { id, roomId } = useParams<{ id: string; roomId?: string }>();
  const navigate = useNavigate();
  const { currentUser } = useAuth();

  const [house, setHouse] = useState<RoomingHouseDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [pageError, setPageError] = useState('');
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);

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
  const [occupantFilter, setOccupantFilter] = useState<OccupantFilter>('all');
  const [tabLoading, setTabLoading] = useState(false);
  const [contractActionError, setContractActionError] = useState<string | null>(null);
  const [isFileActionLoading, setIsFileActionLoading] = useState(false);
  const [isTerminateModalOpen, setIsTerminateModalOpen] = useState(false);
  const [appendices, setAppendices] = useState<ContractAppendixResponse[] | null>(null);
  const [appendicesError, setAppendicesError] = useState<string | null>(null);
  const [accessibleContractFiles, setAccessibleContractFiles] = useState<ContractFileResponse[]>([]);
  const [appendixFilesError, setAppendixFilesError] = useState<string | null>(null);
  const [isAppendixModalOpen, setIsAppendixModalOpen] = useState(false);
  const [editingAppendix, setEditingAppendix] = useState<ContractAppendixResponse | null>(null);
  const [signingAppendixId, setSigningAppendixId] = useState<string | null>(null);
  const [contractInvoices, setContractInvoices] = useState<Invoice[]>([]);
  const [invoiceTabLoading, setInvoiceTabLoading] = useState(false);
  const [invoiceStatusFilter, setInvoiceStatusFilter] = useState('');
  const [isCreateInvoiceModalOpen, setIsCreateInvoiceModalOpen] = useState(false);

  const visibleContractInvoices = useMemo(() => {
    if (!invoiceStatusFilter) return contractInvoices;
    return contractInvoices.filter((invoice) => invoice.status === invoiceStatusFilter);
  }, [contractInvoices, invoiceStatusFilter]);

  const occupants = useMemo(() => (
    [...activeTenants].sort((left, right) =>
      new Date(left.moveInDate).getTime() - new Date(right.moveInDate).getTime()
    )
  ), [activeTenants]);

  const filteredOccupants = useMemo(() => occupants.filter((occupant) => {
    if (occupantFilter === 'active') return occupant.status === 'Active';
    if (occupantFilter === 'pending') return occupant.status === 'PendingMoveIn';
    if (occupantFilter === 'left') return occupant.status !== 'Active' && occupant.status !== 'PendingMoveIn';
    return true;
  }), [occupants, occupantFilter]);

  const pendingChangesAlertInfo = useMemo(() => {
    if (!appendices) return null;
    const dueAppendices = appendices.filter((a) => a.status === 'Active' && !a.appliedAt);
    if (dueAppendices.length === 0) return null;

    let hasPendingOccupantChanges = false;
    let hasPendingContractChanges = false;
    let earliestPendingDate: string | null = null;

    for (const appendix of dueAppendices) {
      if (!earliestPendingDate || new Date(appendix.effectiveDate) < new Date(earliestPendingDate)) {
        earliestPendingDate = appendix.effectiveDate;
      }
      for (const change of appendix.changes) {
        if (change.targetType === 'ContractOccupant' || change.fieldName === 'MainTenantUserId') {
          hasPendingOccupantChanges = true;
        } else if (change.targetType === 'Contract') {
          hasPendingContractChanges = true;
        }
      }
    }

    return {
      hasPendingOccupantChanges,
      hasPendingContractChanges,
      earliestPendingDate,
    };
  }, [appendices]);

  useEffect(() => {
    if (!id) return;
    loadData();
  }, [id, roomId]);

  useEffect(() => {
    if (!roomId || !selectedRoom) return;

    if (mainTab !== 'tenants' && mainTab !== 'contracts' && mainTab !== 'invoices') return;

    if (selectedRoom.status !== 'Occupied' && selectedRoom.status !== 'Reserved') {
      setActiveContract(null);
      setActiveTenants([]);
      setAppendices(null);
      setAccessibleContractFiles([]);
      setAppendixFilesError(null);
      setContractInvoices([]);
      return;
    }

    let isCancelled = false;

    async function loadActiveRoomContractData() {
      setTabLoading(true);
      setToast(null);

      try {
        if (mainTab === 'tenants') {
          const [contract, tenants] = await Promise.all([
            getActiveContractByRoomId(roomId!),
            getActiveTenantsByRoomId(roomId!),
          ]);

          if (isCancelled) return;
          setActiveContract(contract);
          setActiveTenants(tenants);
          await refreshAppendices(contract.id);
          return;
        }

        const contract = await getActiveContractByRoomId(roomId!);

        if (isCancelled) return;
        setActiveContract(contract);
        if (mainTab === 'invoices') {
          await Promise.all([
            loadContractInvoices(contract.id),
            refreshAppendices(contract.id),
          ]);
          return;
        }

        await refreshAppendices(contract.id);
      } catch (err) {
        if (isCancelled) return;
        setActiveContract(null);
        setActiveTenants([]);
        setContractInvoices([]);
        setAppendices([]);
        setAppendicesError(getApiErrorMessage(err, 'Không thể tải danh sách phụ lục.'));
        setToast({ message: getApiErrorMessage(err, 'Không thể tải dữ liệu hợp đồng đang active của phòng.'), type: 'error' });
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
    setToast(null);
    try {
      const houseData = await getRoomingHouseDetail(id!);

      if (houseData.approvalStatus !== 'Approved') {
        setPageError('Khu trọ này chưa được quản trị viên phê duyệt. Không thể truy cập quản lý phòng.');
        setHouse(null);
        return;
      }

      if (!houseData.rentalPolicy || !houseData.houseRule) {
        setPageError('Vui lòng hoàn thành Chính Sách Thuê và Luật Khu Trọ trước khi truy cập trang này.');
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

        try {
          const contract = await getActiveContractByRoomId(roomId);
          setActiveContract(contract);
        } catch {
          setActiveContract(null);
        }

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
      setToast({ message: getApiErrorMessage(err, 'Không thể tải thông tin phòng.'), type: 'error' });
    } finally {
      setLoading(false);
    }
  }

  async function loadContractInvoices(contractId: string) {
    setInvoiceTabLoading(true);
    try {
      const response = await billingApi.getLandlordInvoices({ contractId });
      setContractInvoices(response.data);
    } catch (err) {
      setContractInvoices([]);
      setToast({ message: getApiErrorMessage(err, 'Không thể tải danh sách hóa đơn của hợp đồng.'), type: 'error' });
    } finally {
      setInvoiceTabLoading(false);
    }
  }

  async function handleSaveRoomBasic() {
    if (!house) return;
    if (isRoomEditLocked(selectedRoom)) {
      setToast({ message: 'Không thể chỉnh sửa thông tin phòng khi phòng đang được giữ chỗ hoặc đang được thuê.', type: 'info' });
      return;
    }
    setActionLoading(true);
    setToast(null);
    try {
      if (selectedRoom) {
        const updated = await updateRoom(selectedRoom.id, roomForm);
        setSelectedRoom(updated);
        setToast({ message: 'Đã lưu thông tin cơ bản phòng.', type: 'success' });
      }
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể lưu thông tin phòng.'), type: 'error' });
    } finally {
      setActionLoading(false);
    }
  }

  async function handleSaveRoomImages() {
    if (!selectedRoom) return;
    setActionLoading(true);
    setToast(null);
    try {
      const updated = await updateRoomImages(selectedRoom.id, cleanImages(roomImages));
      setSelectedRoom(updated);
      setRoomImages(toImageRequests(updated.images));
      setToast({ message: 'Đã lưu ảnh phòng thành công.', type: 'success' });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể lưu ảnh phòng.'), type: 'error' });
    } finally {
      setActionLoading(false);
    }
  }

  async function handleSaveRoomAmenities() {
    if (!selectedRoom) return;
    setActionLoading(true);
    setToast(null);
    try {
      const updated = await updateRoomAmenities(selectedRoom.id, roomAmenityIds);
      setSelectedRoom(updated);
      setRoomAmenityIds(updated.amenities.map(a => a.id));
      setToast({ message: 'Đã cập nhật tiện ích phòng thành công.', type: 'success' });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể lưu tiện ích phòng.'), type: 'error' });
    } finally {
      setActionLoading(false);
    }
  }

  async function handleSaveRoomPrice() {
    if (!selectedRoom) return;
    if (isRoomEditLocked(selectedRoom)) {
      setToast({ message: 'Không thể chỉnh sửa bảng giá phòng khi phòng đang được giữ chỗ hoặc đang được thuê.', type: 'info' });
      return;
    }
    setActionLoading(true);
    setToast(null);
    try {
      const updated = await updateRoomPriceTiers(selectedRoom.id, priceTiers);
      setSelectedRoom(updated);
      setPriceTiers(updated.priceTiers.map(t => ({ occupantCount: t.occupantCount, monthlyRent: t.monthlyRent, isActive: t.isActive })));
      setToast({ message: 'Đã cập nhật bảng giá phòng thành công.', type: 'success' });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể lưu bảng giá phòng.'), type: 'error' });
    } finally {
      setActionLoading(false);
    }
  }

  async function handlePublishRoom() {
    if (!selectedRoom) return;
    setActionLoading(true);
    setToast(null);
    try {
      const updated = await submitRoom(selectedRoom.id);
      setSelectedRoom(updated);
      setToast({ message: 'Phòng đã được hiển thị hoạt động và sẵn sàng cho thuê.', type: 'success' });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể hiển thị hoạt động phòng.'), type: 'error' });
    } finally {
      setActionLoading(false);
    }
  }

  async function handleToggleRoomMaintenance() {
    if (!selectedRoom) return;

    const nextStatus = selectedRoom.status === 'Available' ? 'Maintenance' : 'Available';
    setActionLoading(true);
    setToast(null);
    try {
      const updated = await updateRoomStatus(selectedRoom.id, nextStatus);
      setSelectedRoom(updated);
      setToast({ message: nextStatus === 'Maintenance' ? 'Phòng đã được tạm ngưng hiển thị.' : 'Phòng đã được mở lại và có thể nhận thuê.', type: 'success' });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể cập nhật trạng thái phòng.'), type: 'error' });
    } finally {
      setActionLoading(false);
    }
  }

  async function refreshAppendices(contractId: string) {
    setAppendicesError(null);
    setAppendixFilesError(null);
    setAccessibleContractFiles([]);
    try {
      const response = await contractApi.getAppendices(contractId);
      setAppendices(response.data ?? []);
      setAppendicesError(null);
    } catch (err) {
      setAppendices([]);
      setAppendicesError(getApiErrorMessage(err, 'Không thể tải danh sách phụ lục.'));
    }

    try {
      setAccessibleContractFiles(await loadAccessibleContractFiles(contractId));
    } catch (err) {
      setAccessibleContractFiles([]);
      setAppendixFilesError(getApiErrorMessage(err, 'Không thể tải danh sách file phụ lục đã ký.'));
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

      if (mode === 'view') {
        await openContractFileForView(activeContract.id, file);
        return;
      }

      const blob = await contractApi.downloadContractFile(activeContract.id, file.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${activeContract.contractNumber}-${file.purpose.toLowerCase()}.pdf`;
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
  const roomEditLocked = isRoomEditLocked(selectedRoom);
  const canToggleRoomMaintenance = selectedRoom?.status === 'Available' || selectedRoom?.status === 'Maintenance';

  const displayMessage = (
    selectedRoom?.status === 'Available'
      ? 'Phòng đã được hiển thị hoạt động và sẵn sàng cho thuê.'
      : selectedRoom?.status === 'Maintenance'
        ? 'Phòng đang tạm ngưng hiển thị.'
        : selectedRoom?.status === 'Hidden'
          ? 'Phòng đang ẩn và chưa sẵn sàng cho thuê.'
          : selectedRoom?.status === 'Occupied'
            ? 'Phòng đang được thuê và hoạt động bình thường.'
            : selectedRoom?.status === 'Reserved'
              ? 'Phòng đã được giữ chỗ.'
              : ''
  );

  if (loading) {
    return (
      <div className="rooming-house-detail-page" style={{ display: 'contents' }}>
        <main className="dashboard-main">
          <div className="empty-panel">Đang tải thông tin phòng...</div>
        </main>
      </div>
    );
  }

  if (!house || !selectedRoom) {
    return (
      <div className="rooming-house-detail-page" style={{ display: 'contents' }}>
        <main className="dashboard-main">
          <div className="empty-panel">
            <h2>Lỗi truy cập</h2>
            <p>{pageError || 'Không thể truy cập thông tin phòng.'}</p>
            <button className="primary-action" onClick={() => navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSE_DETAIL(id!))}>
              Quay lại danh sách phòng
            </button>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="rooming-house-detail-page" style={{ display: 'contents' }}>
      <main className="dashboard-main">
        <PageHeader
          className="page-header-band--flat-bottom"
          onBack={() => navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSE_DETAIL(id!))}
          icon={
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#2563eb" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                <polyline points="9 22 9 12 15 12 15 22" />
                <rect x="10" y="14" width="4" height="4" />
              </svg>
            </div>
          }
          eyebrow={
            <div style={{ display: 'flex', alignItems: 'center', gap: '4px', color: '#2563eb', fontSize: '11px', fontWeight: 600, textTransform: 'uppercase', marginBottom: '4px' }}>
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                <circle cx="12" cy="10" r="3" />
              </svg>
              {house.addressDisplay}
            </div>
          }
          title={`Phòng ${selectedRoom?.roomNumber} - ${house.name}`}
          description={
            <div className="overview-meta-list" style={{ display: 'flex', alignItems: 'center', gap: '16px', flexWrap: 'wrap', marginTop: '6px' }}>
              <span className="meta-item" style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', fontSize: '13.5px', color: '#64748b' }}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                  <line x1="9" y1="3" x2="9" y2="21" />
                  <line x1="15" y1="3" x2="15" y2="21" />
                  <line x1="3" y1="9" x2="21" y2="9" />
                  <line x1="3" y1="15" x2="21" y2="15" />
                </svg>
                Diện tích: <strong>{selectedRoom?.areaM2 ? `${selectedRoom.areaM2} m²` : 'Chưa nhập'}</strong>
              </span>
              <span style={{ color: '#cbd5e1' }}>|</span>
              <span className="meta-item" style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', fontSize: '13.5px', color: '#64748b' }}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="4" y="2" width="16" height="20" rx="2" ry="2" />
                  <line x1="9" y1="22" x2="9" y2="16" />
                  <line x1="9" y1="16" x2="15" y2="16" />
                  <line x1="15" y1="16" x2="15" y2="22" />
                  <line x1="12" y1="6" x2="12" y2="6.01" strokeWidth="2" />
                  <line x1="12" y1="10" x2="12" y2="10.01" strokeWidth="2" />
                </svg>
                Tầng: <strong>{selectedRoom?.floor}</strong>
              </span>
              <span style={{ color: '#cbd5e1' }}>|</span>
              <span className="meta-item" style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', fontSize: '13.5px', color: '#64748b' }}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                  <circle cx="9" cy="7" r="4" />
                  <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                  <path d="M16 3.13a4 4 0 0 1 0 7.75" />
                </svg>
                Tối đa: <strong>{selectedRoom?.maxOccupants} người</strong>
              </span>
            </div>
          }
          rightContent={
            <div className="overview-right">
              <div className="overview-stats" style={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'center', gap: '8px', marginBottom: '12px' }}>
                <span
                  className={`status-pill ${getStatusToneClass(selectedRoom.status)}`}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    padding: '6px 14px',
                    fontSize: '13.5px',
                    fontWeight: 600,
                    borderRadius: '999px',
                    border: '1px solid currentColor',
                    gap: '6px',
                    background: selectedRoom.status === 'Available' ? '#effaf3' : undefined,
                    color: selectedRoom.status === 'Available' ? '#10b981' : undefined,
                    borderColor: selectedRoom.status === 'Available' ? '#a7f3d0' : 'currentColor',
                  }}
                >
                  <span style={{
                    width: '6px',
                    height: '6px',
                    borderRadius: '50%',
                    backgroundColor: 'currentColor',
                    display: 'inline-block'
                  }} />
                  {formatStatus(selectedRoom.status)}
                </span>
                {selectedRoom.status === 'Hidden' ? (
                  <Button
                    type="button"
                    variant="outline"
                    onClick={handlePublishRoom}
                    disabled={actionLoading}
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '6px',
                      borderColor: '#cbd5e1',
                      color: '#0f172a',
                      fontSize: '13.5px',
                      fontWeight: 600,
                      padding: '6px 14px',
                      borderRadius: '999px',
                      minHeight: '34px',
                      backgroundColor: '#ffffff',
                      whiteSpace: 'nowrap',
                    }}
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                      <circle cx="12" cy="12" r="3" />
                    </svg>
                    Hiển thị phòng
                  </Button>
                ) : canToggleRoomMaintenance ? (
                  <Button
                    type="button"
                    variant="outline"
                    onClick={handleToggleRoomMaintenance}
                    disabled={actionLoading}
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '6px',
                      borderColor: '#cbd5e1',
                      color: '#0f172a',
                      fontSize: '13.5px',
                      fontWeight: 600,
                      padding: '6px 14px',
                      borderRadius: '999px',
                      minHeight: '34px',
                      backgroundColor: '#ffffff',
                      whiteSpace: 'nowrap',
                    }}
                  >
                    {selectedRoom.status === 'Available' ? (
                      <>
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                          <circle cx="12" cy="12" r="10" />
                          <line x1="10" y1="9" x2="10" y2="15" />
                          <line x1="14" y1="9" x2="14" y2="15" />
                        </svg>
                        Tạm ngưng
                      </>
                    ) : (
                      <>
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                          <circle cx="12" cy="12" r="10" />
                          <polygon points="10 8 16 12 10 16 10 8" />
                        </svg>
                        Mở lại phòng
                      </>
                    )}
                  </Button>
                ) : null}
              </div>
            </div>
          }
        />

        {/* === TAB CẤP 1 (dính vào dưới header) === */}
        <Tabs
          className="attached-top"
          variant="segmented-primary"
          activeId={mainTab}
          onChange={(id) => setMainTab(id as RoomMainTab)}
          items={[
            {
              id: 'room-info',
              label: 'Thông tin phòng',
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
              id: 'tenants',
              label: 'Người ở',
              disabled: !activeContract,
              title: !activeContract ? 'Phòng phải có hợp đồng đang hiệu lực mới có thể quản lý người ở' : undefined,
              icon: (
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                  <circle cx="9" cy="7" r="4" />
                  <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                  <path d="M16 3.13a4 4 0 0 1 0 7.75" />
                </svg>
              ),
            },
            {
              id: 'contracts',
              label: 'Hợp đồng',
              disabled: !activeContract,
              title: !activeContract ? 'Phòng phải có hợp đồng đang hiệu lực mới có thể xem' : undefined,
              icon: (
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M9 11l3 3L22 4" />
                  <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
                </svg>
              ),
            },
            {
              id: 'invoices',
              label: 'Hóa đơn',
              disabled: !activeContract,
              title: !activeContract ? 'Phòng phải có hợp đồng đang hiệu lực mới có thể quản lý hóa đơn' : undefined,
              icon: (
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="12" y1="1" x2="12" y2="23" />
                  <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                </svg>
              ),
            },
          ]}
        />

        {displayMessage && (
          <div className={`rooming-house-rule-editor__alert ${displayMessage.toLowerCase().includes('không thể') || displayMessage.toLowerCase().includes('lỗi')
            ? 'rooming-house-rule-editor__alert--danger'
            : displayMessage.toLowerCase().includes('vui lòng') || displayMessage.toLowerCase().includes('chưa') || displayMessage.toLowerCase().includes('phòng đã được') || displayMessage.toLowerCase().includes('tạm ngưng')
              ? 'rooming-house-rule-editor__alert--warning'
              : 'rooming-house-rule-editor__alert--success'
            }`} style={{ marginTop: '20px', marginBottom: '10px' }}>
            {displayMessage.toLowerCase().includes('không thể') || displayMessage.toLowerCase().includes('lỗi') ? (
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
                <circle cx="12" cy="12" r="10" />
                <line x1="15" y1="9" x2="9" y2="15" />
                <line x1="9" y1="9" x2="15" y2="15" />
              </svg>
            ) : displayMessage.toLowerCase().includes('vui lòng') || displayMessage.toLowerCase().includes('chưa') || displayMessage.toLowerCase().includes('phòng đã được') || displayMessage.toLowerCase().includes('tạm ngưng') ? (
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
                <circle cx="12" cy="12" r="10" />
                <line x1="12" y1="16" x2="12" y2="12" />
                <line x1="12" y1="8" x2="12.01" y2="8" />
              </svg>
            ) : (
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
                <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
                <polyline points="22 4 12 14.01 9 11.01" />
              </svg>
            )}
            <span>{displayMessage}</span>
          </div>
        )}
        {actionLoading && <p className="dashboard-message" style={{ background: '#dbeafe', color: '#1e40af', marginTop: '10px' }}>Đang lưu thay đổi...</p>}

        {mainTab === 'room-info' && (
          <Tabs
            className="attached-bottom"
            variant="segmented-secondary"
            activeId={roomActiveTab}
            onChange={(id) => setRoomActiveTab(id as RoomTab)}
            items={[
              {
                id: 'basic',
                label: 'Th\u00f4ng tin c\u01a1 b\u1ea3n',
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
                label: '\u1ea2nh ph\u00f2ng',
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
                label: 'Ti\u1ec7n \u00edch ph\u00f2ng',
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
                label: 'B\u1ea3ng gi\u00e1',
                icon: (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z" />
                    <line x1="7" y1="7" x2="7.01" y2="7" strokeWidth="2.5" />
                  </svg>
                ),
              },
            ]}
          />
        )}

        {mainTab === 'room-info' && (
          <div className="editor-panel tab-attached-panel tab-attached-panel--compact">

            {/* ROOM TAB 1: THÔNG TIN CƠ BẢN PHÒNG */}
            {roomActiveTab === 'basic' && (
              <div className="form-grid">
                {roomEditLocked && (
                  <div className="empty-panel compact" style={{ gridColumn: '1 / -1' }}>
                    Không thể chỉnh sửa thông tin cơ bản khi phòng đang được giữ chỗ hoặc đang được thuê.
                  </div>
                )}
                <label className="field">
                  <span>Số phòng / Tên phòng</span>
                  <input disabled={roomEditLocked} value={roomForm.roomNumber} onChange={e => setRoomForm({ ...roomForm, roomNumber: e.target.value })} />
                </label>

                <label className="field">
                  <span>Tầng</span>
                  <input
                    type="number"
                    disabled={roomEditLocked}
                    value={roomForm.floor}
                    onChange={e => setRoomForm({ ...roomForm, floor: Number(e.target.value) || 1 })}
                  />
                </label>

                <label className="field">
                  <span>Diện tích (m²)</span>
                  <input
                    type="number"
                    disabled={roomEditLocked}
                    value={roomForm.areaM2 ?? ''}
                    onChange={e => setRoomForm({ ...roomForm, areaM2: e.target.value === '' ? null : Number(e.target.value) })}
                  />
                </label>

                <label className="field">
                  <span>Số khách tối đa</span>
                  <input
                    type="number"
                    disabled={roomEditLocked}
                    value={roomForm.maxOccupants}
                    onChange={e => setRoomForm({ ...roomForm, maxOccupants: Number(e.target.value) || 1 })}
                  />
                </label>

                <label className="field checkbox-field" style={{ gridColumn: '1 / -1', display: 'flex', alignItems: 'center', gap: '8px', marginTop: '8px' }}>
                  <input
                    type="checkbox"
                    disabled={roomEditLocked}
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
                    disabled={roomEditLocked}
                    value={roomForm.description ?? ''}
                    onChange={e => setRoomForm({ ...roomForm, description: e.target.value })}
                  />
                </label>

                <div className="save-row">
                  <button className="primary-action" onClick={handleSaveRoomBasic} disabled={roomEditLocked || actionLoading}>
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }}>
                      <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                      <polyline points="17 21 17 13 7 13 7 21" />
                      <polyline points="7 3 7 8 15 8" />
                    </svg>
                    Lưu thông tin
                  </button>
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
              <>
                {roomEditLocked && (
                  <div className="empty-panel compact" style={{ marginBottom: '16px' }}>
                    Không thể chỉnh sửa bảng giá phòng khi phòng đang được giữ chỗ hoặc đang được thuê.
                  </div>
                )}
                <PriceTierEditor
                  priceTiers={priceTiers}
                  isTieredPricing={selectedRoom.isTieredPricing}
                  maxOccupants={selectedRoom.maxOccupants}
                  onChange={setPriceTiers}
                  onSave={handleSaveRoomPrice}
                  depositMonths={house?.rentalPolicy?.depositMonths}
                  disabled={roomEditLocked}
                />
              </>
            )}
          </div>
        )}

        {/* TAB 2: NGƯỜI Ở */}
        {mainTab === 'tenants' && (
          <div className="history-detail-secondary-section">
            <div className="contract-invoices-header history-detail-section-heading">
              <div>
                <h2>Thông tin người ở</h2>
                <p>Danh sách người ở của hợp đồng active trong phòng này.</p>
              </div>
            </div>

            <Tabs
              className="attached-bottom"
              variant="segmented-secondary"
              activeId={occupantFilter}
              onChange={(filter) => setOccupantFilter(filter as OccupantFilter)}
              items={[
                { id: 'all', label: `Tất cả (${occupants.length})`, icon: getOccupantFilterTabIcon('all') },
                { id: 'active', label: `Đang ở (${occupants.filter((occupant) => occupant.status === 'Active').length})`, icon: getOccupantFilterTabIcon('active') },
                { id: 'pending', label: `Chờ dọn vào (${occupants.filter((occupant) => occupant.status === 'PendingMoveIn').length})`, icon: getOccupantFilterTabIcon('pending') },
                { id: 'left', label: `Đã rời đi / Đã hủy (${occupants.filter((occupant) => occupant.status !== 'Active' && occupant.status !== 'PendingMoveIn').length})`, icon: getOccupantFilterTabIcon('left') },
              ]}
            />

            <div className="history-detail-content tab-attached-panel tab-attached-panel--compact">
              {tabLoading ? (
                <div className="coming-soon-placeholder">
                  <p>Đang tải danh sách người ở...</p>
                </div>
              ) : (
                <>
                {pendingChangesAlertInfo?.hasPendingOccupantChanges && (
                  <div style={{ backgroundColor: '#fffbeb', border: '1px solid #fde68a', color: '#b45309', padding: '12px 16px', borderRadius: '8px', marginBottom: '16px', display: 'flex', alignItems: 'flex-start', gap: '8px' }}>
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0, marginTop: '2px' }}>
                      <circle cx="12" cy="12" r="10"></circle>
                      <line x1="12" y1="8" x2="12" y2="12"></line>
                      <line x1="12" y1="16" x2="12.01" y2="16"></line>
                    </svg>
                    <div>
                      <strong>Thay đổi chờ áp dụng:</strong> Đang có phụ lục thay đổi thông tin người ở đã có hiệu lực nhưng chưa được hệ thống cập nhật. Hệ thống sẽ tự động cập nhật trong ít phút tới.
                    </div>
                  </div>
                )}

                <div className="occupant-list">
                  {filteredOccupants.map((occupant) => (
                      <div key={occupant.id} className="occupant-item">
                        <div className="occupant-info">
                          <h4>{occupant.fullName} - {formatOccupantRole(Boolean(activeContract && occupant.userId === activeContract.mainTenantUserId))}</h4>
                          <div className="occupant-role">Email: {formatOptionalContact(occupant.email)}</div>
                          <div className="occupant-role">Số điện thoại: {formatOptionalContact(occupant.phoneNumber)}</div>
                        </div>
                        <div className="occupant-dates">
                          <div style={{ marginBottom: '6px' }}>
                            <span className={`status-badge ${occupant.status === 'Active' ? 'success' : occupant.status === 'PendingMoveIn' ? 'warning' : 'danger'}`} style={{ padding: '2px 8px', fontSize: '0.75rem' }}>
                              {occupant.status === 'Active' ? 'Đang ở' : occupant.status === 'PendingMoveIn' ? 'Chờ dọn vào' : occupant.status === 'Voided' ? 'Đã hủy' : 'Đã rời đi'}
                            </span>
                          </div>
                          <div><strong>Vào:</strong> {formatDateVi(occupant.moveInDate)}</div>
                          {occupant.moveOutDate && (
                            <div style={{ color: '#94a3b8' }}><strong>Rời đi:</strong> {formatDateVi(occupant.moveOutDate)}</div>
                          )}
                        </div>
                      </div>
                    ))}
                  {filteredOccupants.length === 0 && (
                    <div style={{ textAlign: 'center', padding: '20px', color: '#64748b' }}>Không có người ở nào khớp với bộ lọc.</div>
                  )}
                </div>
                </>
              )}
            </div>
          </div>
        )}

        {/* TAB 3: HỢP ĐỒNG */}
        {mainTab === 'contracts' && (
          <div style={{ marginTop: '20px' }}>
            {tabLoading && <p>Đang tải thông tin hợp đồng...</p>}
            {!tabLoading && activeContract && (
              <>
                <div className="history-detail-content">
                  {pendingChangesAlertInfo?.hasPendingContractChanges && (
                    <div style={{ backgroundColor: '#fffbeb', border: '1px solid #fde68a', color: '#b45309', padding: '12px 16px', borderRadius: '8px', marginBottom: '16px', display: 'flex', alignItems: 'flex-start', gap: '8px' }}>
                      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0, marginTop: '2px' }}>
                        <circle cx="12" cy="12" r="10"></circle>
                        <line x1="12" y1="8" x2="12" y2="12"></line>
                        <line x1="12" y1="16" x2="12.01" y2="16"></line>
                      </svg>
                      <div>
                        <strong>Thay đổi chờ áp dụng:</strong> Đang có phụ lục thay đổi thông tin hợp đồng/tài chính đã có hiệu lực nhưng chưa được hệ thống cập nhật. Hệ thống sẽ tự động cập nhật trong ít phút tới.
                      </div>
                    </div>
                  )}
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
                </div>

                <div className="history-detail-content" style={{ marginTop: '20px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                    <h2 style={{ margin: 0, fontSize: '1.25rem' }}>Danh sách phụ lục</h2>
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
                  {appendixFilesError && (
                    <div style={{ color: '#b91c1c', marginBottom: '16px', padding: '12px', background: '#fef2f2', borderRadius: '8px' }}>
                      {appendixFilesError}
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
                            <AppendixFileActions
                              contractId={activeContract.id}
                              contractNumber={activeContract.contractNumber}
                              appendix={appendix}
                              file={findAccessibleAppendixFile(accessibleContractFiles, appendix.id)}
                            />
                          </div>
                          <div className="appendix-status-badge" style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '8px' }}>
                            <span className={`status-badge ${getAppendixStatusClass(appendix.status)}`} style={{ padding: '6px 16px', fontSize: '0.85rem' }}>
                              {formatAppendixStatus(appendix, 'Landlord')}
                            </span>
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              </>
            )}
            {!tabLoading && !activeContract && (
              <div className="history-detail-content empty-panel">
                <p>Không tìm thấy hợp đồng đang active cho phòng này.</p>
              </div>
            )}
          </div>
        )}

        {/* TAB 4: HÓA ĐƠN */}
        {mainTab === 'invoices' && (
          <div className="history-detail-secondary-section">
            <div className="contract-invoices-header history-detail-section-heading">
              <div>
                <h2>Hóa đơn hằng tháng</h2>
                <p>Danh sách hóa đơn hằng tháng thuộc hợp đồng active của phòng này.</p>
              </div>
              <Button
                type="button"
                onClick={() => setIsCreateInvoiceModalOpen(true)}
                disabled={!activeContract || invoiceTabLoading}
              >
                Tạo hóa đơn
              </Button>
            </div>

            <Tabs
              className="attached-bottom"
              variant="segmented-secondary"
              activeId={invoiceStatusFilter || 'all'}
              onChange={(status) => setInvoiceStatusFilter(status === 'all' ? '' : status)}
              items={invoiceStatusTabs.map((status) => ({
                id: status || 'all',
                label: status ? formatInvoiceStatus(status) : 'Tất cả',
                icon: getInvoiceStatusTabIcon(status),
              }))}
            />

            <div className="history-detail-content tab-attached-panel tab-attached-panel--compact">
              {tabLoading || invoiceTabLoading ? (
                <div className="coming-soon-placeholder">
                  <p>Đang tải hóa đơn...</p>
                </div>
              ) : !activeContract ? (
                <div className="coming-soon-placeholder">
                  <h2>Chưa có hợp đồng</h2>
                  <p>Không tìm thấy hợp đồng active cho phòng này.</p>
                </div>
              ) : visibleContractInvoices.length === 0 ? (
                <div className="coming-soon-placeholder">
                  <h2>Chưa có hóa đơn</h2>
                  <p>Chưa có hóa đơn nào phù hợp với bộ lọc hiện tại.</p>
                </div>
              ) : (
                <div className="contract-invoice-list">
                  {visibleContractInvoices.map((invoice) => (
                    <div key={invoice.id} className={`contract-invoice-card ${invoice.status === 'Cancelled' ? 'muted' : ''}`}>
                      <div>
                        <div className="contract-invoice-title">
                          <strong>{invoice.invoiceNo}</strong>
                          <span className={`status-badge ${getInvoiceStatusClass(invoice.status)}`}>
                            {formatInvoiceStatus(invoice.status)}
                          </span>
                        </div>
                        <div className="contract-invoice-meta">
                          <span>Kỳ: {formatDateVi(invoice.billingPeriodStart)} - {formatDateVi(invoice.billingPeriodEnd)}</span>
                          <span>Hạn thanh toán: {formatDateVi(invoice.dueDate)}</span>
                          <span>Người đứng tên: {invoice.tenantName}</span>
                        </div>
                      </div>
                      <div className="contract-invoice-actions">
                        <strong>{formatMoneyString(invoice.totalAmount)} đ</strong>
                        <Button
                          type="button"
                          variant="outline"
                          onClick={() => navigate(ROUTE_PATHS.LANDLORD.INVOICE_DETAIL(invoice.id))}
                        >
                          Xem chi tiết
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}

        {false && mainTab === 'invoices' && (
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
            setToast({ message: 'Đã chấm dứt hợp đồng.', type: 'success' });
          }}
        />
      )}

      {isCreateInvoiceModalOpen && activeContract && (
        <CreateInvoiceWithReadingsModal
          contract={activeContract}
          appendices={appendices ?? []}
          onClose={() => setIsCreateInvoiceModalOpen(false)}
          onCreated={(invoice) => {
            setIsCreateInvoiceModalOpen(false);
            setToast({ message: `Đã tạo hóa đơn nháp ${invoice.invoiceNo}.`, type: 'success' });
            void loadContractInvoices(activeContract.id);
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
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}

// ─── Subcomponents ──────────────────────────────────────────────────────────

type ReadingDraft = {
  previousReading: number;
  hasPreviousReading: boolean;
  currentReading: number;
  hasCurrentReading: boolean;
  proofMediaAssetId?: string | null;
  proofImageUrl: string;
  aiReading: number | null;
  aiRawText: string;
};

const emptyInvoiceReadingDraft: ReadingDraft = {
  previousReading: 0,
  hasPreviousReading: false,
  currentReading: 0,
  hasCurrentReading: false,
  proofMediaAssetId: null,
  proofImageUrl: '',
  aiReading: null,
  aiRawText: '',
};

function CreateInvoiceWithReadingsModal({
  contract,
  appendices,
  onClose,
  onCreated,
}: {
  contract: ContractDetailResponse;
  appendices: ContractAppendixResponse[];
  onClose: () => void;
  onCreated: (invoice: Invoice) => void;
}) {
  const defaultMonth = getDefaultInvoiceMonth(contract);
  const [billingMonth, setBillingMonth] = useState(defaultMonth);
  const [preview, setPreview] = useState<RoomInvoicePreview | null>(null);
  const [prices, setPrices] = useState<ServicePrice[]>([]);
  const [readings, setReadings] = useState<Record<string, ReadingDraft>>({});
  const [discountAmount, setDiscountAmount] = useState(0);
  const [note, setNote] = useState('');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [uploadingServiceId, setUploadingServiceId] = useState('');
  const [error, setError] = useState('');
  const latestReadingByServiceType = Object.fromEntries(
    (preview?.meteredServices ?? []).map((service) => [service.serviceTypeId, service.latestReading ?? null])
  );

  useEffect(() => {
    let cancelled = false;

    async function loadPrices() {
      setLoading(true);
      setError('');
      try {
        const priceResponse = await billingApi.getServicePrices(contract.roomingHouseId);
        if (cancelled) return;

        setPrices(priceResponse.data);
      } catch (err) {
        if (!cancelled) {
          setError(getApiErrorMessage(err, 'Không thể tải bảng giá dịch vụ.'));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void loadPrices();

    return () => {
      cancelled = true;
    };
  }, [contract.roomId, contract.roomingHouseId]);

  const period = useMemo(
    () => resolveInvoicePeriodForContract(billingMonth, contract),
    [billingMonth, contract]
  );
  const fixedPrices = preview?.fixedServices ?? [];
  const meteredPrices = preview?.meteredServices ?? [];
  const periodValidationMessage = !period
    ? 'Tháng hóa đơn không nằm trong thời hạn hợp đồng.'
    : '';
  const previewBlockReason = preview && !preview.canGenerate
    ? preview.blockReason || 'Chưa thể tạo hóa đơn cho kỳ này.'
    : '';
  useEffect(() => {
    if (!period) {
      setPreview(null);
      return;
    }

    let cancelled = false;

    async function loadPreview() {
      setLoading(true);
      setError('');
      try {
        const response = await billingApi.getRoomInvoicePreview(contract.roomId, {
          billingPeriodStart: period!.start,
          billingPeriodEnd: period!.end,
        });
        if (cancelled) return;

        setPreview(response.data);
        const nextReadings: Record<string, ReadingDraft> = {};
        response.data.meteredServices.forEach((service) => {
          const latestReading = service.latestReading;
          nextReadings[service.serviceTypeId] = {
            ...emptyInvoiceReadingDraft,
            previousReading: latestReading?.currentReading ?? 0,
            hasPreviousReading: Boolean(latestReading),
            currentReading: latestReading?.currentReading ?? 0,
            hasCurrentReading: false,
            proofMediaAssetId: null,
          };
        });
        setReadings(nextReadings);
      } catch (err) {
        if (!cancelled) {
          setPreview(null);
          setError(getApiErrorMessage(err, 'Không thể tải thông tin xem trước hóa đơn.'));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void loadPreview();

    return () => {
      cancelled = true;
    };
  }, [contract.roomId, period?.start, period?.end]);

  const resolvedMonthlyRent = preview?.monthlyRent ?? (period
    ? resolveMonthlyRentFromAppendices(contract.monthlyRent, appendices, period.start)
    : contract.monthlyRent);
  const occupantCount = fixedPrices[0]?.occupantCount ?? (period ? getActiveInvoiceOccupantCount(contract, period) : 1);
  const rentPreview = preview?.rentAmount ?? (period ? calculatePeriodAmount(resolvedMonthlyRent, period) : 0);
  const fixedTotal = preview?.fixedServiceAmount ?? 0;
  const utilityPreview = meteredPrices.reduce((sum, price) => {
    const draft = readings[price.serviceTypeId];
    if (!draft) return sum;
    const latestReading = price.latestReading;
    const previousReading = latestReading?.currentReading ?? Number(draft.previousReading);
    const consumption = Math.max(0, Number(draft.currentReading) - previousReading);
    return sum + consumption * price.unitPrice;
  }, 0);
  const previewTotal = Math.max(0, rentPreview + fixedTotal + utilityPreview - discountAmount);

  function updateReading(serviceTypeId: string, patch: Partial<ReadingDraft>) {
    setReadings((current) => ({
      ...current,
      [serviceTypeId]: {
        ...(current[serviceTypeId] ?? emptyInvoiceReadingDraft),
        ...patch,
      },
    }));
  }

  async function handleMeterImage(serviceTypeId: string, file?: File) {
    if (!file || !period) return;
    if (!['image/jpeg', 'image/png'].includes(file.type)) {
      setError('Chỉ hỗ trợ ảnh đồng hồ định dạng JPG hoặc PNG.');
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError('Dung lượng ảnh đồng hồ không được vượt quá 5MB.');
      return;
    }

    setUploadingServiceId(serviceTypeId);
    setError('');
    try {
      const response = await billingApi.readMeterImage({
        contractId: contract.id,
        serviceTypeId,
        billingPeriodStart: period.start,
        file,
      });
      updateReading(serviceTypeId, {
        currentReading: response.data.reading,
        hasCurrentReading: true,
        aiReading: response.data.reading,
        aiRawText: response.data.rawText,
        proofMediaAssetId: response.data.proofMediaAssetId ?? null,
        proofImageUrl: response.data.proofImageUrl,
      });
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể đọc chỉ số từ ảnh. Vui lòng thử ảnh rõ hơn.'));
    } finally {
      setUploadingServiceId('');
    }
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError('');

    if (!period) {
      setError(periodValidationMessage || 'Kỳ hóa đơn không hợp lệ.');
      return;
    }

    if (previewBlockReason) {
      setError(previewBlockReason);
      return;
    }

    const meterReadings: MeterReadingInput[] = meteredPrices.map((price) => {
      const draft = readings[price.serviceTypeId] ?? emptyInvoiceReadingDraft;
      const latestReading = latestReadingByServiceType[price.serviceTypeId];
      return {
        serviceTypeId: price.serviceTypeId,
        previousReading: latestReading ? null : Number(draft.previousReading),
        currentReading: Number(draft.currentReading),
        proofMediaAssetId: draft.proofMediaAssetId || null,
        aiReading: draft.aiReading,
        aiRawText: draft.aiRawText.trim() || null,
      };
    });

    for (const reading of meterReadings) {
      const price = meteredPrices.find((item) => item.serviceTypeId === reading.serviceTypeId);
      const draft = readings[reading.serviceTypeId] ?? emptyInvoiceReadingDraft;
      if (price?.requiresPreviousReading && !draft.hasPreviousReading) {
        setError(`Vui lòng nhập chỉ số đầu kỳ ${price.serviceName}.`);
        return;
      }
      if (!draft.hasCurrentReading) {
        setError(`Vui lòng nhập chỉ số mới ${price?.serviceName ?? ''}.`.trim());
        return;
      }
      if (!Number.isFinite(reading.currentReading)) {
        setError('Vui lòng nhập chỉ số mới hợp lệ cho tất cả dịch vụ tính theo chỉ số.');
        return;
      }

      const latestReading = latestReadingByServiceType[reading.serviceTypeId];
      const previousReading = latestReading?.currentReading ?? reading.previousReading;
      if (previousReading !== null && previousReading !== undefined && reading.currentReading < previousReading) {
        setError('Chỉ số mới không được nhỏ hơn chỉ số cũ.');
        return;
      }
    }

    setSubmitting(true);
    try {
      const response = await billingApi.generateWithReadings({
        contractId: contract.id,
        billingPeriodStart: period.start,
        billingPeriodEnd: period.end,
        discountAmount: Number(discountAmount) || 0,
        note: note.trim() || null,
        meterReadings,
      });
      onCreated(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tạo hóa đơn.'));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="history-modal-overlay">
      <div className="history-modal-content invoice-create-modal">
        <div className="history-modal-header">
          <h2>Tạo hóa đơn</h2>
          <button className="history-modal-close" onClick={onClose} type="button">&times;</button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="history-modal-body">
            {error && <Alert type="error">{error}</Alert>}
            {loading ? (
              <p>Đang tải dữ liệu...</p>
            ) : (
              <div className="invoice-create-stack">
                {periodValidationMessage && (
                  <Alert type="error">{periodValidationMessage}</Alert>
                )}
                {previewBlockReason && (
                  <Alert type="error">{previewBlockReason}</Alert>
                )}
                <div className="invoice-create-grid">
                  <div className="invoice-create-field">
                    <span className="label">Phòng</span>
                    <span className="value">{contract.roomNumber}</span>
                  </div>
                  <div className="invoice-create-field">
                    <span className="label">Người thuê</span>
                    <span className="value">{contract.mainTenantName}</span>
                  </div>
                  <div className="invoice-create-field">
                    <span className="label">Tiền phòng</span>
                    <span className="value">{formatMoneyString(resolvedMonthlyRent)} đ</span>
                  </div>
                  <div className="invoice-create-field">
                    <span className="label">Kỳ hóa đơn</span>
                    <input type="month" value={billingMonth} onChange={(event) => setBillingMonth(event.target.value)} />
                  </div>
                  <div className="invoice-create-field">
                    <span className="label">Kỳ thực tế</span>
                    <span className="value">{period ? `${formatDateVi(period.start)} - ${formatDateVi(period.end)}` : '--'}</span>
                  </div>
                  <div className="invoice-create-field">
                    <span className="label">Tiền phòng kỳ này</span>
                    <span className="value">{formatMoneyString(rentPreview)} đ</span>
                  </div>
                </div>

                <section className="invoice-create-section">
                  <h3>Dịch vụ cố định</h3>
                  {fixedPrices.length === 0 ? (
                    <p className="invoice-create-empty">Chưa có dịch vụ cố định được cấu hình.</p>
                  ) : (
                    <div className="invoice-create-stack">
                      {fixedPrices.map((price) => (
                        <div key={price.serviceTypeId} className="invoice-create-line">
                          <span>{price.serviceName} / {price.displayUnitName}</span>
                          <strong>{formatFixedPreviewLine(price)}</strong>
                        </div>
                      ))}
                    </div>
                  )}
                </section>

                <section className="invoice-create-section">
                  <h3>Chỉ số điện nước</h3>
                  {meteredPrices.length === 0 ? (
                    <p className="invoice-create-empty">Không có dịch vụ điện/nước nào đang cấu hình theo chỉ số.</p>
                  ) : (
                    <div className="invoice-create-meter-list">
                      {meteredPrices.map((price) => {
                        const draft = readings[price.serviceTypeId] ?? emptyInvoiceReadingDraft;
                        const latestReading = latestReadingByServiceType[price.serviceTypeId];
                        const previousReading = latestReading?.currentReading ?? Number(draft.previousReading);
                        const aiReadingIsLower = draft.aiReading !== null && draft.aiReading < previousReading;
                        const confirmedReadingIsLower = draft.hasCurrentReading && Number(draft.currentReading) < previousReading;
                        const consumption = Math.max(0, Number(draft.currentReading) - previousReading);
                        const amount = Math.round(consumption * price.unitPrice);
                        return (
                          <div key={price.serviceTypeId} className={`invoice-create-meter-card${aiReadingIsLower ? ' meter-card-warning' : ''}`}>
                            <div className="meter-reading-main">
                              <strong className="meter-reading-title">{price.serviceName} ({formatMoneyString(price.unitPrice)} đ / {price.meterUnitName})</strong>
                              <div className="meter-reading-fields">
                                {price.requiresPreviousReading ? (
                                  <label className="invoice-create-field">
                                    <span className="label">Chỉ số đầu kỳ</span>
                                    <input
                                      type="number"
                                      min="0"
                                      value={draft.hasPreviousReading ? draft.previousReading : ''}
                                      placeholder="Nhập chỉ số khi bắt đầu ở"
                                      onChange={(event) => updateReading(price.serviceTypeId, {
                                        previousReading: Number(event.target.value),
                                        hasPreviousReading: event.target.value !== '',
                                      })}
                                    />
                                    <small>Nhập lần đầu cho phòng này</small>
                                  </label>
                                ) : (
                                  <div className="invoice-create-field">
                                    <span className="label">Chỉ số cũ</span>
                                    <strong>{previousReading} {price.meterUnitName}</strong>
                                    <small>Giữ nguyên từ kỳ trước</small>
                                  </div>
                                )}
                                <div className="invoice-create-field">
                                  <span className="label">Chỉ số mới (AI)</span>
                                  <strong className={aiReadingIsLower ? 'meter-reading-danger' : undefined}>{draft.aiReading ?? '--'} {draft.aiReading !== null ? price.meterUnitName : ''}</strong>
                                  {aiReadingIsLower ? (
                                    <small className="meter-warning-text">Chỉ số AI thấp hơn chỉ số cũ. Ảnh đã được giữ.</small>
                                  ) : (
                                    <small>{draft.aiReading !== null ? 'Đọc từ ảnh' : 'Chưa upload ảnh'}</small>
                                  )}
                                </div>
                                <label className="invoice-create-field">
                                  <span className="label">Chỉ số xác nhận cuối cùng</span>
                                  <input
                                    type="number"
                                    min="0"
                                    value={draft.hasCurrentReading ? draft.currentReading : ''}
                                    placeholder={`Từ ${previousReading}`}
                                    aria-invalid={confirmedReadingIsLower}
                                    className={confirmedReadingIsLower ? 'meter-input-invalid' : undefined}
                                    onChange={(event) => updateReading(price.serviceTypeId, {
                                      currentReading: Number(event.target.value),
                                      hasCurrentReading: event.target.value !== '',
                                    })}
                                  />
                                  {draft.aiReading !== null && (
                                    <small className={confirmedReadingIsLower ? 'meter-warning-text' : draft.currentReading === draft.aiReading ? 'meter-ok' : 'meter-edited'}>
                                      {confirmedReadingIsLower
                                        ? `Cần nhập từ ${previousReading} ${price.meterUnitName} trở lên trước khi tạo hóa đơn.`
                                        : draft.currentReading === draft.aiReading
                                          ? '✓ Không chỉnh sửa'
                                          : '✓ Đã chỉnh sửa kết quả AI'}
                                    </small>
                                  )}
                                  {draft.aiReading === null && (
                                    <small>Nhập trực tiếp nếu không dùng AI.</small>
                                  )}
                                </label>
                              </div>
                              <div className="meter-calculation">
                                <span><small>TIÊU THỤ</small><strong>{formatMoneyString(consumption)} {price.meterUnitName}</strong></span>
                                <b>×</b>
                                <span><small>ĐƠN GIÁ</small><strong>{formatMoneyString(price.unitPrice)} / {price.meterUnitName}</strong></span>
                                <b>=</b>
                                <span><small>TẠM TÍNH</small><strong className="meter-amount">{formatMoneyString(amount)} đ</strong></span>
                              </div>
                            </div>
                            <div className="meter-image-panel">
                              <span className="label">ẢNH ĐỒNG HỒ {price.serviceName.toUpperCase()}</span>
                              {draft.proofImageUrl ? <PrivateMediaImage source={draft.proofImageUrl} alt={`Đồng hồ ${price.serviceName}`} /> : <div className="meter-image-empty">Chưa có ảnh đồng hồ</div>}
                              <label className="meter-upload-button">
                                {uploadingServiceId === price.serviceTypeId ? 'AI đang đọc ảnh...' : draft.proofImageUrl ? 'Thay ảnh' : 'Upload & đọc AI'}
                                <input type="file" accept="image/jpeg,image/png" disabled={uploadingServiceId !== ''} onChange={(event) => { void handleMeterImage(price.serviceTypeId, event.target.files?.[0]); event.currentTarget.value = ''; }} />
                              </label>
                              <small>Tùy chọn · JPG, PNG · tối đa 5MB</small>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </section>

                <div className="invoice-create-summary">
                  <label className="invoice-create-field">
                    <span className="label">Giảm trừ</span>
                    <input type="number" min="0" value={discountAmount} onChange={(event) => setDiscountAmount(Number(event.target.value))} />
                  </label>
                  <label className="invoice-create-field">
                    <span className="label">Ghi chú</span>
                    <input value={note} onChange={(event) => setNote(event.target.value)} />
                  </label>
                  <div className="invoice-create-field invoice-create-total">
                    <span className="label">Tạm tính</span>
                    <span className="value">{formatMoneyString(previewTotal)} đ</span>
                  </div>
                </div>
              </div>
            )}
          </div>

          <div className="history-modal-footer">
            <Button variant="outline" type="button" onClick={onClose} disabled={submitting}>Đóng</Button>
            <Button type="submit" disabled={loading || submitting || uploadingServiceId !== '' || !period || !preview || Boolean(previewBlockReason)}>{submitting ? 'Đang tạo...' : 'Tạo hóa đơn nháp'}</Button>
          </div>
        </form>
      </div>
    </div>
  );
}

async function resolveRawContractFile(contractId: string): Promise<ContractFileResponse> {
  const response = await contractApi.getContractFiles(contractId);
  let file = findContractFile(response.data ?? [], 'SignedLegalDocument') ?? findContractFile(response.data ?? [], 'Preview');

  if (!file) {
    await contractApi.generateContractFile(contractId);
    const refreshedResponse = await contractApi.getContractFiles(contractId);
    file = findContractFile(refreshedResponse.data ?? [], 'SignedLegalDocument') ?? findContractFile(refreshedResponse.data ?? [], 'Preview');
  }

  if (!file) {
    throw new Error('Chưa có file hợp đồng phù hợp để xem hoặc tải.');
  }

  return file;
}

function findContractFile(files: ContractFileResponse[], purpose: string) {
  return files
    .filter((file) => !file.rentalContractAppendixId && file.purpose === purpose)
    .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0];
}

function normalizePricingUnit(unit: PricingUnit | string) {
  if (unit === 'Metered' || unit === 'MeterBased') {
    return 'MeterReading';
  }

  if (unit === 'Fixed' || unit === 'PerMonth') {
    return 'PerMonth';
  }

  if (unit === 'PerPerson' || unit === 'PerPersonPerMonth') {
    return 'PerPersonPerMonth';
  }

  return unit;
}

function isMeteredServicePrice(price: ServicePrice) {
  return normalizePricingUnit(price.pricingUnit) === 'MeterReading';
}

function calculateFixedServiceAmount(
  price: ServicePrice,
  period: ResolvedInvoicePeriod,
  occupantCount: number
) {
  const unitCount = normalizePricingUnit(price.pricingUnit) === 'PerPersonPerMonth'
    ? occupantCount
    : 1;

  return Math.round(price.unitPrice * unitCount * getInvoicePeriodQuantity(period));
}

function formatFixedServicePreview(
  price: ServicePrice,
  period: ResolvedInvoicePeriod | null,
  occupantCount: number
) {
  if (!period) {
    return '0 đ';
  }

  const amount = calculateFixedServiceAmount(price, period, occupantCount);

  if (normalizePricingUnit(price.pricingUnit) !== 'PerPersonPerMonth') {
    return `${formatMoneyString(amount)} đ`;
  }

  const unitAmount = Math.round(price.unitPrice * getInvoicePeriodQuantity(period));
  return `${formatMoneyString(unitAmount)} x ${occupantCount} = ${formatMoneyString(amount)} đ`;
}

function formatFixedPreviewLine(price: FixedServicePreview) {
  if (normalizePricingUnit(price.pricingUnit) !== 'PerPersonPerMonth') {
    return `${formatMoneyString(price.amount)} đ`;
  }

  const unitAmount = price.occupantCount > 0
    ? Math.round(price.amount / price.occupantCount)
    : price.unitPrice;

  return `${formatMoneyString(unitAmount)} x ${price.occupantCount} = ${formatMoneyString(price.amount)} đ`;
}

function getInvoicePeriodQuantity(period: ResolvedInvoicePeriod) {
  return period.isFullMonth ? 1 : period.billableDays / period.daysInMonth;
}

function getActiveInvoiceOccupantCount(contract: ContractDetailResponse, period: ResolvedInvoicePeriod) {
  const count = contract.occupants.filter((occupant) =>
    occupant.status === 'Active' &&
    occupant.moveInDate <= period.end &&
    (!occupant.moveOutDate || occupant.moveOutDate >= period.start)
  ).length;

  return Math.max(count, 1);
}

function resolveMonthlyRentFromAppendices(
  baseMonthlyRent: number,
  appendices: ContractAppendixResponse[],
  effectiveOn: string
) {
  const rentChanges = appendices
    .filter((appendix) => appendix.status === 'Active')
    .flatMap((appendix) =>
      appendix.changes
        .filter((change) =>
          change.changeType === 'Update' &&
          change.targetType === 'Contract' &&
          change.fieldName?.toLowerCase() === 'monthlyrent'
        )
        .map((change) => ({
          effectiveDate: appendix.effectiveDate,
          oldValue: change.oldValue,
          newValue: change.newValue
        }))
    )
    .sort((left, right) => left.effectiveDate.localeCompare(right.effectiveDate));

  if (rentChanges.length === 0) {
    return baseMonthlyRent;
  }

  const latestAppliedChange = [...rentChanges]
    .filter((change) => change.effectiveDate <= effectiveOn)
    .sort((left, right) => right.effectiveDate.localeCompare(left.effectiveDate))[0];
  const appliedRent = parseInvoiceAppendixMoney(latestAppliedChange?.newValue);
  if (appliedRent !== null) {
    return appliedRent;
  }

  const firstChange = rentChanges[0];
  const oldRent = effectiveOn < firstChange.effectiveDate
    ? parseInvoiceAppendixMoney(firstChange.oldValue)
    : null;

  return oldRent ?? baseMonthlyRent;
}

function parseInvoiceAppendixMoney(value?: string | null) {
  if (!value?.trim()) {
    return null;
  }

  const parsed = Number.parseFloat(value.trim().replace(/^"|"$/g, '').replace(/,/g, ''));
  return Number.isFinite(parsed) ? parsed : null;
}

type ResolvedInvoicePeriod = {
  start: string;
  end: string;
  billableDays: number;
  daysInMonth: number;
  isFullMonth: boolean;
};

function getDefaultInvoiceMonth(contract: ContractDetailResponse) {
  const today = getTodayDateOnly();
  const contractStart = parseDateOnly(contract.startDate);
  let cursor = new Date(today.getFullYear(), today.getMonth(), 1);

  for (let index = 0; index < 240; index += 1) {
    const monthValue = toMonthValue(cursor);
    const period = resolveInvoicePeriodForContract(monthValue, contract);
    if (period && compareDateOnly(period.end, toDateOnlyString(today)) <= 0) {
      return monthValue;
    }

    cursor = new Date(cursor.getFullYear(), cursor.getMonth() - 1, 1);
  }

  return toMonthValue(contractStart);
}

function resolveInvoicePeriodForContract(monthValue: string, contract: ContractDetailResponse): ResolvedInvoicePeriod | null {
  const [year, month] = monthValue.split('-').map(Number);
  if (!year || !month) {
    return null;
  }

  const monthStart = new Date(year, month - 1, 1);
  const monthEnd = new Date(year, month, 0);
  const contractStart = parseDateOnly(contract.startDate);
  const contractEnd = parseDateOnly(contract.endDate);
  const startDate = compareDates(contractStart, monthStart) > 0 ? contractStart : monthStart;
  const endDate = compareDates(contractEnd, monthEnd) < 0 ? contractEnd : monthEnd;

  if (compareDates(startDate, endDate) > 0) {
    return null;
  }

  const billableDays = countInclusiveDays(startDate, endDate);
  const daysInMonth = countInclusiveDays(monthStart, monthEnd);

  return {
    start: toDateOnlyString(startDate),
    end: toDateOnlyString(endDate),
    billableDays,
    daysInMonth,
    isFullMonth: compareDates(startDate, monthStart) === 0 && compareDates(endDate, monthEnd) === 0,
  };
}

function calculatePeriodAmount(monthlyAmount: number, period: ResolvedInvoicePeriod) {
  if (period.isFullMonth) {
    return monthlyAmount;
  }

  return Math.round(monthlyAmount * period.billableDays / period.daysInMonth);
}

function getTodayDateOnly() {
  const today = new Date();
  return new Date(today.getFullYear(), today.getMonth(), today.getDate());
}

function parseDateOnly(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

function toDateOnlyString(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

function toMonthValue(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
}

function compareDateOnly(left: string, right: string) {
  return compareDates(parseDateOnly(left), parseDateOnly(right));
}

function compareDates(left: Date, right: Date) {
  return getDayNumber(left) - getDayNumber(right);
}

function countInclusiveDays(start: Date, end: Date) {
  return getDayNumber(end) - getDayNumber(start) + 1;
}

function getDayNumber(date: Date) {
  return Math.floor(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()) / 86400000);
}

function formatInvoiceStatus(status: string) {
  const labels: Record<string, string> = {
    Draft: 'Nháp',
    Issued: 'Đã phát hành',
    Paid: 'Đã thanh toán',
    Overdue: 'Quá hạn',
    Cancelled: 'Đã hủy',
  };

  return labels[status] ?? status;
}

function getInvoiceStatusClass(status: string) {
  if (status === 'Paid') return 'success';
  if (status === 'Cancelled' || status === 'Overdue') return 'danger';
  if (status === 'Issued') return 'warning';
  return 'info';
}

function isRoomEditLocked(room: Room | null) {
  return room?.status === 'Reserved' || room?.status === 'Occupied';
}

function getAppendixStatusClass(status: ContractAppendixStatus) {
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
  disabled = false,
}: {
  priceTiers: RoomPriceTierRequest[];
  isTieredPricing: boolean;
  maxOccupants: number;
  onChange: (tiers: RoomPriceTierRequest[]) => void;
  onSave: () => void;
  depositMonths?: number;
  disabled?: boolean;
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
              disabled={disabled}
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
        <button className="primary-action" onClick={onSave} disabled={disabled}>
          Lưu bảng giá
        </button>
      </div>
    </div>
  );
}

