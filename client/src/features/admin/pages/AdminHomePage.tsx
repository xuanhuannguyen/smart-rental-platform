import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { Alert } from '../../../shared/components/ui/Alert';
import { Toast } from '../../../shared/components/ui/Toast';
import { Button } from '../../../shared/components/ui/Button';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import { AdminImage } from '../components/AdminImage';
import { AdminAmenitiesTab } from '../components/AdminAmenitiesTab';
import { AdminBillingServicesTab } from '../components/AdminBillingServicesTab';
import { AdminProvincesTab } from '../components/AdminProvincesTab';
import { AdminReviewReportsTab } from '../components/AdminReviewReportsTab';
import { adminApprovalApi } from '../services/adminApprovalApi';
import type {
  AdminKycDetail,
  AdminKycListItem,
  AdminRoomingHouseDetail,
  AdminRoomingHouseListItem,
  AdminUserListItem,
  AdminUserDetail,
  AdminApproveKycRequest
} from '../types/adminApproval.types';
import './AdminHomePage.css';

type AdminMenu = 'users' | 'houses' | 'reports' | 'provinces' | 'amenities' | 'billing-services';
type UserTab = 'list' | 'kyc';
type HouseTab = 'public' | 'pending';

// SVG Icons
function ArrowLeftIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <line x1="19" y1="12" x2="5" y2="12" />
      <polyline points="12 19 5 12 12 5" />
    </svg>
  );
}

function UserIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
      <circle cx="12" cy="7" r="4" />
    </svg>
  );
}

function MailIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" />
      <polyline points="22,6 12,13 2,6" />
    </svg>
  );
}

function ShieldIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
    </svg>
  );
}

function CalendarIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
      <line x1="16" y1="2" x2="16" y2="6" />
      <line x1="8" y1="2" x2="8" y2="6" />
      <line x1="3" y1="10" x2="21" y2="10" />
    </svg>
  );
}

function MapPinIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
      <circle cx="12" cy="10" r="3" />
    </svg>
  );
}

function CheckIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <polyline points="20 6 9 17 4 12" />
    </svg>
  );
}

function CloseIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <line x1="18" y1="6" x2="6" y2="18" />
      <line x1="6" y1="6" x2="18" y2="18" />
    </svg>
  );
}

function PhoneIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z" />
    </svg>
  );
}

function HouseIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
      <polyline points="9 22 9 12 15 12 15 22" />
    </svg>
  );
}

function DescriptionIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <line x1="21" y1="10" x2="3" y2="10" />
      <line x1="21" y1="6" x2="3" y2="6" />
      <line x1="21" y1="14" x2="3" y2="14" />
      <line x1="21" y1="18" x2="3" y2="18" />
    </svg>
  );
}

function FlagIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z" />
      <line x1="4" y1="22" x2="4" y2="15" />
    </svg>
  );
}

function LegalIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
      <path d="M7 11V7a5 5 0 0 1 10 0v4" />
    </svg>
  );
}

function LogoutIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ display: 'inline-block', verticalAlign: 'middle' }}>
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <polyline points="16 17 21 12 16 7" />
      <line x1="21" y1="12" x2="9" y2="12" />
    </svg>
  );
}


