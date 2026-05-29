import { useCallback, useEffect, useState, useRef, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import { kycApi } from '../../kyc/services/kycApi';
import type { KycStatusResponse } from '../../kyc/types/kyc.types';
import { profileApi } from '../services/profileApi';
import type { UserProfileResponse, UserSession } from '../types/profile.types';
import { authApi } from '../../auth/services/authApi';
import { OtpInput } from '../../../shared/components/ui/OtpInput';
import { uploadImage } from '../../files/api';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { AvatarCropper, cropAvatar } from '../../../shared/components/ui/AvatarCropper';
import './MyProfilePage.css';

function display(value?: string | null) {
  return value && value.trim() ? value : 'Chưa có';
}

function formatDate(value?: string | null) {
  return value ? value.slice(0, 10) : 'Chưa có';
}

function getKycMessage(kycStatus?: string | null) {
  if (!kycStatus) {
    return 'Bạn chưa gửi KYC. Hãy xác thực KYC để hệ thống cập nhật hồ sơ định danh.';
  }

  if (kycStatus === 'PendingAdminReview') {
    return 'Hồ sơ KYC của bạn đang được admin duyệt.';
  }

  if (kycStatus === 'Approved') {
    return 'KYC đã được duyệt. Thông tin định danh bên dưới được lấy từ hồ sơ KYC.';
  }

  if (kycStatus === 'Rejected') {
    return 'KYC đã bị từ chối. Vui lòng gửi lại hồ sơ KYC.';
  }

  if (kycStatus === 'EkycFailed') {
    return 'KYC chưa hoàn tất do lỗi eKYC. Vui lòng gửi lại ảnh rõ hơn.';
  }

  return `Trạng thái KYC hiện tại: ${kycStatus}.`;
}

function parseUserAgent(uaString?: string | null): string {
  if (!uaString) return 'Thiết bị không xác định';

  const ua = uaString.toLowerCase();
  let os = 'Hệ điều hành không xác định';
  let browser = 'Trình duyệt không xác định';

  // Parse OS
  if (ua.includes('windows')) os = 'Windows';
  else if (ua.includes('macintosh') || ua.includes('mac os')) os = 'macOS';
  else if (ua.includes('iphone') || ua.includes('ipad') || ua.includes('ipod')) os = 'iOS';
  else if (ua.includes('android')) os = 'Android';
  else if (ua.includes('linux')) os = 'Linux';

  // Parse Browser
  if (ua.includes('edg/')) browser = 'Edge';
  else if (ua.includes('chrome') && !ua.includes('chromium')) browser = 'Chrome';
  else if (ua.includes('safari') && !ua.includes('chrome')) browser = 'Safari';
  else if (ua.includes('firefox')) browser = 'Firefox';
  else if (ua.includes('opr/') || ua.includes('opera')) browser = 'Opera';

  return `${browser} trên ${os}`;
}

type SecurityMode = 'current-password' | 'email-otp' | 'devices';

export function MyProfilePage() {
  const { currentUser, clearSession, refreshMe } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  
  // Tab hiện tại: mặc định là 'info'
  const activeTab = searchParams.get('tab') || 'info';

  // --- State của Profile Info ---
  const [profile, setProfile] = useState<UserProfileResponse | null>(null);
  const [latestKyc, setLatestKyc] = useState<KycStatusResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [profileSuccessMessage, setProfileSuccessMessage] = useState<string | null>(null);

  const [profileForm, setProfileForm] = useState({
    displayName: '',
    phoneNumber: '',
    emergencyContactName: '',
    emergencyContactPhone: '',
    avatarUrl: ''
  });

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [showCropper, setShowCropper] = useState(false);
  const [rawImageSrc, setRawImageSrc] = useState<string | null>(null);
  const [cropParams, setCropParams] = useState<{ zoom: number; position: { x: number; y: number } }>({
    zoom: 1,
    position: { x: 0, y: 0 }
  });
  const [isCropChanged, setIsCropChanged] = useState(false);

  const [isSavingProfile, setIsSavingProfile] = useState(false);
  const [isEditingProfile, setIsEditingProfile] = useState(false);
  
  const [securityMode, setSecurityMode] = useState<SecurityMode>('current-password');
  
  // --- State của Quản lý thiết bị ---
  const [sessions, setSessions] = useState<UserSession[]>([]);
  const [isLoadingSessions, setIsLoadingSessions] = useState(false);
  const [sessionsError, setSessionsError] = useState<string | null>(null);
  const [currentPasswordForm, setCurrentPasswordForm] = useState({
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  });
  const [otpForm, setOtpForm] = useState({
    email: currentUser?.email ?? '',
    otp: '',
    newPassword: '',
    confirmPassword: ''
  });
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSendingOtp, setIsSendingOtp] = useState(false);
  const [otpVerified, setOtpVerified] = useState(false);
  const [isVerifyingOtp, setIsVerifyingOtp] = useState(false);
  const [securityMessage, setSecurityMessage] = useState<string | null>(null);
  const [securityError, setSecurityError] = useState<string | null>(null);

  const loadProfile = useCallback(async () => {
    setIsLoading(true);
    setProfileError(null);

    try {
      const [profileResponse, kycResponse] = await Promise.all([
        profileApi.getProfile(),
        kycApi.getMyStatus()
      ]);
      setProfile(profileResponse.data);
      setLatestKyc(kycResponse.data);
      setProfileForm({
        displayName: profileResponse.data?.displayName || '',
        phoneNumber: profileResponse.data?.phoneNumber || '',
        emergencyContactName: profileResponse.data?.emergencyContactName || '',
        emergencyContactPhone: profileResponse.data?.emergencyContactPhone || '',
        avatarUrl: profileResponse.data?.avatarUrl || ''
      });
    } catch (loadError) {
      setProfileError(getApiErrorMessage(loadError, 'Không thể tải hồ sơ.'));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadProfile();
  }, [loadProfile]);

  // Track active blob URLs to clean up on unmount without premature reactive revocation
  const previewUrlRef = useRef<string | null>(null);
  const rawImageSrcRef = useRef<string | null>(null);

  useEffect(() => {
    previewUrlRef.current = previewUrl;
  }, [previewUrl]);

  useEffect(() => {
    rawImageSrcRef.current = rawImageSrc;
  }, [rawImageSrc]);

  useEffect(() => {
    return () => {
      if (previewUrlRef.current) {
        URL.revokeObjectURL(previewUrlRef.current);
      }
      if (rawImageSrcRef.current && rawImageSrcRef.current !== previewUrlRef.current) {
        URL.revokeObjectURL(rawImageSrcRef.current);
      }
    };
  }, []);

  if (!currentUser) {
    return null;
  }

  const displayedKycStatus = profile?.kycStatus ?? (latestKyc?.hasSubmission ? latestKyc.status : null);

  // --- Handler cho Profile Info ---
  async function handleSaveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setProfileError(null);
    setProfileSuccessMessage(null);

    if (!profileForm.displayName.trim()) {
      setProfileError('Tên hiển thị không được để trống.');
      return;
    }

    setIsSavingProfile(true);

    try {
      let finalAvatarUrl = profileForm.avatarUrl;
      if (selectedFile || isCropChanged) {
        const srcToCrop = previewUrl || toAssetUrl(profileForm.avatarUrl);
        const croppedFile = await cropAvatar(srcToCrop, cropParams.zoom, cropParams.position);
        const uploadResult = await uploadImage(croppedFile, 'Avatar');
        finalAvatarUrl = uploadResult.url;
      }

      const response = await profileApi.updateProfile({
        displayName: profileForm.displayName.trim(),
        phoneNumber: profileForm.phoneNumber.trim() || null,
        emergencyContactName: profileForm.emergencyContactName.trim() || null,
        emergencyContactPhone: profileForm.emergencyContactPhone.trim() || null,
        avatarUrl: finalAvatarUrl || null
      });

      setProfile(response.data);
      setProfileForm(current => ({
        ...current,
        avatarUrl: response.data?.avatarUrl || ''
      }));

      // Reset các state upload tạm
      setSelectedFile(null);
      setCropParams({ zoom: 1, position: { x: 0, y: 0 } });
      setIsCropChanged(false);
      if (previewUrl) {
        URL.revokeObjectURL(previewUrl);
        setPreviewUrl(null);
      }

      // Refresh Auth Context để cập nhật Header ngay lập tức
      await refreshMe();

      setProfileSuccessMessage('Cập nhật thông tin hồ sơ thành công.');
      setIsEditingProfile(false);
    } catch (saveError) {
      setProfileError(getApiErrorMessage(saveError, 'Không thể cập nhật hồ sơ.'));
    } finally {
      setIsSavingProfile(false);
    }
  }

  // --- Handlers cho Security Tab ---
  async function handleChangeWithCurrentPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSecurityError(null);
    setSecurityMessage(null);

    if (!currentPasswordForm.currentPassword || !currentPasswordForm.newPassword) {
      setSecurityError('Vui lòng nhập mật khẩu hiện tại và mật khẩu mới.');
      return;
    }

    if (currentPasswordForm.newPassword.length < 6) {
      setSecurityError('Mật khẩu mới phải có ít nhất 6 ký tự.');
      return;
    }

    if (currentPasswordForm.newPassword !== currentPasswordForm.confirmPassword) {
      setSecurityError('Xác nhận mật khẩu mới không khớp.');
      return;
    }

    setIsSubmitting(true);

    try {
      await authApi.changePassword({
        currentPassword: currentPasswordForm.currentPassword,
        newPassword: currentPasswordForm.newPassword
      });
      clearSession();
      navigate(ROUTE_PATHS.AUTH.LOGIN, { replace: true });
    } catch (changeError) {
      setSecurityError(getApiErrorMessage(changeError, 'Không thể đổi mật khẩu.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleSendOtp() {
    setSecurityError(null);
    setSecurityMessage(null);

    if (!otpForm.email.trim()) {
      setSecurityError('Vui lòng nhập email.');
      return;
    }

    setIsSendingOtp(true);

    try {
      await authApi.forgotPassword({ email: otpForm.email.trim() });
      setSecurityMessage('OTP đặt lại mật khẩu đã được gửi. Vui lòng kiểm tra Gmail.');
    } catch (sendError) {
      setSecurityError(getApiErrorMessage(sendError, 'Không thể gửi OTP.'));
    } finally {
      setIsSendingOtp(false);
    }
  }

  async function handleVerifyOtp() {
    setSecurityError(null);
    setSecurityMessage(null);

    if (!otpForm.email.trim() || !otpForm.otp.trim()) {
      setSecurityError('Vui lòng nhập email và mã OTP.');
      return;
    }

    setIsVerifyingOtp(true);

    try {
      await authApi.verifyResetOtp({
        email: otpForm.email.trim(),
        otp: otpForm.otp.trim()
      });
      setOtpVerified(true);
      setSecurityMessage('OTP hợp lệ! Vui lòng nhập mật khẩu mới bên dưới.');
    } catch (verifyError) {
      setSecurityError(getApiErrorMessage(verifyError, 'OTP không hợp lệ hoặc đã hết hạn.'));
    } finally {
      setIsVerifyingOtp(false);
    }
  }

  async function handleResetWithOtp(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSecurityError(null);
    setSecurityMessage(null);

    if (!otpForm.newPassword) {
      setSecurityError('Vui lòng nhập mật khẩu mới.');
      return;
    }

    if (otpForm.newPassword.length < 6) {
      setSecurityError('Mật khẩu mới phải có ít nhất 6 ký tự.');
      return;
    }

    if (otpForm.newPassword !== otpForm.confirmPassword) {
      setSecurityError('Xác nhận mật khẩu mới không khớp.');
      return;
    }

    setIsSubmitting(true);

    try {
      await authApi.resetPassword({
        email: otpForm.email.trim(),
        otp: otpForm.otp.trim(),
        newPassword: otpForm.newPassword
      });
      clearSession();
      navigate(ROUTE_PATHS.AUTH.LOGIN, { replace: true });
    } catch (resetError) {
      setSecurityError(getApiErrorMessage(resetError, 'Không thể đặt lại mật khẩu.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  const loadSessions = useCallback(async () => {
    setIsLoadingSessions(true);
    setSessionsError(null);
    try {
      const response = await profileApi.getActiveSessions();
      setSessions(response.data || []);
    } catch (err) {
      console.error('[Sessions] Failed to load:', err);
      setSessionsError(getApiErrorMessage(err, 'Không thể tải danh sách thiết bị.'));
    } finally {
      setIsLoadingSessions(false);
    }
  }, []);

  useEffect(() => {
    if (activeTab === 'security' && securityMode === 'devices') {
      void loadSessions();
    }
  }, [activeTab, securityMode, loadSessions]);

  async function handleRevokeSession(sessionId: string, isCurrentSession: boolean) {
    const confirmMsg = isCurrentSession
      ? 'Bạn đang đăng xuất khỏi thiết bị hiện tại. Bạn sẽ phải đăng nhập lại. Tiếp tục?'
      : 'Bạn có chắc chắn muốn đăng xuất khỏi thiết bị này?';
    if (!window.confirm(confirmMsg)) {
      return;
    }
    setSecurityError(null);
    setSecurityMessage(null);
    try {
      await profileApi.revokeSession(sessionId);
      if (isCurrentSession) {
        clearSession();
        navigate(ROUTE_PATHS.AUTH.LOGIN, { replace: true });
        return;
      }
      setSecurityMessage('Đã đăng xuất thiết bị thành công.');
      void loadSessions();
    } catch (err) {
      setSecurityError(getApiErrorMessage(err, 'Không thể đăng xuất thiết bị.'));
    }
  }

  return (
    <div className="profile-container">
      <div className="profile-layout">
        {/* Sidebar */}
        <aside className="profile-sidebar">
          <h2>Cài đặt tài khoản</h2>
          <button
            type="button"
            className={`profile-sidebar-item ${activeTab === 'info' ? 'active' : ''}`}
            onClick={() => setSearchParams({ tab: 'info' })}
          >
            Cập nhật thông tin
          </button>
          <button
            type="button"
            className={`profile-sidebar-item ${activeTab === 'security' ? 'active' : ''}`}
            onClick={() => setSearchParams({ tab: 'security' })}
          >
            Quản lý bảo mật
          </button>
          <button
            type="button"
            className="profile-sidebar-item sidebar-back-btn"
            onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}
          >
            ← Quay lại trang chủ
          </button>
        </aside>

        {/* Content Area */}
        <main className="profile-content">

          {activeTab === 'info' ? (
            <section className="profile-section">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <h1>Thông tin hồ sơ</h1>
                {!isEditingProfile && !isLoading && (
                  <Button type="button" onClick={() => setIsEditingProfile(true)}>
                    Chỉnh sửa
                  </Button>
                )}
              </div>
              <p className="subtle">
                Cập nhật thông tin cá nhân và thông tin liên hệ của bạn bên dưới.
              </p>

              {isLoading ? <LoadingState message="Đang tải hồ sơ..." /> : null}

              {!isLoading ? (
                <form className="auth-form" onSubmit={handleSaveProfile}>
                  {profileError ? <Alert type="error">{profileError}</Alert> : null}
                  {profileSuccessMessage ? <Alert type="success">{profileSuccessMessage}</Alert> : null}

                  {/* Avatar Section */}
                  <div className="profile-avatar-section">
                    {isEditingProfile ? (
                      <div className="profile-avatar-container is-editing">
                        {previewUrl || (profileForm.avatarUrl && profileForm.avatarUrl.trim() !== '') ? (
                          <>
                            {previewUrl || isCropChanged ? (
                              <div className="profile-avatar-preview-wrapper">
                                <img
                                  src={previewUrl || toAssetUrl(profileForm.avatarUrl)}
                                  alt="Avatar"
                                  className="profile-avatar-preview-img"
                                  style={{
                                    transform: `translate(${cropParams.position.x * 0.5}px, ${cropParams.position.y * 0.5}px) scale(${cropParams.zoom * 1.6})`
                                  }}
                                />
                              </div>
                            ) : (
                              <img
                                src={toAssetUrl(profileForm.avatarUrl)}
                                alt="Avatar"
                                className="profile-avatar-preview"
                              />
                            )}
                            <button 
                              type="button"
                              className="profile-avatar-edit-overlay"
                              title="Chỉnh sửa khung ảnh"
                              onClick={() => {
                                const currentSrc = previewUrl || toAssetUrl(profileForm.avatarUrl);
                                if (currentSrc) {
                                  // Append cache buster parameter to avoid CORS cache issues for remote/local static URLs
                                  const finalSrc = currentSrc.startsWith('blob:') 
                                    ? currentSrc 
                                    : `${currentSrc}${currentSrc.includes('?') ? '&' : '?'}t=${Date.now()}`;
                                  setRawImageSrc(finalSrc);
                                  setShowCropper(true);
                                }
                              }}
                            >
                              <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
                                <path strokeLinecap="round" strokeLinejoin="round" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                              </svg>
                            </button>
                          </>
                        ) : (
                          <div className="profile-avatar-fallback">
                            {profileForm.displayName
                              ? profileForm.displayName.split(' ').map((n) => n[0]).join('').substring(0, 2).toUpperCase()
                              : 'U'}
                          </div>
                        )}

                        <label className="profile-avatar-upload-btn" htmlFor="avatar-file-input">
                          <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
                            <path strokeLinecap="round" strokeLinejoin="round" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
                            <path strokeLinecap="round" strokeLinejoin="round" d="M15 13a3 3 0 11-6 0 3 3 0 016 0z" />
                          </svg>
                        </label>

                        <input
                          type="file"
                          id="avatar-file-input"
                          accept="image/jpeg,image/png,image/webp"
                          style={{ display: 'none' }}
                          onChange={e => {
                            const file = e.target.files?.[0];
                            if (file) {
                              const objectUrl = URL.createObjectURL(file);
                              setSelectedFile(file);
                              setRawImageSrc(objectUrl);
                              setCropParams({ zoom: 1, position: { x: 0, y: 0 } });
                              setShowCropper(true);
                              e.target.value = '';
                            }
                          }}
                        />
                      </div>
                    ) : (
                      <div className="profile-avatar-container">
                        {profileForm.avatarUrl && profileForm.avatarUrl.trim() !== '' ? (
                          <img
                            src={toAssetUrl(profileForm.avatarUrl)}
                            alt="Avatar"
                            className="profile-avatar-preview"
                          />
                        ) : (
                          <div className="profile-avatar-fallback">
                            {profileForm.displayName
                              ? profileForm.displayName.split(' ').map((n) => n[0]).join('').substring(0, 2).toUpperCase()
                              : 'U'}
                          </div>
                        )}
                      </div>
                    )}
                    <div className="profile-avatar-info">
                      {isEditingProfile ? (
                        <span className="profile-avatar-hint">
                          {profile?.isGoogleUser ? 'Đang dùng ảnh Google. ' : ''}Click ngòi bút để chỉnh sửa khung tròn, click biểu tượng camera để đổi ảnh
                        </span>
                      ) : profile?.isGoogleUser ? (
                        <span className="profile-avatar-badge google-badge">
                          Tài khoản liên kết Google (Có thể thay đổi ảnh)
                        </span>
                      ) : null}
                    </div>
                  </div>

                  <div className="profile-grid">
                    <FormField label="Tên hiển thị" htmlFor="profile-display-name">
                      <input
                        id="profile-display-name"
                        className="ui-input"
                        value={profileForm.displayName}
                        disabled={!isEditingProfile || isSavingProfile}
                        onChange={event =>
                          setProfileForm(current => ({
                            ...current,
                            displayName: event.target.value
                          }))
                        }
                      />
                    </FormField>

                    <FormField label="Số điện thoại liên hệ" htmlFor="profile-phone-number">
                      <input
                        id="profile-phone-number"
                        className="ui-input"
                        value={profileForm.phoneNumber}
                        disabled={!isEditingProfile || isSavingProfile}
                        onChange={event =>
                          setProfileForm(current => ({
                            ...current,
                            phoneNumber: event.target.value
                          }))
                        }
                      />
                    </FormField>
                  </div>

                  <div className="profile-grid">
                    <FormField label="Tên liên hệ khẩn cấp" htmlFor="profile-emergency-name">
                      <input
                        id="profile-emergency-name"
                        className="ui-input"
                        value={profileForm.emergencyContactName}
                        disabled={!isEditingProfile || isSavingProfile}
                        onChange={event =>
                          setProfileForm(current => ({
                            ...current,
                            emergencyContactName: event.target.value
                          }))
                        }
                      />
                    </FormField>

                    <FormField label="Số điện thoại khẩn cấp" htmlFor="profile-emergency-phone">
                      <input
                        id="profile-emergency-phone"
                        className="ui-input"
                        value={profileForm.emergencyContactPhone}
                        disabled={!isEditingProfile || isSavingProfile}
                        onChange={event =>
                          setProfileForm(current => ({
                            ...current,
                            emergencyContactPhone: event.target.value
                          }))
                        }
                      />
                    </FormField>
                  </div>

                  {isEditingProfile && (
                    <div className="auth-actions" style={{ marginTop: '16px', display: 'flex', gap: '12px' }}>
                      <Button type="submit" disabled={isSavingProfile}>
                        {isSavingProfile ? 'Đang lưu...' : 'Lưu thay đổi'}
                      </Button>
                      <Button type="button" variant="secondary" disabled={isSavingProfile} onClick={() => {
                        setIsEditingProfile(false);
                        setProfileError(null);
                        setProfileSuccessMessage(null);
                        // Khôi phục lại dữ liệu ban đầu
                        setProfileForm({
                          displayName: profile?.displayName || '',
                          phoneNumber: profile?.phoneNumber || '',
                          emergencyContactName: profile?.emergencyContactName || '',
                          emergencyContactPhone: profile?.emergencyContactPhone || '',
                          avatarUrl: profile?.avatarUrl || ''
                        });
                        setSelectedFile(null);
                        setCropParams({ zoom: 1, position: { x: 0, y: 0 } });
                        setIsCropChanged(false);
                        if (previewUrl) {
                          URL.revokeObjectURL(previewUrl);
                          setPreviewUrl(null);
                        }
                      }}>
                        Hủy
                      </Button>
                    </div>
                  )}
                </form>
              ) : null}

              {!isLoading ? (
                <div style={{ marginTop: '32px' }}>
                  <hr style={{ margin: '32px 0', border: 'none', borderTop: '1px solid #dce4ef' }} />
                  
                  <h2 className="section-title">Thông tin định danh (eKYC)</h2>
                  <p className="subtle" style={{ marginBottom: '16px' }}>
                    Thông tin định danh được lấy trực tiếp từ hồ sơ eKYC của bạn và không thể chỉnh sửa thủ công.
                  </p>

                  <Alert type={displayedKycStatus === 'Approved' ? 'success' : 'info'}>
                    {getKycMessage(displayedKycStatus)}
                  </Alert>

                  <dl className="user-summary" style={{ marginTop: '24px' }}>
                    <div>
                      <dt>Họ tên</dt>
                      <dd>{display(profile?.fullName)}</dd>
                    </div>
                    <div>
                      <dt>Ngày sinh</dt>
                      <dd>{formatDate(profile?.dateOfBirth)}</dd>
                    </div>
                    <div>
                      <dt>Giới tính</dt>
                      <dd>{display(profile?.gender)}</dd>
                    </div>
                    <div>
                      <dt>Địa chỉ</dt>
                      <dd>{display(profile?.addressLine)}</dd>
                    </div>
                    <div>
                      <dt>CCCD</dt>
                      <dd>{display(profile?.verifiedCitizenIdMasked)}</dd>
                    </div>
                    <div>
                      <dt>Duyệt lúc</dt>
                      <dd>{profile?.kycReviewedAt ? new Date(profile.kycReviewedAt).toLocaleString() : 'Chưa duyệt'}</dd>
                    </div>
                  </dl>

                  <div className="auth-actions" style={{ marginTop: '24px' }}>
                    {displayedKycStatus !== 'Approved' && (
                      <Button type="button" onClick={() => navigate(ROUTE_PATHS.ME.KYC)}>
                        {profile?.identityVerified ? 'Cập nhật lại eKYC' : 'Xác thực eKYC'}
                      </Button>
                    )}
                    <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.KYC_STATUS)}>
                      Xem trạng thái KYC
                    </Button>
                  </div>
                </div>
              ) : null}
            </section>
          ) : (
            <section className="profile-section">
              <h1>Quản lý bảo mật</h1>
              <p className="subtle">Chọn cách đổi mật khẩu phù hợp với tình trạng tài khoản của bạn.</p>

              <div className="mode-tabs" style={{ marginBottom: '24px', display: 'flex', gap: '12px' }}>
                <button
                  type="button"
                  className={securityMode === 'current-password' ? 'mode-tab active' : 'mode-tab'}
                  style={{
                    padding: '10px 16px',
                    border: 'none',
                    borderRadius: '6px',
                    cursor: 'pointer',
                    fontWeight: 600,
                    fontSize: '14px',
                    background: securityMode === 'current-password' ? '#eff6ff' : 'transparent',
                    color: securityMode === 'current-password' ? '#246bfe' : '#475569',
                    transition: 'all 0.15s'
                  }}
                  onClick={() => { setSecurityMode('current-password'); setOtpVerified(false); setSecurityError(null); setSecurityMessage(null); }}
                >
                  Còn nhớ mật khẩu
                </button>
                <button
                  type="button"
                  className={securityMode === 'email-otp' ? 'mode-tab active' : 'mode-tab'}
                  style={{
                    padding: '10px 16px',
                    border: 'none',
                    borderRadius: '6px',
                    cursor: 'pointer',
                    fontWeight: 600,
                    fontSize: '14px',
                    background: securityMode === 'email-otp' ? '#eff6ff' : 'transparent',
                    color: securityMode === 'email-otp' ? '#246bfe' : '#475569',
                    transition: 'all 0.15s'
                  }}
                  onClick={() => { setSecurityMode('email-otp'); setOtpVerified(false); setOtpForm(c => ({ ...c, otp: '', newPassword: '', confirmPassword: '' })); setSecurityError(null); setSecurityMessage(null); }}
                >
                  Không nhớ mật khẩu
                </button>
                <button
                  type="button"
                  className={securityMode === 'devices' ? 'mode-tab active' : 'mode-tab'}
                  style={{
                    padding: '10px 16px',
                    border: 'none',
                    borderRadius: '6px',
                    cursor: 'pointer',
                    fontWeight: 600,
                    fontSize: '14px',
                    background: securityMode === 'devices' ? '#eff6ff' : 'transparent',
                    color: securityMode === 'devices' ? '#246bfe' : '#475569',
                    transition: 'all 0.15s'
                  }}
                  onClick={() => { setSecurityMode('devices'); setOtpVerified(false); setSecurityError(null); setSecurityMessage(null); }}
                >
                  Thiết bị đã đăng nhập
                </button>
              </div>

              {securityError ? <Alert type="error">{securityError}</Alert> : null}
              {securityMessage ? <Alert type="success">{securityMessage}</Alert> : null}

              {securityMode === 'current-password' && (
                <form className="auth-form" onSubmit={handleChangeWithCurrentPassword}>
                  <FormField label="Mật khẩu hiện tại" htmlFor="change-current-password">
                    <input
                      id="change-current-password"
                      className="ui-input"
                      type="password"
                      value={currentPasswordForm.currentPassword}
                      disabled={isSubmitting}
                      onChange={event =>
                        setCurrentPasswordForm(current => ({
                          ...current,
                          currentPassword: event.target.value
                        }))
                      }
                    />
                  </FormField>
                  <div className="profile-grid">
                    <FormField label="Mật khẩu mới" htmlFor="change-new-password">
                      <input
                        id="change-new-password"
                        className="ui-input"
                        type="password"
                        value={currentPasswordForm.newPassword}
                        disabled={isSubmitting}
                        onChange={event =>
                          setCurrentPasswordForm(current => ({
                            ...current,
                            newPassword: event.target.value
                          }))
                        }
                      />
                    </FormField>
                    <FormField label="Xác nhận mật khẩu mới" htmlFor="change-confirm-password">
                      <input
                        id="change-confirm-password"
                        className="ui-input"
                        type="password"
                        value={currentPasswordForm.confirmPassword}
                        disabled={isSubmitting}
                        onChange={event =>
                          setCurrentPasswordForm(current => ({
                            ...current,
                            confirmPassword: event.target.value
                          }))
                        }
                      />
                    </FormField>
                  </div>
                  <div className="auth-actions" style={{ marginTop: '24px' }}>
                    <Button type="submit" disabled={isSubmitting}>
                      {isSubmitting ? 'Đang đổi...' : 'Đổi mật khẩu'}
                    </Button>
                  </div>
                </form>
              )}

              {securityMode === 'email-otp' && (
                <div className="auth-form">
                  {/* Bước 1: Nhập email + gửi OTP */}
                  <FormField label="Email nhận OTP" htmlFor="change-email">
                    <input
                      id="change-email"
                      className="ui-input"
                      type="email"
                      value={otpForm.email}
                      disabled={otpVerified || isSubmitting || isSendingOtp}
                      onChange={event =>
                        setOtpForm(current => ({
                          ...current,
                          email: event.target.value
                        }))
                      }
                    />
                  </FormField>
                  {!otpVerified && (
                    <div className="auth-actions" style={{ marginBottom: '16px' }}>
                      <Button
                        type="button"
                        variant="secondary"
                        disabled={isSendingOtp}
                        onClick={() => void handleSendOtp()}
                      >
                        {isSendingOtp ? 'Đang gửi...' : 'Gửi OTP về Gmail'}
                      </Button>
                    </div>
                  )}

                  {/* Bước 2: Nhập OTP + kiểm tra */}
                  {!otpVerified && (
                    <>
                      <FormField label="Mã OTP (6 số)" htmlFor="change-otp">
                        <OtpInput
                          value={otpForm.otp}
                          disabled={isVerifyingOtp}
                          onChange={val =>
                            setOtpForm(current => ({
                              ...current,
                              otp: val
                            }))
                          }
                        />
                      </FormField>
                      <div className="auth-actions" style={{ marginTop: '16px' }}>
                        <Button
                          type="button"
                          disabled={isVerifyingOtp || otpForm.otp.length < 6}
                          onClick={() => void handleVerifyOtp()}
                        >
                          {isVerifyingOtp ? 'Đang kiểm tra...' : 'Kiểm tra OTP'}
                        </Button>
                      </div>
                    </>
                  )}

                  {/* Bước 3: Nhập mật khẩu mới (chỉ hiện khi OTP đã xác thực) */}
                  {otpVerified && (
                    <form className="auth-form" onSubmit={handleResetWithOtp} style={{ marginTop: '16px' }}>
                      <Alert type="success">OTP đã xác thực thành công. Vui lòng nhập mật khẩu mới.</Alert>
                      <div className="profile-grid">
                        <FormField label="Mật khẩu mới" htmlFor="change-otp-new-password">
                          <input
                            id="change-otp-new-password"
                            className="ui-input"
                            type="password"
                            value={otpForm.newPassword}
                            disabled={isSubmitting}
                            onChange={event =>
                              setOtpForm(current => ({
                                ...current,
                                newPassword: event.target.value
                              }))
                            }
                          />
                        </FormField>
                        <FormField label="Xác nhận mật khẩu mới" htmlFor="change-otp-confirm-password">
                          <input
                            id="change-otp-confirm-password"
                            className="ui-input"
                            type="password"
                            value={otpForm.confirmPassword}
                            disabled={isSubmitting}
                            onChange={event =>
                              setOtpForm(current => ({
                                ...current,
                                confirmPassword: event.target.value
                              }))
                            }
                          />
                        </FormField>
                      </div>
                      <div className="auth-actions" style={{ marginTop: '24px' }}>
                        <Button type="submit" disabled={isSubmitting}>
                          {isSubmitting ? 'Đang đặt lại...' : 'Đặt lại mật khẩu'}
                        </Button>
                      </div>
                    </form>
                  )}
                </div>
              )}

              {securityMode === 'devices' && (
                <div className="security-devices-section">
                  <h3 style={{ fontSize: '18px', fontWeight: 700, color: '#0f172a', marginBottom: '8px' }}>
                    Các thiết bị đã đăng nhập
                  </h3>
                  <p className="subtle" style={{ marginBottom: '20px' }}>
                    Đây là danh sách các thiết bị/trình duyệt đang duy trì phiên đăng nhập của bạn. Bạn có thể đăng xuất từ xa khỏi các thiết bị khác.
                  </p>

                  {isLoadingSessions ? (
                    <LoadingState message="Đang tải danh sách thiết bị..." />
                  ) : sessionsError ? (
                    <Alert type="error">{sessionsError}</Alert>
                  ) : sessions.length === 0 ? (
                    <p className="subtle">Không tìm thấy thiết bị hoạt động nào.</p>
                  ) : (
                    <div className="device-list">
                      {sessions.map(session => (
                        <div key={session.id} className="device-item">
                          <div className="device-info">
                            <div className="device-icon">
                              {session.userAgent?.toLowerCase().includes('windows') ||
                              session.userAgent?.toLowerCase().includes('macintosh') ||
                              session.userAgent?.toLowerCase().includes('linux') ? (
                                <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2">
                                  <path strokeLinecap="round" strokeLinejoin="round" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                                </svg>
                              ) : (
                                <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2">
                                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 18h.01M8 21h8a2 2 0 002-2V5a2 2 0 00-2-2H8a2 2 0 00-2 2v14a2 2 0 002 2z" />
                                </svg>
                              )}
                            </div>
                            <div className="device-details">
                              <span className="device-name">{parseUserAgent(session.userAgent)}</span>
                              <span className="device-meta">
                                IP: {session.ipAddress || 'Không rõ'} &bull; Đăng nhập: {new Date(session.createdAt).toLocaleString('vi-VN')}
                              </span>
                            </div>
                          </div>
                          <div className="device-action">
                            {session.isCurrentSession && (
                              <span className="device-current-badge">Thiết bị hiện tại</span>
                            )}
                            <button
                              type="button"
                              className="device-revoke-btn"
                              onClick={() => void handleRevokeSession(session.id, session.isCurrentSession)}
                            >
                              Đăng xuất
                            </button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </section>
          )}
        </main>
      </div>

      {showCropper && rawImageSrc && (
        <AvatarCropper
          imageSrc={rawImageSrc}
          initialZoom={cropParams.zoom}
          initialPosition={cropParams.position}
          onConfirm={(zoom, position) => {
            setCropParams({ zoom, position });
            setIsCropChanged(true);
            if (rawImageSrc.startsWith('blob:')) {
              setPreviewUrl(rawImageSrc);
            }
            setShowCropper(false);
            setRawImageSrc(null);
          }}
          onCancel={() => {
            setShowCropper(false);
            if (!previewUrl) {
              setSelectedFile(null);
            }
            if (rawImageSrc && rawImageSrc.startsWith('blob:') && rawImageSrc !== previewUrl) {
              URL.revokeObjectURL(rawImageSrc);
            }
            setRawImageSrc(null);
          }}
        />
      )}
    </div>
  );
}
