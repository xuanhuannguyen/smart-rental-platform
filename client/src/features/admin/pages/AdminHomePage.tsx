import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import { AdminImage } from '../components/AdminImage';
import { adminApprovalApi } from '../services/adminApprovalApi';
import type {
  AdminKycDetail,
  AdminKycListItem,
  AdminRoomingHouseDetail,
  AdminRoomingHouseListItem,
  AdminUserListItem,
  AdminUserDetail
} from '../types/adminApproval.types';
import './AdminHomePage.css';

type AdminMenu = 'users' | 'houses';
type UserTab = 'list' | 'kyc';
type HouseTab = 'public' | 'pending';

export function AdminHomePage() {
  const { currentUser, logout } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  // URL state management
  const activeMenu = (searchParams.get('menu') as AdminMenu) || 'users';
  const activeTab = searchParams.get('tab') || (activeMenu === 'users' ? 'list' : 'public');
  const pageParam = parseInt(searchParams.get('page') || '1', 10);
  const selectedId = searchParams.get('id') || null;

  // Application States
  const [users, setUsers] = useState<AdminUserListItem[]>([]);
  const [userTotalCount, setUserTotalCount] = useState(0);
  const [selectedUser, setSelectedUser] = useState<AdminUserDetail | null>(null);

  const [kycPendingItems, setKycPendingItems] = useState<AdminKycListItem[]>([]);
  const [kycPendingTotal, setKycPendingTotal] = useState(0);
  const [selectedKyc, setSelectedKyc] = useState<AdminKycDetail | null>(null);
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
  const [successMessage, setSuccessMessage] = useState('');

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
        setSelectedHouse(null);
        setKycHistory([]);

        if (activeMenu === 'users') {
          if (activeTab === 'list') {
            const response = await adminApprovalApi.getUsers(pageParam, 20);
            setUsers(response.data.items);
            setUserTotalCount(response.data.totalCount);
          } else if (activeTab === 'kyc') {
            const response = await adminApprovalApi.getPendingKyc(pageParam, 20);
            setKycPendingItems(response.data.items);
            setKycPendingTotal(response.data.totalCount);
          }
        } else if (activeMenu === 'houses') {
          if (activeTab === 'public') {
            const response = await adminApprovalApi.getPublicRoomingHouses(pageParam, 20);
            setHousesPublic(response.data.items);
            setHousePublicTotal(response.data.totalCount);
          } else if (activeTab === 'pending') {
            const response = await adminApprovalApi.getPendingRoomingHouses(pageParam, 20);
            setHousesPending(response.data.items);
            setHousePendingTotal(response.data.totalCount);
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
    setSuccessMessage('');
    setError('');
    setSearchParams({ menu, tab: menu === 'users' ? 'list' : 'public', page: '1' });
  }

  function handleTabChange(tab: string) {
    setSuccessMessage('');
    setError('');
    setSearchParams({ menu: activeMenu, tab, page: '1' });
  }

  function openUser(id: string) {
    setError('');
    setSuccessMessage('');
    setReason('');
    setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString(), id });
  }

  function openKyc(id: string) {
    setError('');
    setSuccessMessage('');
    setReason('');
    setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString(), id });
  }

  function openHouse(id: string) {
    setError('');
    setSuccessMessage('');
    setReason('');
    setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString(), id });
  }

  async function handleApprove() {
    setIsSubmitting(true);
    setError('');
    setSuccessMessage('');
    try {
      if (activeMenu === 'users' && activeTab === 'kyc' && selectedKyc) {
        const response = await adminApprovalApi.approveKyc(selectedKyc.id);
        setSuccessMessage(response.message ?? 'Đã duyệt KYC thành công.');
        setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() });
      } else if (activeMenu === 'houses' && activeTab === 'pending' && selectedHouse) {
        const response = await adminApprovalApi.approveRoomingHouse(selectedHouse.id);
        setSuccessMessage(response.message ?? 'Đã duyệt khu trọ thành công.');
        setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() });
      }
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể duyệt hồ sơ.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleReject() {
    if (!reason.trim()) {
      setError('Vui lòng nhập lý do từ chối.');
      return;
    }
    setIsSubmitting(true);
    setError('');
    setSuccessMessage('');
    try {
      if (activeMenu === 'users' && activeTab === 'kyc' && selectedKyc) {
        const response = await adminApprovalApi.rejectKyc(selectedKyc.id, reason);
        setSuccessMessage(response.message ?? 'Đã từ chối KYC.');
        setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() });
      } else if (activeMenu === 'houses' && activeTab === 'pending' && selectedHouse) {
        const response = await adminApprovalApi.rejectRoomingHouse(selectedHouse.id, reason);
        setSuccessMessage(response.message ?? 'Đã từ chối khu trọ.');
        setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() });
      }
      setReason('');
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể từ chối hồ sơ.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  function assetUrl(url: string) {
    return toAssetUrl(url);
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
            👤 Quản lý người dùng
          </button>
          <button
            type="button"
            className={`sidebar-menu-item ${activeMenu === 'houses' ? 'active' : ''}`}
            onClick={() => handleMenuChange('houses')}
          >
            🏠 Quản lý khu trọ
          </button>
        </nav>

        <div className="sidebar-footer">
          <div className="sidebar-user" title={currentUser?.email}>
            Admin: <strong>{currentUser?.displayName || currentUser?.email}</strong>
          </div>
          <button type="button" className="sidebar-logout-btn" onClick={() => void logout()}>
            🚪 Đăng xuất
          </button>
        </div>
      </aside>

      {/* Main Content bên phải */}
      <main className="admin-main-content">
        <div className="content-header">
          <h1>{activeMenu === 'users' ? 'Quản lý Người dùng' : 'Quản lý Khu trọ'}</h1>
          <p>Hệ thống giám sát, xác thực thành viên và khu trọ toàn nền tảng.</p>
        </div>

        {error && <Alert type="error">{error}</Alert>}
        {successMessage && <Alert type="success">{successMessage}</Alert>}

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
        ) : (
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
        )}

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
                  <div style={{ marginBottom: '24px' }}>
                    <button
                      type="button"
                      className="pagination-btn"
                      onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() })}
                      style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', fontWeight: '600' }}
                    >
                      ← Quay lại danh sách
                    </button>
                  </div>

                  <h2 className="detail-section-title">Chi tiết Người dùng</h2>
                  <div className="admin-grid-details">
                    <div className="admin-detail-item">
                      <label>Tên hiển thị</label>
                      <span>{selectedUser.displayName}</span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Email</label>
                      <span>{selectedUser.email}</span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Số điện thoại</label>
                      <span>{selectedUser.phoneNumber || '—'}</span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Vai trò</label>
                      <div style={{ display: 'flex', gap: '4px', flexWrap: 'wrap', marginTop: '4px' }}>
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
                    <div className="admin-detail-item">
                      <label>Ngày tham gia</label>
                      <span>{new Date(selectedUser.createdAt).toLocaleString('vi-VN')}</span>
                    </div>

                    {/* Hiển thị Profile cá nhân nếu có */}
                    {selectedUser.fullName && (
                      <>
                        <div className="admin-detail-item">
                          <label>Họ và tên thật</label>
                          <span>{selectedUser.fullName}</span>
                        </div>
                        <div className="admin-detail-item">
                          <label>Ngày sinh</label>
                          <span>{selectedUser.dateOfBirth ? new Date(selectedUser.dateOfBirth).toLocaleDateString('vi-VN') : '—'}</span>
                        </div>
                        <div className="admin-detail-item">
                          <label>Giới tính</label>
                          <span>{selectedUser.gender || '—'}</span>
                        </div>
                        <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                          <label>Địa chỉ liên hệ</label>
                          <span>{selectedUser.addressLine || '—'}</span>
                        </div>
                        <div className="admin-detail-item">
                          <label>Số CCCD đã xác thực</label>
                          <span style={{ color: 'var(--admin-primary-color)', fontWeight: '600' }}>
                            {selectedUser.verifiedCitizenIdMasked || '—'}
                          </span>
                        </div>
                        {selectedUser.emergencyContactName && (
                          <div className="admin-detail-item">
                            <label>Liên hệ khẩn cấp</label>
                            <span>{selectedUser.emergencyContactName} ({selectedUser.emergencyContactPhone || '—'})</span>
                          </div>
                        )}
                      </>
                    )}
                  </div>

                  {/* Hiển thị hình ảnh CCCD nếu đã được duyệt (Completed) */}
                  {selectedUser.onboardingStatus === 'Completed' && selectedUser.kycInfo ? (
                    <div className="admin-images-section" style={{ marginTop: '32px' }}>
                      <div className="admin-images-title">Hình ảnh CCCD & Selfie xác thực (Đã duyệt)</div>
                      <div className="admin-media-grid">
                        <AdminImage label="Mặt trước CCCD" src={assetUrl(selectedUser.kycInfo.frontImageUrl)} />
                        <AdminImage label="Mặt sau CCCD" src={assetUrl(selectedUser.kycInfo.backImageUrl)} />
                        <AdminImage label="Ảnh Selfie" src={assetUrl(selectedUser.kycInfo.selfieImageUrl)} />
                      </div>
                      
                      <div className="admin-grid-details" style={{ marginTop: '20px', borderTop: '1px solid rgba(255, 255, 255, 0.08)', paddingTop: '20px' }}>

                        <div className="admin-detail-item">
                          <label>Mức rủi ro KYC</label>
                          <span className={`badge ${selectedUser.kycInfo.riskLevel === 'High' ? 'badge-rejected' : 'badge-pending'}`}>
                            {selectedUser.kycInfo.riskLevel}
                          </span>
                        </div>
                        <div className="admin-detail-item">
                          <label>Kết quả hệ thống tự động (AI)</label>
                          <span style={{ fontWeight: '600' }}>
                            {selectedUser.kycInfo.ekycResult === 'Passed' ? 'Passed (Hợp lệ)' :
                             selectedUser.kycInfo.ekycResult === 'NeedReview' ? 'NeedReview (Cần hậu kiểm)' :
                             selectedUser.kycInfo.ekycResult === 'Failed' ? 'Failed (Không trùng khớp)' :
                             selectedUser.kycInfo.ekycResult === 'ProviderError' ? 'ProviderError (Lỗi nhà cung cấp)' :
                             selectedUser.kycInfo.ekycResult}
                          </span>
                        </div>
                        <div className="admin-detail-item">
                          <label>Trạng thái duyệt của Admin</label>
                          <span>
                            <span className="badge badge-active" style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                              Đã duyệt thành công (Approved) ✅
                            </span>
                          </span>
                        </div>
                        <div className="admin-detail-item">
                          <label>Ngày duyệt KYC</label>
                          <span>{selectedUser.kycInfo.approvedAt ? new Date(selectedUser.kycInfo.approvedAt).toLocaleString('vi-VN') : '—'}</span>
                        </div>
                      </div>
                    </div>
                  ) : (
                    selectedUser.onboardingStatus === 'Completed' && (
                      <div style={{ marginTop: '20px', color: 'var(--admin-text-muted)', fontSize: '0.9rem' }}>
                        * Tài khoản này đã hoàn thành xác thực nhưng dữ liệu hồ sơ KYC gốc không còn tồn tại hoặc không tìm thấy.
                      </div>
                    )
                  )}
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
                                  className="pagination-btn"
                                  onClick={() => void openUser(user.id)}
                                  style={{ padding: '6px 12px', fontSize: '0.8rem', backgroundColor: 'var(--admin-primary-color)', color: '#fff', border: 'none', cursor: 'pointer' }}
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
                <div className="admin-detail-card">
                  <div style={{ marginBottom: '24px' }}>
                    <button
                      type="button"
                      className="pagination-btn"
                      onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() })}
                      style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', fontWeight: '600' }}
                    >
                      ← Quay lại danh sách
                    </button>
                  </div>
                  
                  <h2 className="detail-section-title">Chi tiết Hồ sơ eKYC</h2>
                  <div className="admin-grid-details">
                    <div className="admin-detail-item">
                      <label>Tên chủ thẻ</label>
                      <span>{selectedKyc.ocrFullName || selectedKyc.userDisplayName}</span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Email đăng ký</label>
                      <span>{selectedKyc.userEmail}</span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Số CCCD / CMND</label>
                      <span>{selectedKyc.ocrCitizenIdMasked || 'Chưa nhận dạng được'}</span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Ngày sinh</label>
                      <span>
                        {selectedKyc.ocrDateOfBirth
                          ? new Date(selectedKyc.ocrDateOfBirth).toLocaleDateString('vi-VN')
                          : 'Chưa nhận dạng được'}
                      </span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Giới tính / Địa chỉ</label>
                      <span>
                        {selectedKyc.ocrGender || '—'} · {selectedKyc.ocrAddress || 'Chưa nhận dạng được'}
                      </span>
                    </div>
                  </div>

                  {/* Ảnh KYC */}
                  <div className="admin-images-section">
                    <div className="admin-images-title">Hình ảnh xác thực</div>
                    <div className="admin-media-grid">
                      <AdminImage label="Mặt trước CCCD" src={assetUrl(selectedKyc.frontImageUrl)} />
                      <AdminImage label="Mặt sau CCCD" src={assetUrl(selectedKyc.backImageUrl)} />
                      <AdminImage label="Ảnh Selfie" src={assetUrl(selectedKyc.selfieImageUrl)} />
                    </div>
                  </div>

                  {/* Lịch sử eKYC của người dùng này */}
                  <div className="kyc-history-section">
                    <h3 className="detail-section-title" style={{ fontSize: '1rem', borderBottom: 'none', marginBottom: '16px' }}>
                      Lịch sử duyệt KYC của tài khoản này
                    </h3>
                    {isLoadingKycHistory ? (
                      <div style={{ fontSize: '0.875rem', color: 'var(--admin-text-muted)' }}>Đang tải lịch sử...</div>
                    ) : kycHistory.length === 0 ? (
                      <div style={{ fontSize: '0.875rem', color: 'var(--admin-text-muted)' }}>Chưa từng có yêu cầu duyệt nào trước đó.</div>
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
                    <textarea
                      className="admin-reason-textarea"
                      value={reason}
                      onChange={(e) => setReason(e.target.value)}
                      placeholder="Nhập lý do từ chối (bắt buộc nếu nhấn từ chối hồ sơ)..."
                    />
                    <div className="admin-action-buttons">
                      <Button type="button" disabled={isSubmitting} onClick={handleApprove}>
                        Phê duyệt hồ sơ
                      </Button>
                      <Button type="button" variant="danger" disabled={isSubmitting} onClick={handleReject}>
                        Từ chối duyệt
                      </Button>
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
                                  className="pagination-btn"
                                  onClick={() => void openKyc(item.id)}
                                  style={{ padding: '6px 12px', fontSize: '0.8rem', backgroundColor: 'var(--admin-primary-color)', color: '#fff', border: 'none', cursor: 'pointer' }}
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
                <div className="admin-detail-card">
                  <div style={{ marginBottom: '24px' }}>
                    <button
                      type="button"
                      className="pagination-btn"
                      onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() })}
                      style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', fontWeight: '600' }}
                    >
                      ← Quay lại danh sách
                    </button>
                  </div>

                  <h2 className="detail-section-title">{selectedHouse.name}</h2>
                  <div className="admin-grid-details">
                    <div className="admin-detail-item">
                      <label>Chủ trọ</label>
                      <span>{selectedHouse.landlordName} ({selectedHouse.landlordEmail})</span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Địa chỉ hiển thị</label>
                      <span>{selectedHouse.addressDisplay}</span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Địa chỉ chi tiết</label>
                      <span>{selectedHouse.addressLine}</span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Trạng thái duyệt</label>
                      <span><span className="badge badge-completed">Approved</span></span>
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
                      <label>Mô tả khu trọ</label>
                      <span>{selectedHouse.description || 'Chưa cung cấp mô tả.'}</span>
                    </div>
                    {selectedHouse.legalDocument && (
                      <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                        <label>Số giấy tờ pháp lý (CCCD / Giấy chứng nhận quyền sở hữu)</label>
                        <span style={{ color: 'var(--admin-primary-color)', fontWeight: '600' }}>
                          {selectedHouse.legalDocument.documentNumberMasked}
                        </span>
                      </div>
                    )}
                  </div>

                  {/* Tiện ích */}
                  <div style={{ marginBottom: '28px' }}>
                    <div className="admin-images-title">Tiện ích khu trọ</div>
                    <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                      {selectedHouse.amenities.length === 0 ? (
                        <span style={{ fontSize: '0.85rem', color: 'var(--admin-text-muted)' }}>Chưa đăng ký tiện ích nào.</span>
                      ) : (
                        selectedHouse.amenities.map((a) => (
                          <span key={a.id} className="badge badge-renter" style={{ padding: '6px 12px' }}>
                            {a.name}
                          </span>
                        ))
                      )}
                    </div>
                  </div>

                  {/* Ảnh khu trọ & Giấy tờ */}
                  <div className="admin-images-section">
                    <div className="admin-images-title">Hình ảnh & Giấy tờ pháp lý</div>
                    <div className="admin-media-grid">
                      {selectedHouse.legalDocument && (
                        <>
                          <a
                            href={assetUrl(`/uploads/${selectedHouse.legalDocument.frontImageObjectKey}`)}
                            target="_blank"
                            rel="noreferrer"
                            className="admin-image-box"
                          >
                            <img src={assetUrl(`/uploads/${selectedHouse.legalDocument.frontImageObjectKey}`)} alt="Giấy tờ mặt trước" />
                            <span>Giấy tờ mặt trước</span>
                          </a>
                          <a
                            href={assetUrl(`/uploads/${selectedHouse.legalDocument.backImageObjectKey}`)}
                            target="_blank"
                            rel="noreferrer"
                            className="admin-image-box"
                          >
                            <img src={assetUrl(`/uploads/${selectedHouse.legalDocument.backImageObjectKey}`)} alt="Giấy tờ mặt sau" />
                            <span>Giấy tờ mặt sau</span>
                          </a>
                          {selectedHouse.legalDocument.extraImageObjectKey && (
                            <a
                              href={assetUrl(`/uploads/${selectedHouse.legalDocument.extraImageObjectKey}`)}
                              target="_blank"
                              rel="noreferrer"
                              className="admin-image-box"
                            >
                              <img src={assetUrl(`/uploads/${selectedHouse.legalDocument.extraImageObjectKey}`)} alt="Giấy tờ khác" />
                              <span>Giấy tờ khác</span>
                            </a>
                          )}
                        </>
                      )}
                      {selectedHouse.images.map((image) => (
                        <a
                          key={image.id}
                          href={assetUrl(image.imageUrl)}
                          target="_blank"
                          rel="noreferrer"
                          className="admin-image-box"
                        >
                          <img src={assetUrl(image.imageUrl)} alt={image.isCover ? 'Ảnh bìa khu trọ' : image.caption || 'Ảnh chi tiết'} />
                          <span>{image.isCover ? 'Ảnh bìa khu trọ' : image.caption || 'Ảnh chi tiết'}</span>
                        </a>
                      ))}
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
                                  className="pagination-btn"
                                  onClick={() => void openHouse(house.id)}
                                  style={{ padding: '6px 12px', fontSize: '0.8rem', backgroundColor: 'var(--admin-primary-color)', color: '#fff', border: 'none', cursor: 'pointer' }}
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
                <div className="admin-detail-card">
                  <div style={{ marginBottom: '24px' }}>
                    <button
                      type="button"
                      className="pagination-btn"
                      onClick={() => setSearchParams({ menu: activeMenu, tab: activeTab, page: pageParam.toString() })}
                      style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', fontWeight: '600' }}
                    >
                      ← Quay lại danh sách
                    </button>
                  </div>

                  <h2 className="detail-section-title">{selectedHouse.name}</h2>
                  <div className="admin-grid-details">
                    <div className="admin-detail-item">
                      <label>Chủ trọ</label>
                      <span>{selectedHouse.landlordName} ({selectedHouse.landlordEmail})</span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Địa chỉ hiển thị</label>
                      <span>{selectedHouse.addressDisplay}</span>
                    </div>
                    <div className="admin-detail-item">
                      <label>Địa chỉ chi tiết</label>
                      <span>{selectedHouse.addressLine}</span>
                    </div>
                    <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                      <label>Mô tả khu trọ</label>
                      <span>{selectedHouse.description || 'Chưa cung cấp mô tả.'}</span>
                    </div>
                    {selectedHouse.legalDocument && (
                      <div className="admin-detail-item" style={{ gridColumn: 'span 2' }}>
                        <label>Số giấy tờ pháp lý (CCCD / Giấy chứng nhận quyền sở hữu)</label>
                        <span style={{ color: 'var(--admin-primary-color)', fontWeight: '600' }}>
                          {selectedHouse.legalDocument.documentNumberMasked}
                        </span>
                      </div>
                    )}
                  </div>

                  {/* Tiện ích */}
                  <div style={{ marginBottom: '28px' }}>
                    <div className="admin-images-title">Tiện ích khu trọ</div>
                    <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                      {selectedHouse.amenities.length === 0 ? (
                        <span style={{ fontSize: '0.85rem', color: 'var(--admin-text-muted)' }}>Chưa đăng ký tiện ích nào.</span>
                      ) : (
                        selectedHouse.amenities.map((a) => (
                          <span key={a.id} className="badge badge-renter" style={{ padding: '6px 12px' }}>
                            {a.name}
                          </span>
                        ))
                      )}
                    </div>
                  </div>

                  {/* Ảnh khu trọ & Giấy tờ */}
                  <div className="admin-images-section">
                    <div className="admin-images-title">Hình ảnh & Giấy tờ pháp lý</div>
                    <div className="admin-media-grid">
                      {selectedHouse.legalDocument && (
                        <>
                          <a
                            href={assetUrl(`/uploads/${selectedHouse.legalDocument.frontImageObjectKey}`)}
                            target="_blank"
                            rel="noreferrer"
                            className="admin-image-box"
                          >
                            <img src={assetUrl(`/uploads/${selectedHouse.legalDocument.frontImageObjectKey}`)} alt="Giấy tờ mặt trước" />
                            <span>Giấy tờ mặt trước</span>
                          </a>
                          <a
                            href={assetUrl(`/uploads/${selectedHouse.legalDocument.backImageObjectKey}`)}
                            target="_blank"
                            rel="noreferrer"
                            className="admin-image-box"
                          >
                            <img src={assetUrl(`/uploads/${selectedHouse.legalDocument.backImageObjectKey}`)} alt="Giấy tờ mặt sau" />
                            <span>Giấy tờ mặt sau</span>
                          </a>
                          {selectedHouse.legalDocument.extraImageObjectKey && (
                            <a
                              href={assetUrl(`/uploads/${selectedHouse.legalDocument.extraImageObjectKey}`)}
                              target="_blank"
                              rel="noreferrer"
                              className="admin-image-box"
                            >
                              <img src={assetUrl(`/uploads/${selectedHouse.legalDocument.extraImageObjectKey}`)} alt="Giấy tờ khác" />
                              <span>Giấy tờ khác</span>
                            </a>
                          )}
                        </>
                      )}
                      {selectedHouse.images.map((image) => (
                        <a
                          key={image.id}
                          href={assetUrl(image.imageUrl)}
                          target="_blank"
                          rel="noreferrer"
                          className="admin-image-box"
                        >
                          <img src={assetUrl(image.imageUrl)} alt={image.isCover ? 'Ảnh bìa khu trọ' : image.caption || 'Ảnh chi tiết'} />
                          <span>{image.isCover ? 'Ảnh bìa khu trọ' : image.caption || 'Ảnh chi tiết'}</span>
                        </a>
                      ))}
                    </div>
                  </div>

                  {/* Action Approval */}
                  <div className="admin-action-area">
                    <textarea
                      className="admin-reason-textarea"
                      value={reason}
                      onChange={(e) => setReason(e.target.value)}
                      placeholder="Nhập lý do từ chối (bắt buộc nếu nhấn từ chối duyệt khu trọ)..."
                    />
                    <div className="admin-action-buttons">
                      <Button type="button" disabled={isSubmitting} onClick={handleApprove}>
                        Duyệt & Cho phép Hoạt động
                      </Button>
                      <Button type="button" variant="danger" disabled={isSubmitting} onClick={handleReject}>
                        Từ chối duyệt
                      </Button>
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
                                  className="pagination-btn"
                                  onClick={() => void openHouse(item.id)}
                                  style={{ padding: '6px 12px', fontSize: '0.8rem', backgroundColor: 'var(--admin-primary-color)', color: '#fff', border: 'none', cursor: 'pointer' }}
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
          </>
        )}
      </main>
    </div>
  );
}