export function AdminHomePage() {
  const { currentUser, logout } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  // URL state management
  const activeMenu = (searchParams.get('menu') as AdminMenu) || 'users';
  const activeTab = searchParams.get('tab') || (activeMenu === 'users' ? 'list' : activeMenu === 'reports' ? 'pending' : 'public');
  const pageParam = parseInt(searchParams.get('page') || '1', 10);
  const selectedId = searchParams.get('id') || null;

  // Application States
  const [users, setUsers] = useState<AdminUserListItem[]>([]);
  const [userTotalCount, setUserTotalCount] = useState(0);
  const [selectedUser, setSelectedUser] = useState<AdminUserDetail | null>(null);

  const [kycPendingItems, setKycPendingItems] = useState<AdminKycListItem[]>([]);
  const [kycPendingTotal, setKycPendingTotal] = useState(0);
  const [selectedKyc, setSelectedKyc] = useState<AdminKycDetail | null>(null);
  const [kycEditForm, setKycEditForm] = useState<AdminApproveKycRequest>({
    citizenId: '',
    fullName: '',
    dateOfBirth: '',
    gender: '',
    address: '',
  });
  const [kycHistory, setKycHistory] = useState<AdminKycDetail[]>([]);
  const [isLoadingKycHistory, setIsLoadingKycHistory] = useState(false);

  const [housesPublic, setHousesPublic] = useState<AdminRoomingHouseListItem[]>([]);
  const [housePublicTotal, setHousePublicTotal] = useState(0);

  const [housesPending, setHousesPending] = useState<AdminRoomingHouseListItem[]>([]);
  const [housePendingTotal, setHousePendingTotal] = useState(0);
  const [selectedHouse, setSelectedHouse] = useState<AdminRoomingHouseDetail | null>(null);
  const [reason, setReason] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);

  const isAdmin = currentUser?.roles.includes('Admin');

  useEffect(() => {
    if (!isAdmin) {
      return;
    }
    void loadData();
  }, [activeMenu, activeTab, pageParam, selectedId, isAdmin]);

  async function fetchUserDetail(id: string) {
    const response = await adminApprovalApi.getUserDetail(id);
    setSelectedUser(response.data);
    setSelectedKyc(null);
    setSelectedHouse(null);
  }

  async function fetchKycDetail(id: string) {
    setKycHistory([]);
    const response = await adminApprovalApi.getKycDetail(id);
    setSelectedKyc(response.data);
    setKycEditForm({
      citizenId: '',
      fullName: response.data.ocrFullName ?? response.data.userDisplayName ?? '',
      dateOfBirth: toDateInputValue(response.data.ocrDateOfBirth),
      gender: response.data.ocrGender ?? '',
      address: response.data.ocrAddress ?? '',
    });
    setSelectedHouse(null);

    // Load history
    setIsLoadingKycHistory(true);
    try {
      const historyResponse = await adminApprovalApi.getKycHistory(response.data.userId);
      // Lọc bỏ chính đơn KYC đang pending để không bị trùng lặp trong timeline
      setKycHistory(historyResponse.data.filter((h) => h.id !== id));
    } catch (err) {
      console.error('Không tải được lịch sử eKYC:', err);
    } finally {
      setIsLoadingKycHistory(false);
    }
  }

  async function fetchHouseDetail(id: string) {
    const response = await adminApprovalApi.getRoomingHouseDetail(id);
    setSelectedHouse(response.data);
    setSelectedKyc(null);
  }

  async function loadData() {
    setIsLoading(true);
    setError('');
    try {
      if (selectedId) {
        if (activeMenu === 'users' && activeTab === 'list') {
          await fetchUserDetail(selectedId);
        } else if (activeMenu === 'users' && activeTab === 'kyc') {
          await fetchKycDetail(selectedId);
        } else if (activeMenu === 'houses' && (activeTab === 'pending' || activeTab === 'public')) {
          await fetchHouseDetail(selectedId);
        }
      } else {
        setSelectedUser(null);
        setSelectedKyc(null);
        setKycEditForm({ citizenId: '', fullName: '', dateOfBirth: '', gender: '', address: '' });
        setSelectedHouse(null);
        setKycHistory([]);

        if (activeMenu === 'users') {
          if (activeTab === 'list') {
            const response = await adminApprovalApi.getUsers(pageParam, 20);
            setUsers(response.data.items);
            setUserTotalCount(response.data.totalItems);
          } else if (activeTab === 'kyc') {
            const response = await adminApprovalApi.getPendingKyc(pageParam, 20);
            setKycPendingItems(response.data.items);
            setKycPendingTotal(response.data.totalItems);
          }
        } else if (activeMenu === 'houses') {
          if (activeTab === 'public') {
            const response = await adminApprovalApi.getPublicRoomingHouses(pageParam, 20);
            setHousesPublic(response.data.items);
            setHousePublicTotal(response.data.totalItems);
          } else if (activeTab === 'pending') {
            const response = await adminApprovalApi.getPendingRoomingHouses(pageParam, 20);
            setHousesPending(response.data.items);
            setHousePendingTotal(response.data.totalItems);
          }
        }
      }
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tải dữ liệu quản trị.'));
    } finally {
      setIsLoading(false);
    }
  }

  function handleMenuChange(menu: AdminMenu) {
    setToast(null);
    setError('');
    let defaultTab = 'public';
    if (menu === 'users') defaultTab = 'list';
    else if (menu === 'reports') defaultTab = 'pending';

    setSearchParams({ menu, tab: defaultTab, page: '1' });
  }

  function handleTabChange(tab: string) {
    setToast(null);
    setError('');
    setSearchParams({ menu: activeMenu, tab, page: '1' });
  }

  function openUser(id: string) {
    setError('');
    setToast(null);
    setReason('');
    setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString(), id });
  }

  function openKyc(id: string) {
    setError('');
    setToast(null);
    setReason('');
    setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString(), id });
  }

  function patchKycEditForm(patch: Partial<AdminApproveKycRequest>) {
    setKycEditForm((current) => ({ ...current, ...patch }));
  }

  function openHouse(id: string) {
    setError('');
    setToast(null);
    setReason('');
    setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString(), id });
  }

  async function handleApprove() {
    setIsSubmitting(true);
    setError('');
    setToast(null);
    try {
      if (activeMenu === 'users' && activeTab === 'kyc' && selectedKyc) {
        const response = await adminApprovalApi.approveKyc(selectedKyc.id, {
          citizenId: kycEditForm.citizenId?.trim() || null,
          fullName: kycEditForm.fullName?.trim() || null,
          dateOfBirth: kycEditForm.dateOfBirth || null,
          gender: kycEditForm.gender?.trim() || null,
          address: kycEditForm.address?.trim() || null,
        });
        setToast({ message: response.message ?? 'Đã duyệt KYC thành công.', type: 'success' });
        setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() });
      } else if (activeMenu === 'houses' && activeTab === 'pending' && selectedHouse) {
        const response = await adminApprovalApi.approveRoomingHouse(selectedHouse.id);
        setToast({ message: response.message ?? 'Đã duyệt khu trọ thành công.', type: 'success' });
        setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() });
      }
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể duyệt hồ sơ.'), type: 'error' });
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleReject() {
    if (!reason.trim()) {
      setToast({ message: 'Vui lòng nhập lý do từ chối.', type: 'error' });
      return;
    }
    setIsSubmitting(true);
    setError('');
    setToast(null);
    try {
      if (activeMenu === 'users' && activeTab === 'kyc' && selectedKyc) {
        const response = await adminApprovalApi.rejectKyc(selectedKyc.id, reason);
        setToast({ message: response.message ?? 'Đã từ chối KYC.', type: 'success' });
        setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() });
      } else if (activeMenu === 'houses' && activeTab === 'pending' && selectedHouse) {
        const response = await adminApprovalApi.rejectRoomingHouse(selectedHouse.id, reason);
        setToast({ message: response.message ?? 'Đã từ chối khu trọ.', type: 'success' });
        setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() });
      }
      setReason('');
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể từ chối hồ sơ.'), type: 'error' });
    } finally {
      setIsSubmitting(false);
    }
  }

  function assetUrl(url: string) {
    return toAssetUrl(url);
  }

  function toDateInputValue(value?: string | null) {
    if (!value) {
      return '';
    }

    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) {
      return '';
    }

    return parsed.toISOString().slice(0, 10);
  }

  if (!isAdmin) {
    return (
      <main className="auth-page">
        <section className="auth-panel admin-panel">
          <Alert type="error">Bạn cần quyền Admin để truy cập trang này.</Alert>
          <div className="auth-actions" style={{ marginTop: '20px' }}>
            <Button type="button" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
              Về trang chủ
            </Button>
          </div>
        </section>
      </main>
    );
  }

  return (
    <div className="admin-dashboard">
      {/* Sidebar bên trái */}
      <aside className="admin-sidebar">
        <div className="sidebar-header">
          <div className="sidebar-logo">Smart<span>Rental</span></div>
          <div className="sidebar-subtitle">Quản trị viên</div>
        </div>

        <nav className="sidebar-menu">
          <button
            type="button"
            className={`sidebar-menu-item ${activeMenu === 'users' ? 'active' : ''}`}
            onClick={() => handleMenuChange('users')}
          >
            <UserIcon /> Quản lý người dùng
          </button>
          <button
            type="button"
            className={`sidebar-menu-item ${activeMenu === 'houses' ? 'active' : ''}`}
            onClick={() => handleMenuChange('houses')}
          >
            <HouseIcon /> Quản lý khu trọ
          </button>
          <button
            type="button"
            className={`sidebar-menu-item ${activeMenu === 'reports' ? 'active' : ''}`}
            onClick={() => handleMenuChange('reports')}
          >
            <FlagIcon /> Quản lý báo cáo
          </button>
          <button
            type="button"
            className={`sidebar-menu-item ${activeMenu === 'provinces' ? 'active' : ''}`}
            onClick={() => handleMenuChange('provinces')}
          >
            <MapPinIcon /> Quản lý khu vực
          </button>
          <button
            type="button"
            className={`sidebar-menu-item ${activeMenu === 'amenities' ? 'active' : ''}`}
            onClick={() => handleMenuChange('amenities')}
          >
            <CheckIcon /> Quản lý tiện ích
          </button>
          <button
            type="button"
            className={`sidebar-menu-item ${activeMenu === 'billing-services' ? 'active' : ''}`}
            onClick={() => handleMenuChange('billing-services')}
          >
            <CalendarIcon /> Quản lý dịch vụ
          </button>
        </nav>

        <div className="sidebar-footer">
          <div className="sidebar-user" title={currentUser?.email}>
            Admin: <strong>{currentUser?.displayName || currentUser?.email}</strong>
          </div>
          <button type="button" className="sidebar-logout-btn" onClick={() => void logout()}>
            <LogoutIcon /> Đăng xuất
          </button>
        </div>
      </aside>

      {/* Main Content bên phải */}
      <main className="admin-main-content">
        <div className="content-header">
          <h1>
            {activeMenu === 'users' ? 'Quản lý Người dùng' :
              activeMenu === 'houses' ? 'Quản lý Khu trọ' :
              activeMenu === 'reports' ? 'Quản lý Báo cáo' :
              activeMenu === 'provinces' ? 'Quản lý Khu vực' :
              activeMenu === 'amenities' ? 'Quản lý Tiện ích' :
                'Quản lý Dịch vụ'}
          </h1>
          <p>Hệ thống giám sát, xác thực thành viên, khu trọ và các nội dung trên nền tảng.</p>
        </div>

        {error && <Alert type="error">{error}</Alert>}
        

        {/* Cấu trúc Tabs */}
        {activeMenu === 'users' ? (
          <div className="admin-tabs">
            <button
              type="button"
              className={`admin-tab-btn ${activeTab === 'list' ? 'active' : ''}`}
              onClick={() => handleTabChange('list')}
            >
              Tất cả người dùng
            </button>
            <button
              type="button"
              className={`admin-tab-btn ${activeTab === 'kyc' ? 'active' : ''}`}
              onClick={() => handleTabChange('kyc')}
            >
              Duyệt eKYC
            </button>
          </div>
        ) : activeMenu === 'houses' ? (
          <div className="admin-tabs">
            <button
              type="button"
              className={`admin-tab-btn ${activeTab === 'public' ? 'active' : ''}`}
              onClick={() => handleTabChange('public')}
            >
              Khu trọ đã public
            </button>
            <button
              type="button"
              className={`admin-tab-btn ${activeTab === 'pending' ? 'active' : ''}`}
              onClick={() => handleTabChange('pending')}
            >
              Khu trọ chờ duyệt
            </button>
          </div>
        ) : null}

        {/* Nội dung tương ứng theo Tab */}
        {isLoading ? (
          <LoadingState message="Đang tải dữ liệu..." />
        ) : (
          <>
            {/* Tab: Tất cả người dùng */}
            {activeMenu === 'users' && activeTab === 'list' && (
              selectedUser ? (
                /* View Chi tiết người dùng rộng toàn màn hình */
                <div className="admin-detail-card">
                  <div className="admin-detail-card__header">
                    <button
                      type="button"
                      className="admin-back-btn"
                      onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() })}
                    >
                      <ArrowLeftIcon />
                      Quay lại danh sách
                    </button>
                    <h2>Chi tiết Người dùng</h2>
                  </div>

                  <div className="admin-detail-split-grid">
                    {/* Cột trái: Thông tin tài khoản */}
                    <div className="admin-detail-column-left">
                      <div className="admin-detail-section">
                        <h3 className="section-subtitle">Thông tin tài khoản</h3>
                        <div className="admin-grid-details">
                          <div className="admin-detail-item">
                            <label><UserIcon /> Tên hiển thị</label>
                            <span>{selectedUser.displayName}</span>
                          </div>
                          <div className="admin-detail-item">
                            <label><MailIcon /> Email</label>
                            <span>{selectedUser.email}</span>
                          </div>
                          <div className="admin-detail-item">
                            <label><PhoneIcon /> Số điện thoại</label>
                            <span>{selectedUser.phoneNumber || '—'}</span>
                          </div>
                          <div className="admin-detail-item">
                            <label><ShieldIcon /> Vai trò</label>
                            <div className="admin-detail-badges">
                              {selectedUser.roles.map((role) => (
                                <span key={role} className={`badge badge-${role.toLowerCase()}`}>
                                  {role}
                                </span>
                              ))}
                            </div>
                          </div>
                          <div className="admin-detail-item">
                            <label>Trạng thái tài khoản</label>
                            <span>
                              <span className={`badge ${selectedUser.status === 'Active' ? 'badge-active' : 'badge-suspended'}`}>
                                <span className="status-dot"></span>
                                {selectedUser.status}
                              </span>
                            </span>
                          </div>
                          <div className="admin-detail-item">
                            <label>Trạng thái KYC</label>
                            <span>
                              <span className={`badge badge-${selectedUser.onboardingStatus.toLowerCase()}`}>
                                {selectedUser.onboardingStatus === 'Completed' ? 'Đã KYC' : selectedUser.onboardingStatus}
                              </span>
                            </span>
                          </div>
                          <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                            <label><CalendarIcon /> Ngày tham gia</label>
                            <span>{new Date(selectedUser.createdAt).toLocaleString('vi-VN')}</span>
                          </div>
                        </div>
                      </div>

                      {/* Thông tin hồ sơ cá nhân nếu có */}
                      {selectedUser.fullName && (
                        <div className="admin-detail-section" style={{ marginTop: '24px' }}>
                          <h3 className="section-subtitle">Thông tin cá nhân (Hồ sơ)</h3>
                          <div className="admin-grid-details">
                            <div className="admin-detail-item">
                              <label>Họ và tên thật</label>
                              <span>{selectedUser.fullName}</span>
                            </div>
                            <div className="admin-detail-item">
                              <label><CalendarIcon /> Ngày sinh</label>
                              <span>{selectedUser.dateOfBirth ? new Date(selectedUser.dateOfBirth).toLocaleDateString('vi-VN') : '—'}</span>
                            </div>
                            <div className="admin-detail-item">
                              <label>Giới tính</label>
                              <span>{selectedUser.gender || '—'}</span>
                            </div>
                            <div className="admin-detail-item">
                              <label>Số CCCD đã xác thực</label>
                              <span className="verified-citizen-id">
                                {selectedUser.verifiedCitizenIdMasked || '—'}
                              </span>
                            </div>
                            <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                              <label><MapPinIcon /> Địa chỉ liên hệ</label>
                              <span>{selectedUser.addressLine || '—'}</span>
                            </div>
                            {selectedUser.emergencyContactName && (
                              <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                                <label>Liên hệ khẩn cấp</label>
                                <span>{selectedUser.emergencyContactName} ({selectedUser.emergencyContactPhone || '—'})</span>
                              </div>
                            )}
                          </div>
                        </div>
                      )}
                    </div>

                    {/* Cột phải: Dữ liệu KYC đã duyệt */}
                    <div className="admin-detail-column-right">
                      {selectedUser.onboardingStatus === 'Completed' && selectedUser.kycInfo ? (
                        <div className="admin-detail-section">
                          <h3 className="section-subtitle">Tài liệu định danh & Selfie (Đã duyệt)</h3>

                          <div className="admin-media-grid-vertical">
                            <div className="media-container">
                              <span className="media-label">Mặt trước CCCD</span>
                              <AdminImage label="Mặt trước CCCD" src={assetUrl(selectedUser.kycInfo.frontImageUrl)} />
                            </div>
                            <div className="media-container">
                              <span className="media-label">Mặt sau CCCD</span>
                              <AdminImage label="Mặt sau CCCD" src={assetUrl(selectedUser.kycInfo.backImageUrl)} />
                            </div>
                            <div className="media-container">
                              <span className="media-label">Ảnh Selfie</span>
                              <AdminImage label="Ảnh Selfie" src={assetUrl(selectedUser.kycInfo.selfieImageUrl)} />
                            </div>
                          </div>

                          <div className="admin-kyc-summary-panel">
                            <div className="admin-grid-details" style={{ marginTop: '20px' }}>
                              <div className="admin-detail-item">
                                <label>Mức rủi ro KYC</label>
                                <span className={`badge ${selectedUser.kycInfo.riskLevel === 'High' ? 'badge-rejected' : 'badge-pending'}`}>
                                  {selectedUser.kycInfo.riskLevel}
                                </span>
                              </div>
                              <div className="admin-detail-item">
                                <label>Kết quả hệ thống tự động (AI)</label>
                                <span className="system-result-text">
                                  {selectedUser.kycInfo.ekycResult === 'Passed' ? 'Passed (Hợp lệ)' :
                                    selectedUser.kycInfo.ekycResult === 'NeedReview' ? 'NeedReview (Cần hậu kiểm)' :
                                      selectedUser.kycInfo.ekycResult === 'Failed' ? 'Failed (Không trùng khớp)' :
                                        selectedUser.kycInfo.ekycResult === 'ProviderError' ? 'ProviderError (Lỗi nhà cung cấp)' :
                                          selectedUser.kycInfo.ekycResult}
                                </span>
                              </div>
                              <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                                <label>Ngày duyệt KYC</label>
                                <span>{selectedUser.kycInfo.approvedAt ? new Date(selectedUser.kycInfo.approvedAt).toLocaleString('vi-VN') : '—'}</span>
                              </div>
                            </div>
                          </div>
                        </div>
                      ) : (
                        <div className="admin-no-kyc-placeholder">
                          <ShieldIcon />
                          <span>Tài khoản này chưa hoàn thành hoặc chưa lưu dữ liệu KYC.</span>
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              ) : (
                /* Bảng danh sách người dùng */
                <div className="admin-card">
                  <div className="table-responsive">
                    <table className="admin-table">
                      <thead>
                        <tr>
                          <th>Tên hiển thị</th>
                          <th>Email</th>
                          <th>Số điện thoại</th>
                          <th>Vai trò</th>
                          <th>Xác thực KYC</th>
                          <th>Ngày tham gia</th>
                          <th style={{ textAlign: 'right' }}>Hành động</th>
                        </tr>
                      </thead>
                      <tbody>
                        {users.length === 0 ? (
                          <tr>
                            <td colSpan={7} style={{ textAlign: 'center', padding: '32px' }}>
                              Không tìm thấy người dùng nào.
                            </td>
                          </tr>
                        ) : (
                          users.map((user) => (
                            <tr key={user.id}>
                              <td><strong>{user.displayName}</strong></td>
                              <td>{user.email}</td>
                              <td>{user.phoneNumber || '—'}</td>
                              <td>
                                <div style={{ display: 'flex', gap: '4px', flexWrap: 'wrap' }}>
                                  {user.roles.map((role) => (
                                    <span key={role} className={`badge badge-${role.toLowerCase()}`}>
                                      {role}
                                    </span>
                                  ))}
                                </div>
                              </td>
                              <td>
                                <span className={`badge badge-${user.onboardingStatus.toLowerCase()}`}>
                                  {user.onboardingStatus === 'Completed' ? 'Đã KYC' : user.onboardingStatus}
                                </span>
                              </td>
                              <td>{new Date(user.createdAt).toLocaleDateString('vi-VN')}</td>
                              <td style={{ textAlign: 'right' }}>
                                <button
                                  type="button"
                                  className="admin-table-action-btn"
                                  onClick={() => void openUser(user.id)}
                                >
                                  Xem chi tiết
                                </button>
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>

                  {/* Phân trang người dùng */}
                  <div className="admin-pagination">
                    <span className="pagination-info">
                      Hiển thị tối đa 20 dòng · Tổng cộng {userTotalCount} người dùng
                    </span>
                    <div className="pagination-actions">
                      <button
                        type="button"
                        className="pagination-btn"
                        disabled={pageParam <= 1}
                        onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: (pageParam - 1).toString() })}
                      >
                        Trước
                      </button>
                      <button
                        type="button"
                        className="pagination-btn"
                        disabled={pageParam * 20 >= userTotalCount}
                        onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: (pageParam + 1).toString() })}
                      >
                        Sau
                      </button>
                    </div>
                  </div>
                </div>
              )
            )}

            {/* Tab: Yêu cầu KYC chờ duyệt */}
            {activeMenu === 'users' && activeTab === 'kyc' && (
              selectedKyc ? (
                /* View Chi tiết hồ sơ KYC động rộng toàn màn hình */
                <div className="admin-detail-card admin-detail-card--split">
                  <div className="admin-detail-card__header">
                    <button
                      type="button"
                      className="admin-back-btn"
                      onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() })}
                    >
                      <ArrowLeftIcon />
                      Quay lại danh sách
                    </button>
                    <h2>Chi tiết Hồ sơ eKYC</h2>
                  </div>

                  <div className="admin-detail-split-grid">
                    {/* Cột trái: Thông tin định danh + Lịch sử + Actions */}
                    <div className="admin-detail-column-left">
                      <div className="admin-detail-section">
                        <h3 className="section-subtitle">Thông tin định danh có thể chỉnh sửa</h3>
                        {selectedKyc.ekycErrorCode || selectedKyc.ekycErrorMessage ? (
                          <Alert type="info">
                            VNPT trả về: {selectedKyc.ekycErrorCode || 'Không có mã lỗi'} - {selectedKyc.ekycErrorMessage || 'Cần admin đối chiếu thủ công.'}
                          </Alert>
                        ) : null}
                        <div className="admin-grid-details">
                          <div className="admin-detail-item">
                            <label><UserIcon /> Tên chủ thẻ</label>
                            <input
                              className="ui-input"
                              value={kycEditForm.fullName ?? ''}
                              onChange={(event) => patchKycEditForm({ fullName: event.target.value })}
                              placeholder="Nhập họ tên theo giấy tờ"
                            />
                          </div>
                          <div className="admin-detail-item">
                            <label><MailIcon /> Email đăng ký</label>
                            <span>{selectedKyc.userEmail}</span>
                          </div>
                          <div className="admin-detail-item">
                            <label><ShieldIcon /> Số CCCD / CMND</label>
                            <input
                              className="ui-input"
                              value={kycEditForm.citizenId ?? ''}
                              onChange={(event) => patchKycEditForm({ citizenId: event.target.value })}
                              placeholder={selectedKyc.ocrCitizenIdMasked ? `Đang lưu: ${selectedKyc.ocrCitizenIdMasked}` : 'Nhập số CCCD nếu OCR chưa nhận dạng'}
                            />
                          </div>
                          <div className="admin-detail-item">
                            <label><CalendarIcon /> Ngày sinh</label>
                            <input
                              className="ui-input"
                              type="date"
                              value={kycEditForm.dateOfBirth ?? ''}
                              onChange={(event) => patchKycEditForm({ dateOfBirth: event.target.value })}
                            />
                          </div>
                          <div className="admin-detail-item">
                            <label><ShieldIcon /> Giới tính</label>
                            <input
                              className="ui-input"
                              value={kycEditForm.gender ?? ''}
                              onChange={(event) => patchKycEditForm({ gender: event.target.value })}
                              placeholder="Nam/Nữ"
                            />
                          </div>
                          <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                            <label><MapPinIcon /> Quê quán / địa chỉ thường trú</label>
                            <textarea
                              className="ui-input"
                              value={kycEditForm.address ?? ''}
                              onChange={(event) => patchKycEditForm({ address: event.target.value })}
                              placeholder="Nhập địa chỉ theo giấy tờ"
                              rows={3}
                            />
                          </div>
                        </div>
                      </div>

                      {/* Lịch sử eKYC */}
                      <div className="admin-detail-section kyc-history-section">
                        <h3 className="section-subtitle">Lịch sử duyệt KYC của tài khoản</h3>
                        {isLoadingKycHistory ? (
                          <div className="loading-history">Đang tải lịch sử...</div>
                        ) : kycHistory.length === 0 ? (
                          <div className="no-history">Chưa từng có yêu cầu duyệt nào trước đó.</div>
                        ) : (
                          <div className="kyc-timeline">
                            {kycHistory.map((history) => (
                              <div className="kyc-timeline-item" key={history.id}>
                                <div className={`kyc-timeline-dot ${history.status}`}></div>
                                <div className="kyc-timeline-header">
                                  <span className={`kyc-timeline-status ${history.status}`}>
                                    {history.status === 'Approved' ? 'Đã Duyệt' : history.status === 'Rejected' ? 'Bị Từ Chối' : history.status}
                                  </span>
                                  <span className="kyc-timeline-date">
                                    gửi lúc {new Date(history.submittedAt).toLocaleString('vi-VN')}
                                  </span>
                                </div>
                                {history.rejectedReason && (
                                  <div className="kyc-timeline-reason">Lý do từ chối: {history.rejectedReason}</div>
                                )}
                              </div>
                            ))}
                          </div>
                        )}
                      </div>

                      {/* Action Approval */}
                      <div className="admin-action-area">
                        <h3 className="section-subtitle">Quyết định phê duyệt</h3>
                        <textarea
                          className="admin-reason-textarea"
                          value={reason}
                          onChange={(e) => setReason(e.target.value)}
                          placeholder="Nhập lý do từ chối (bắt buộc nếu nhấn từ chối hồ sơ)..."
                        />
                        <div className="admin-action-buttons">
                          <button
                            type="button"
                            className="admin-btn admin-btn--success"
                            disabled={isSubmitting}
                            onClick={handleApprove}
                          >
                            <CheckIcon />
                            Phê duyệt hồ sơ
                          </button>
                          <button
                            type="button"
                            className="admin-btn admin-btn--danger"
                            disabled={isSubmitting}
                            onClick={handleReject}
                          >
                            <CloseIcon />
                            Từ chối duyệt
                          </button>
                        </div>
                      </div>
                    </div>

                    {/* Cột phải: Hình ảnh xác thực */}
                    <div className="admin-detail-column-right">
                      <div className="admin-detail-section">
                        <h3 className="section-subtitle">Tài liệu xác thực</h3>
                        <div className="admin-media-grid-vertical">
                          <div className="media-container">
                            <span className="media-label">Mặt trước CCCD</span>
                            <AdminImage label="Mặt trước CCCD" src={assetUrl(selectedKyc.frontImageUrl)} />
                          </div>
                          <div className="media-container">
                            <span className="media-label">Mặt sau CCCD</span>
                            <AdminImage label="Mặt sau CCCD" src={assetUrl(selectedKyc.backImageUrl)} />
                          </div>
                          <div className="media-container">
                            <span className="media-label">Ảnh Selfie</span>
                            <AdminImage label="Ảnh Selfie" src={assetUrl(selectedKyc.selfieImageUrl)} />
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              ) : (
                /* Bảng danh sách KYC chờ duyệt */
                <div className="admin-card">
                  <div className="table-responsive">
                    <table className="admin-table">
                      <thead>
                        <tr>
                          <th>Tên hiển thị</th>
                          <th>Email</th>
                          <th>CCCD (Ocr)</th>
                          <th>Mức rủi ro</th>
                          <th>Ngày gửi</th>
                          <th style={{ textAlign: 'right' }}>Hành động</th>
                        </tr>
                      </thead>
                      <tbody>
                        {kycPendingItems.length === 0 ? (
                          <tr>
                            <td colSpan={6} style={{ textAlign: 'center', padding: '32px' }}>
                              Không có hồ sơ eKYC nào đang chờ duyệt.
                            </td>
                          </tr>
                        ) : (
                          kycPendingItems.map((item) => (
                            <tr key={item.id}>
                              <td><strong>{item.ocrFullName || item.userDisplayName}</strong></td>
                              <td>{item.userEmail}</td>
                              <td>{item.ocrCitizenIdMasked || '—'}</td>
                              <td>
                                <span className={`badge ${item.riskLevel === 'High' ? 'badge-rejected' : 'badge-pending'}`}>
                                  {item.riskLevel}
                                </span>
                              </td>
                              <td>{new Date(item.submittedAt).toLocaleString('vi-VN')}</td>
                              <td style={{ textAlign: 'right' }}>
                                <button
                                  type="button"
                                  className="admin-table-action-btn"
                                  onClick={() => void openKyc(item.id)}
                                >
                                  Xem chi tiết
                                </button>
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>

                  {/* Phân trang KYC chờ duyệt */}
                  <div className="admin-pagination">
                    <span className="pagination-info">
                      Hiển thị tối đa 20 dòng · Tổng cộng {kycPendingTotal} đơn eKYC chờ duyệt
                    </span>
                    <div className="pagination-actions">
                      <button
                        type="button"
                        className="pagination-btn"
                        disabled={pageParam <= 1}
                        onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: (pageParam - 1).toString() })}
                      >
                        Trước
                      </button>
                      <button
                        type="button"
                        className="pagination-btn"
                        disabled={pageParam * 20 >= kycPendingTotal}
                        onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: (pageParam + 1).toString() })}
                      >
                        Sau
                      </button>
                    </div>
                  </div>
                </div>
              )
            )}

            {/* Tab: Khu trọ đã public */}
            {activeMenu === 'houses' && activeTab === 'public' && (
              selectedHouse ? (
                /* View Chi tiết khu trọ đã public (read-only) */
                <div className="admin-detail-card admin-detail-card--split">
                  <div className="admin-detail-card__header">
                    <button
                      type="button"
                      className="admin-back-btn"
                      onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() })}
                    >
                      <ArrowLeftIcon />
                      Quay lại danh sách
                    </button>
                    <h2>Chi tiết Khu trọ đã duyệt</h2>
                  </div>

                  <div className="admin-detail-split-grid">
                    {/* Cột trái: Thông tin chi tiết */}
                    <div className="admin-detail-column-left">
                      <div className="admin-detail-section">
                        <h3 className="section-subtitle">Thông tin cơ bản</h3>
                        <h2 className="property-title">{selectedHouse.name}</h2>

                        <div className="admin-grid-details" style={{ marginTop: '16px' }}>
                          <div className="admin-detail-item">
                            <label><UserIcon /> Chủ trọ</label>
                            <span>{selectedHouse.landlordName}</span>
                            <span className="sub-text">{selectedHouse.landlordEmail}</span>
                          </div>
                          <div className="admin-detail-item">
                            <label><MapPinIcon /> Địa chỉ hiển thị</label>
                            <span>{selectedHouse.addressDisplay}</span>
                          </div>
                          <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                            <label><MapPinIcon /> Địa chỉ chi tiết</label>
                            <span>{selectedHouse.addressLine}</span>
                          </div>
                          <div className="admin-detail-item">
                            <label>Trạng thái duyệt</label>
                            <span>
                              <span className="badge badge-completed">
                                <span className="status-dot"></span> Approved
                              </span>
                            </span>
                          </div>
                          <div className="admin-detail-item">
                            <label>Trạng thái hiển thị</label>
                            <span>
                              <span className={`badge ${selectedHouse.visibilityStatus === 'Visible' ? 'badge-active' : 'badge-suspended'}`}>
                                {selectedHouse.visibilityStatus}
                              </span>
                            </span>
                          </div>
                          <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                            <label><DescriptionIcon /> Mô tả khu trọ</label>
                            <span className="property-description">{selectedHouse.description || 'Chưa cung cấp mô tả.'}</span>
                          </div>
                          {selectedHouse.legalDocument && (
                            <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                              <label><LegalIcon /> Số giấy tờ pháp lý (CCCD / Giấy chứng nhận)</label>
                              <span className="verified-citizen-id">
                                {selectedHouse.legalDocument.documentNumberMasked}
                              </span>
                            </div>
                          )}
                        </div>
                      </div>

                      {/* Tiện ích */}
                      <div className="admin-detail-section" style={{ marginTop: '24px' }}>
                        <h3 className="section-subtitle">Tiện ích khu trọ</h3>
                        <div className="admin-amenities-tags">
                          {selectedHouse.amenities.length === 0 ? (
                            <span className="no-amenities-text">Chưa đăng ký tiện ích nào.</span>
                          ) : (
                            selectedHouse.amenities.map((a) => (
                              <span key={a.id} className="badge badge-renter-tag">
                                {a.name}
                              </span>
                            ))
                          )}
                        </div>
                      </div>
                    </div>

                    {/* Cột phải: Tài liệu pháp lý & Ảnh chụp */}
                    <div className="admin-detail-column-right">
                      <div className="admin-detail-section">
                        <h3 className="section-subtitle">Tài liệu pháp lý & Hình ảnh</h3>

                        <div className="admin-media-grid-vertical">
                          {selectedHouse.legalDocument && (
                            <>
                              <div className="media-container">
                                <span className="media-label">Mặt trước Giấy tờ Pháp lý</span>
                                <AdminImage
                                  label="Mặt trước Giấy tờ Pháp lý"
                                  src={assetUrl(selectedHouse.legalDocument.frontImageUrl || '')}
                                />
                              </div>
                              <div className="media-container">
                                <span className="media-label">Mặt sau Giấy tờ Pháp lý</span>
                                <AdminImage
                                  label="Mặt sau Giấy tờ Pháp lý"
                                  src={assetUrl(selectedHouse.legalDocument.backImageUrl || '')}
                                />
                              </div>
                              {selectedHouse.legalDocument.extraImageUrl && (
                                <div className="media-container">
                                  <span className="media-label">Tài liệu pháp lý bổ sung</span>
                                  <AdminImage
                                    label="Tài liệu bổ sung"
                                    src={assetUrl(selectedHouse.legalDocument.extraImageUrl)}
                                  />
                                </div>
                              )}
                            </>
                          )}

                          <div className="property-photos-gallery">
                            <span className="media-label">Hình ảnh thực tế khu trọ</span>
                            <div className="gallery-thumbnail-grid">
                              {selectedHouse.images.map((image) => (
                                <div key={image.id} className="gallery-thumbnail-item">
                                  <AdminImage
                                    label={image.isCover ? 'Ảnh bìa' : image.caption || 'Ảnh chi tiết'}
                                    src={assetUrl(image.imageUrl)}
                                  />
                                </div>
                              ))}
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              ) : (
                /* Bảng danh sách khu trọ đã public */
                <div className="admin-card">
                  <div className="table-responsive">
                    <table className="admin-table">
                      <thead>
                        <tr>
                          <th>Tên khu trọ</th>
                          <th>Chủ trọ</th>
                          <th>Địa chỉ</th>
                          <th>Trạng thái duyệt</th>
                          <th>Hiển thị</th>
                          <th>Ngày tạo</th>
                          <th style={{ textAlign: 'right' }}>Hành động</th>
                        </tr>
                      </thead>
                      <tbody>
                        {housesPublic.length === 0 ? (
                          <tr>
                            <td colSpan={7} style={{ textAlign: 'center', padding: '32px' }}>
                              Không tìm thấy khu trọ public nào.
                            </td>
                          </tr>
                        ) : (
                          housesPublic.map((house) => (
                            <tr key={house.id}>
                              <td><strong>{house.name}</strong></td>
                              <td>{house.landlordName} ({house.landlordEmail})</td>
                              <td>{house.addressDisplay}</td>
                              <td>
                                <span className="badge badge-completed">Approved</span>
                              </td>
                              <td>
                                <span className={`badge ${house.visibilityStatus === 'Visible' ? 'badge-active' : 'badge-suspended'}`}>
                                  {house.visibilityStatus}
                                </span>
                              </td>
                              <td>{new Date(house.createdAt).toLocaleDateString('vi-VN')}</td>
                              <td style={{ textAlign: 'right' }}>
                                <button
                                  type="button"
                                  className="admin-table-action-btn"
                                  onClick={() => void openHouse(house.id)}
                                >
                                  Xem chi tiết
                                </button>
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>

                  {/* Phân trang khu trọ public */}
                  <div className="admin-pagination">
                    <span className="pagination-info">
                      Hiển thị tối đa 20 dòng · Tổng cộng {housePublicTotal} khu trọ đã duyệt
                    </span>
                    <div className="pagination-actions">
                      <button
                        type="button"
                        className="pagination-btn"
                        disabled={pageParam <= 1}
                        onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: (pageParam - 1).toString() })}
                      >
                        Trước
                      </button>
                      <button
                        type="button"
                        className="pagination-btn"
                        disabled={pageParam * 20 >= housePublicTotal}
                        onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: (pageParam + 1).toString() })}
                      >
                        Sau
                      </button>
                    </div>
                  </div>
                </div>
              )
            )}

            {/* Tab: Khu trọ cần duyệt */}
            {activeMenu === 'houses' && activeTab === 'pending' && (
              selectedHouse ? (
                /* View Chi tiết khu trọ rộng toàn màn hình */
                <div className="admin-detail-card admin-detail-card--split">
                  <div className="admin-detail-card__header">
                    <button
                      type="button"
                      className="admin-back-btn"
                      onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() })}
                    >
                      <ArrowLeftIcon />
                      Quay lại danh sách
                    </button>
                    <h2>Chi tiết Khu trọ chờ duyệt</h2>
                  </div>

                  <div className="admin-detail-split-grid">
                    {/* Cột trái: Thông tin chi tiết & xử lý duyệt */}
                    <div className="admin-detail-column-left">
                      <div className="admin-detail-section">
                        <h3 className="section-subtitle">Thông tin cơ bản</h3>
                        <h2 className="property-title">{selectedHouse.name}</h2>

                        <div className="admin-grid-details" style={{ marginTop: '16px' }}>
                          <div className="admin-detail-item">
                            <label><UserIcon /> Chủ trọ</label>
                            <span>{selectedHouse.landlordName}</span>
                            <span className="sub-text">{selectedHouse.landlordEmail}</span>
                          </div>
                          <div className="admin-detail-item">
                            <label><MapPinIcon /> Địa chỉ hiển thị</label>
                            <span>{selectedHouse.addressDisplay}</span>
                          </div>
                          <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                            <label><MapPinIcon /> Địa chỉ chi tiết</label>
                            <span>{selectedHouse.addressLine}</span>
                          </div>
                          <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                            <label><DescriptionIcon /> Mô tả khu trọ</label>
                            <span className="property-description">{selectedHouse.description || 'Chưa cung cấp mô tả.'}</span>
                          </div>
                          {selectedHouse.legalDocument && (
                            <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                              <label><LegalIcon /> Số giấy tờ pháp lý (CCCD / Giấy chứng nhận)</label>
                              <span className="verified-citizen-id">
                                {selectedHouse.legalDocument.documentNumberMasked}
                              </span>
                            </div>
                          )}
                        </div>
                      </div>

                      {/* Tiện ích */}
                      <div className="admin-detail-section" style={{ marginTop: '24px' }}>
                        <h3 className="section-subtitle">Tiện ích khu trọ</h3>
                        <div className="admin-amenities-tags">
                          {selectedHouse.amenities.length === 0 ? (
                            <span className="no-amenities-text">Chưa đăng ký tiện ích nào.</span>
                          ) : (
                            selectedHouse.amenities.map((a) => (
                              <span key={a.id} className="badge badge-renter-tag">
                                {a.name}
                              </span>
                            ))
                          )}
                        </div>
                      </div>

                      {/* Action Approval */}
                      <div className="admin-action-area">
                        <h3 className="section-subtitle">Quyết định phê duyệt</h3>
                        <textarea
                          className="admin-reason-textarea"
                          value={reason}
                          onChange={(e) => setReason(e.target.value)}
                          placeholder="Nhập lý do từ chối (bắt buộc nếu nhấn từ chối duyệt khu trọ)..."
                        />
                        <div className="admin-action-buttons">
                          <button
                            type="button"
                            className="admin-btn admin-btn--success"
                            disabled={isSubmitting}
                            onClick={handleApprove}
                          >
                            <CheckIcon />
                            Duyệt & Cho phép Hoạt động
                          </button>
                          <button
                            type="button"
                            className="admin-btn admin-btn--danger"
                            disabled={isSubmitting}
                            onClick={handleReject}
                          >
                            <CloseIcon />
                            Từ chối duyệt
                          </button>
                        </div>
                      </div>
                    </div>

                    {/* Cột phải: Tài liệu pháp lý & Ảnh chụp */}
                    <div className="admin-detail-column-right">
                      <div className="admin-detail-section">
                        <h3 className="section-subtitle">Tài liệu pháp lý & Hình ảnh</h3>

                        <div className="admin-media-grid-vertical">
                          {selectedHouse.legalDocument && (
                            <>
                              <div className="media-container">
                                <span className="media-label">Mặt trước Giấy tờ Pháp lý</span>
                                <AdminImage
                                  label="Mặt trước Giấy tờ Pháp lý"
                                  src={assetUrl(selectedHouse.legalDocument.frontImageUrl || '')}
                                />
                              </div>
                              <div className="media-container">
                                <span className="media-label">Mặt sau Giấy tờ Pháp lý</span>
                                <AdminImage
                                  label="Mặt sau Giấy tờ Pháp lý"
                                  src={assetUrl(selectedHouse.legalDocument.backImageUrl || '')}
                                />
                              </div>
                              {selectedHouse.legalDocument.extraImageUrl && (
                                <div className="media-container">
                                  <span className="media-label">Tài liệu pháp lý bổ sung</span>
                                  <AdminImage
                                    label="Tài liệu bổ sung"
                                    src={assetUrl(selectedHouse.legalDocument.extraImageUrl)}
                                  />
                                </div>
                              )}
                            </>
                          )}

                          <div className="property-photos-gallery">
                            <span className="media-label">Hình ảnh thực tế khu trọ</span>
                            <div className="gallery-thumbnail-grid">
                              {selectedHouse.images.map((image) => (
                                <div key={image.id} className="gallery-thumbnail-item">
                                  <AdminImage
                                    label={image.isCover ? 'Ảnh bìa' : image.caption || 'Ảnh chi tiết'}
                                    src={assetUrl(image.imageUrl)}
                                  />
                                </div>
                              ))}
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              ) : (
                /* Bảng danh sách Khu trọ chờ duyệt */
                <div className="admin-card">
                  <div className="table-responsive">
                    <table className="admin-table">
                      <thead>
                        <tr>
                          <th>Tên khu trọ</th>
                          <th>Chủ trọ</th>
                          <th>Địa chỉ hiển thị</th>
                          <th>Ngày gửi</th>
                          <th style={{ textAlign: 'right' }}>Hành động</th>
                        </tr>
                      </thead>
                      <tbody>
                        {housesPending.length === 0 ? (
                          <tr>
                            <td colSpan={5} style={{ textAlign: 'center', padding: '32px' }}>
                              Không có khu trọ nào đang chờ duyệt.
                            </td>
                          </tr>
                        ) : (
                          housesPending.map((item) => (
                            <tr key={item.id}>
                              <td><strong>{item.name}</strong></td>
                              <td>{item.landlordName} ({item.landlordEmail})</td>
                              <td>{item.addressDisplay}</td>
                              <td>{new Date(item.createdAt).toLocaleDateString('vi-VN')}</td>
                              <td style={{ textAlign: 'right' }}>
                                <button
                                  type="button"
                                  className="admin-table-action-btn"
                                  onClick={() => void openHouse(item.id)}
                                >
                                  Xem chi tiết
                                </button>
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>

                  {/* Phân trang Khu trọ chờ duyệt */}
                  <div className="admin-pagination">
                    <span className="pagination-info">
                      Hiển thị tối đa 20 dòng · Tổng cộng {housePendingTotal} khu trọ chờ duyệt
                    </span>
                    <div className="pagination-actions">
                      <button
                        type="button"
                        className="pagination-btn"
                        disabled={pageParam <= 1}
                        onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: (pageParam - 1).toString() })}
                      >
                        Trước
                      </button>
                      <button
                        type="button"
                        className="pagination-btn"
                        disabled={pageParam * 20 >= housePendingTotal}
                        onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: (pageParam + 1).toString() })}
                      >
                        Sau
                      </button>
                    </div>
                  </div>
                </div>
              )
            )}
            {/* Tab: Báo cáo đánh giá */}
            {activeMenu === 'reports' && <AdminReviewReportsTab />}
          </>
        )}
        
        {!isLoading && activeMenu === 'provinces' && <AdminProvincesTab />}
        {!isLoading && activeMenu === 'amenities' && <AdminAmenitiesTab />}
        {!isLoading && activeMenu === 'billing-services' && <AdminBillingServicesTab />}
      </main>
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
