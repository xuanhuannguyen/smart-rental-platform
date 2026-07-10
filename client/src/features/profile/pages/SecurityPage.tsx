import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Toast } from '../../../shared/components/ui/Toast';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';
import { OtpInput } from '../../../shared/components/ui/OtpInput';
import { Tabs } from '../../../shared/components/ui/Tabs';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import { authApi } from '../../auth/services/authApi';
import { profileApi } from '../services/profileApi';
import type { UserSession } from '../types/profile.types';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { ConfirmModal } from '../../../shared/components/ui/ConfirmModal';
import './MyProfilePage.css'; // Reuse CSS

type SecurityTab = 'change-password' | 'devices' | 'activity-logs';
type PasswordMethod = 'current-password' | 'email-otp';

function getSecurityTabIcon(tab: SecurityTab) {
  const props = {
    className: 'security-tab-icon',
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 2.2,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
  };

  switch (tab) {
    case 'devices':
      return (
        <svg {...props}>
          <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
          <line x1="8" y1="21" x2="16" y2="21" />
          <line x1="12" y1="17" x2="12" y2="21" />
        </svg>
      );
    case 'activity-logs':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <polyline points="12 6 12 12 16 14" />
          <path d="M12 2a10 10 0 1 0 10 10" />
        </svg>
      );
    default:
      return (
        <svg {...props}>
          <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
          <path d="M7 11V7a5 5 0 0 1 10 0v4" />
        </svg>
      );
  }
}

function getPasswordStrength(password: string): number {
  if (!password) return 0;
  let strength = 0;
  if (password.length >= 6) strength++;
  if (password.length >= 8) strength++;
  if (/[A-Z]/.test(password) || /[^A-Za-z0-9]/.test(password)) strength++;
  if (/[0-9]/.test(password) && /[a-z]/.test(password)) strength++;
  return strength;
}

export function SecurityPage() {
  const { currentUser, clearSession } = useAuth();
  const navigate = useNavigate();

  const [activeTab, setActiveTab] = useState<SecurityTab>('change-password');
  const [passwordMethod, setPasswordMethod] = useState<PasswordMethod>('current-password');

  // Password visibility states
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [showOtpNewPassword, setShowOtpNewPassword] = useState(false);
  const [showOtpConfirmPassword, setShowOtpConfirmPassword] = useState(false);

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
  const [validationError, setValidationError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);

  const [revokeModalState, setRevokeModalState] = useState<{
    isOpen: boolean;
    sessionId: string | null;
    isCurrentSession: boolean;
  }>({ isOpen: false, sessionId: null, isCurrentSession: false });

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
    if (activeTab === 'devices') {
      void loadSessions();
    }
  }, [activeTab, loadSessions]);

  if (!currentUser) {
    return null;
  }

  async function handleChangeWithCurrentPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setValidationError(null);
    setToast(null);


    if (!currentPasswordForm.currentPassword || !currentPasswordForm.newPassword) {
      setValidationError('Vui lòng nhập mật khẩu hiện tại và mật khẩu mới.');
      return;
    }

    if (currentPasswordForm.newPassword.length < 6) {
      setValidationError('Mật khẩu mới phải có ít nhất 6 ký tự.');
      return;
    }

    if (currentPasswordForm.newPassword !== currentPasswordForm.confirmPassword) {
      setValidationError('Xác nhận mật khẩu mới không khớp.');
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
      setToast({ message: getApiErrorMessage(changeError, 'Không thể đổi mật khẩu.'), type: 'error' });
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleSendOtp() {
    setValidationError(null);
    setToast(null);


    if (!otpForm.email.trim()) {
      setValidationError('Vui lòng nhập email.');
      return;
    }

    setIsSendingOtp(true);

    try {
      await authApi.forgotPassword({ email: otpForm.email.trim() });
      setToast({ message: 'OTP đặt lại mật khẩu đã được gửi. Vui lòng kiểm tra Gmail.', type: 'success' });
    } catch (sendError) {
      setToast({ message: getApiErrorMessage(sendError, 'Không thể gửi OTP.'), type: 'error' });
    } finally {
      setIsSendingOtp(false);
    }
  }

  async function handleVerifyOtp() {
    setValidationError(null);
    setToast(null);


    if (!otpForm.email.trim() || !otpForm.otp.trim()) {
      setValidationError('Vui lòng nhập email và mã OTP.');
      return;
    }

    setIsVerifyingOtp(true);

    try {
      await authApi.verifyResetOtp({
        email: otpForm.email.trim(),
        otp: otpForm.otp.trim()
      });
      setOtpVerified(true);
      setToast({ message: 'OTP hợp lệ! Vui lòng nhập mật khẩu mới bên dưới.', type: 'success' });
    } catch (verifyError) {
      setToast({ message: getApiErrorMessage(verifyError, 'OTP không hợp lệ hoặc đã hết hạn.'), type: 'error' });
    } finally {
      setIsVerifyingOtp(false);
    }
  }

  async function handleResetWithOtp(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setValidationError(null);
    setToast(null);


    if (!otpForm.newPassword) {
      setValidationError('Vui lòng nhập mật khẩu mới.');
      return;
    }

    if (otpForm.newPassword.length < 6) {
      setValidationError('Mật khẩu mới phải có ít nhất 6 ký tự.');
      return;
    }

    if (otpForm.newPassword !== otpForm.confirmPassword) {
      setValidationError('Xác nhận mật khẩu mới không khớp.');
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
      setToast({ message: getApiErrorMessage(resetError, 'Không thể đặt lại mật khẩu.'), type: 'error' });
    } finally {
      setIsSubmitting(false);
    }
  }

  const handleRevokeSessionClick = (sessionId: string, isCurrentSession: boolean) => {
    setRevokeModalState({ isOpen: true, sessionId, isCurrentSession });
  };

  const handleConfirmRevoke = async () => {
    if (!revokeModalState.sessionId) return;

    setValidationError(null);
    setToast(null);

    try {
      await profileApi.revokeSession(revokeModalState.sessionId);
      if (revokeModalState.isCurrentSession) {
        clearSession();
        navigate(ROUTE_PATHS.AUTH.LOGIN, { replace: true });
        return;
      }
      setToast({ message: 'Đã đăng xuất thiết bị thành công.', type: 'success' });
      void loadSessions();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể đăng xuất thiết bị.'), type: 'error' });
    } finally {
      setRevokeModalState({ isOpen: false, sessionId: null, isCurrentSession: false });
    }
  };

  function parseUserAgent(uaString?: string | null): string {
    if (!uaString) return 'Thiết bị không xác định';
    const ua = uaString.toLowerCase();
    let os = 'Hệ điều hành không xác định';
    let browser = 'Trình duyệt không xác định';

    if (ua.includes('windows')) os = 'Windows';
    else if (ua.includes('macintosh') || ua.includes('mac os')) os = 'macOS';
    else if (ua.includes('iphone') || ua.includes('ipad') || ua.includes('ipod')) os = 'iOS';
    else if (ua.includes('android')) os = 'Android';
    else if (ua.includes('linux')) os = 'Linux';

    if (ua.includes('edg/')) browser = 'Edge';
    else if (ua.includes('chrome') && !ua.includes('chromium')) browser = 'Chrome';
    else if (ua.includes('safari') && !ua.includes('chrome')) browser = 'Safari';
    else if (ua.includes('firefox')) browser = 'Firefox';
    else if (ua.includes('opr/') || ua.includes('opera')) browser = 'Opera';

    return `${browser} trên ${os}`;
  }

  function handleSecurityTabChange(tab: SecurityTab) {
    setActiveTab(tab);
    setValidationError(null);
    setToast(null);
  }

  return (
    <div>
      <PageHeader
        icon={
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="#2563eb" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
              <path d="m9 11 2 2 4-4" />
            </svg>
          </div>
        }
        eyebrow="TÀI KHOẢN"
        title="Quản lý bảo mật"
        description="Quản lý mật khẩu và các thiết bị đã đăng nhập của bạn"
      />

      <Tabs
        className="attached-bottom"
        variant="segmented-secondary"
        activeId={activeTab}
        onChange={(tab) => handleSecurityTabChange(tab as SecurityTab)}
        items={[
          { id: 'change-password', label: 'Đổi mật khẩu', icon: getSecurityTabIcon('change-password') },
          { id: 'devices', label: 'Thiết bị đăng nhập', icon: getSecurityTabIcon('devices') },
          { id: 'activity-logs', label: 'Lịch sử hoạt động', icon: getSecurityTabIcon('activity-logs') },
        ]}
      />

      {validationError && (
        <div style={{ maxWidth: '760px', margin: '0 auto 16px auto' }}>
          <Alert type="error">{validationError}</Alert>

        </div>
      )}

      {activeTab === 'change-password' && (
        <div className="security-card tab-attached-panel tab-attached-panel--compact">
          <div className="security-card-icon-box">
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
              <path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
          </div>
          <h3>Đổi mật khẩu</h3>
          <p className="card-desc">Vui lòng tạo mật khẩu mới mạnh và không sử dụng lại mật khẩu cũ.</p>

          <div className="security-method-selector">
            <button
              type="button"
              className={`method-pill ${passwordMethod === 'current-password' ? 'active' : ''}`}
              onClick={() => {
                setPasswordMethod('current-password');
                setOtpVerified(false);
                setValidationError(null);
                setToast(null);

              }}
            >
              Dùng mật khẩu hiện tại
            </button>
            <button
              type="button"
              className={`method-pill ${passwordMethod === 'email-otp' ? 'active' : ''}`}
              onClick={() => {
                setPasswordMethod('email-otp');
                setOtpVerified(false);
                setOtpForm(c => ({ ...c, otp: '', newPassword: '', confirmPassword: '' }));
                setValidationError(null);
                setToast(null);

              }}
            >
              Nhận mã OTP qua Email
            </button>
          </div>

          {passwordMethod === 'current-password' && (
            <form onSubmit={handleChangeWithCurrentPassword} className="security-form-fields">
              <FormField label="Mật khẩu hiện tại *" htmlFor="security-current-password">
                <div className="input-with-actions">
                  <svg className="input-icon-left" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                    <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                  </svg>
                  <input
                    id="security-current-password"
                    className="ui-input"
                    type={showCurrentPassword ? 'text' : 'password'}
                    placeholder="Nhập mật khẩu hiện tại"
                    value={currentPasswordForm.currentPassword}
                    disabled={isSubmitting}
                    onChange={e => setCurrentPasswordForm(c => ({ ...c, currentPassword: e.target.value }))}
                  />
                  <button
                    type="button"
                    className="input-action-right"
                    onClick={() => setShowCurrentPassword(!showCurrentPassword)}
                    title={showCurrentPassword ? 'Ẩn mật khẩu' : 'Hiển thị mật khẩu'}
                  >
                    {showCurrentPassword ? (
                      <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
                        <line x1="1" y1="1" x2="23" y2="23" />
                      </svg>
                    ) : (
                      <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                        <circle cx="12" cy="12" r="3" />
                      </svg>
                    )}
                  </button>
                </div>
              </FormField>

              <FormField label="Mật khẩu mới *" htmlFor="security-new-password">
                <div className="input-with-actions">
                  <svg className="input-icon-left" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                    <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                  </svg>
                  <input
                    id="security-new-password"
                    className="ui-input"
                    type={showNewPassword ? 'text' : 'password'}
                    placeholder="Nhập mật khẩu mới"
                    value={currentPasswordForm.newPassword}
                    disabled={isSubmitting}
                    onChange={e => setCurrentPasswordForm(c => ({ ...c, newPassword: e.target.value }))}
                  />
                  <button
                    type="button"
                    className="input-action-right"
                    onClick={() => setShowNewPassword(!showNewPassword)}
                    title={showNewPassword ? 'Ẩn mật khẩu' : 'Hiển thị mật khẩu'}
                  >
                    {showNewPassword ? (
                      <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
                        <line x1="1" y1="1" x2="23" y2="23" />
                      </svg>
                    ) : (
                      <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                        <circle cx="12" cy="12" r="3" />
                      </svg>
                    )}
                  </button>
                </div>

                <div className="password-strength-section">
                  <div className="strength-indicator-row">
                    <div className="strength-bars">
                      {(() => {
                        const score = getPasswordStrength(currentPasswordForm.newPassword);
                        return (
                          <>
                            <div className={`strength-bar ${score >= 1 ? `active level-${score}` : ''}`} />
                            <div className={`strength-bar ${score >= 2 ? `active level-${score}` : ''}`} />
                            <div className={`strength-bar ${score >= 3 ? `active level-${score}` : ''}`} />
                            <div className={`strength-bar ${score >= 4 ? `active level-${score}` : ''}`} />
                          </>
                        );
                      })()}
                    </div>
                    <span className="strength-hint-text">Mật khẩu phải có ít nhất 8 ký tự</span>
                  </div>
                </div>
              </FormField>

              <FormField label="Xác nhận mật khẩu mới *" htmlFor="security-confirm-password">
                <div className="input-with-actions">
                  <svg className="input-icon-left" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                    <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                  </svg>
                  <input
                    id="security-confirm-password"
                    className="ui-input"
                    type={showConfirmPassword ? 'text' : 'password'}
                    placeholder="Nhập lại mật khẩu mới"
                    value={currentPasswordForm.confirmPassword}
                    disabled={isSubmitting}
                    onChange={e => setCurrentPasswordForm(c => ({ ...c, confirmPassword: e.target.value }))}
                  />
                  <button
                    type="button"
                    className="input-action-right"
                    onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                    title={showConfirmPassword ? 'Ẩn mật khẩu' : 'Hiển thị mật khẩu'}
                  >
                    {showConfirmPassword ? (
                      <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
                        <line x1="1" y1="1" x2="23" y2="23" />
                      </svg>
                    ) : (
                      <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                        <circle cx="12" cy="12" r="3" />
                      </svg>
                    )}
                  </button>
                </div>
              </FormField>

              <div className="auth-actions" style={{ marginTop: '8px', display: 'flex', justifyContent: 'center', width: '100%' }}>
                <Button type="submit" disabled={isSubmitting} style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '12px 32px' }}>
                  <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                    <path d="m9 11 2 2 4-4" />
                  </svg>
                  {isSubmitting ? 'Đang lưu...' : 'Lưu mật khẩu mới'}
                </Button>
              </div>
            </form>
          )}

          {passwordMethod === 'email-otp' && (
            <div className="security-form-fields">
              <FormField label="Email" htmlFor="reset-email">
                <div className="input-with-actions">
                  <svg className="input-icon-left" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" />
                    <polyline points="22,6 12,13 2,6" />
                  </svg>
                  <input
                    id="reset-email"
                    className="ui-input"
                    value={otpForm.email}
                    disabled
                  />
                </div>
              </FormField>

              {!otpVerified && (
                <div style={{ display: 'flex', gap: '12px', alignItems: 'flex-end', width: '100%' }}>
                  <div style={{ flex: 1 }}>
                    <FormField label="Mã OTP từ Email" htmlFor="reset-otp">
                      <OtpInput
                        value={otpForm.otp}
                        onChange={val => setOtpForm(c => ({ ...c, otp: val }))}
                        disabled={isVerifyingOtp}
                      />
                    </FormField>
                  </div>
                  <div style={{ marginBottom: '0px' }}>
                    <Button type="button" variant="secondary" onClick={() => void handleSendOtp()} disabled={isSendingOtp} style={{ height: '46px' }}>
                      {isSendingOtp ? 'Đang gửi...' : 'Gửi mã OTP'}
                    </Button>
                  </div>
                </div>
              )}

              {!otpVerified && (
                <div className="auth-actions" style={{ display: 'flex', justifyContent: 'center', width: '100%' }}>
                  <Button type="button" onClick={() => void handleVerifyOtp()} disabled={isVerifyingOtp} style={{ padding: '12px 32px' }}>
                    {isVerifyingOtp ? 'Đang xác minh...' : 'Xác minh OTP'}
                  </Button>
                </div>
              )}

              {otpVerified && (
                <form onSubmit={handleResetWithOtp} style={{ display: 'flex', flexDirection: 'column', gap: '20px', width: '100%' }}>
                  <FormField label="Mật khẩu mới *" htmlFor="reset-new-password">
                    <div className="input-with-actions">
                      <svg className="input-icon-left" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                        <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                        <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                      </svg>
                      <input
                        id="reset-new-password"
                        className="ui-input"
                        type={showOtpNewPassword ? 'text' : 'password'}
                        placeholder="Nhập mật khẩu mới"
                        value={otpForm.newPassword}
                        disabled={isSubmitting}
                        onChange={e => setOtpForm(c => ({ ...c, newPassword: e.target.value }))}
                      />
                      <button
                        type="button"
                        className="input-action-right"
                        onClick={() => setShowOtpNewPassword(!showOtpNewPassword)}
                        title={showOtpNewPassword ? 'Ẩn mật khẩu' : 'Hiển thị mật khẩu'}
                      >
                        {showOtpNewPassword ? (
                          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
                            <line x1="1" y1="1" x2="23" y2="23" />
                          </svg>
                        ) : (
                          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                            <circle cx="12" cy="12" r="3" />
                          </svg>
                        )}
                      </button>
                    </div>

                    <div className="password-strength-section">
                      <div className="strength-indicator-row">
                        <div className="strength-bars">
                          {(() => {
                            const score = getPasswordStrength(otpForm.newPassword);
                            return (
                              <>
                                <div className={`strength-bar ${score >= 1 ? `active level-${score}` : ''}`} />
                                <div className={`strength-bar ${score >= 2 ? `active level-${score}` : ''}`} />
                                <div className={`strength-bar ${score >= 3 ? `active level-${score}` : ''}`} />
                                <div className={`strength-bar ${score >= 4 ? `active level-${score}` : ''}`} />
                              </>
                            );
                          })()}
                        </div>
                        <span className="strength-hint-text">Mật khẩu phải có ít nhất 8 ký tự</span>
                      </div>
                    </div>
                  </FormField>

                  <FormField label="Xác nhận mật khẩu mới *" htmlFor="reset-confirm-password">
                    <div className="input-with-actions">
                      <svg className="input-icon-left" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                        <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                        <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                      </svg>
                      <input
                        id="reset-confirm-password"
                        className="ui-input"
                        type={showOtpConfirmPassword ? 'text' : 'password'}
                        placeholder="Nhập lại mật khẩu mới"
                        value={otpForm.confirmPassword}
                        disabled={isSubmitting}
                        onChange={e => setOtpForm(c => ({ ...c, confirmPassword: e.target.value }))}
                      />
                      <button
                        type="button"
                        className="input-action-right"
                        onClick={() => setShowOtpConfirmPassword(!showOtpConfirmPassword)}
                        title={showOtpConfirmPassword ? 'Ẩn mật khẩu' : 'Hiển thị mật khẩu'}
                      >
                        {showOtpConfirmPassword ? (
                          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
                            <line x1="1" y1="1" x2="23" y2="23" />
                          </svg>
                        ) : (
                          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                            <circle cx="12" cy="12" r="3" />
                          </svg>
                        )}
                      </button>
                    </div>
                  </FormField>

                  <div className="auth-actions" style={{ display: 'flex', justifyContent: 'center', width: '100%' }}>
                    <Button type="submit" disabled={isSubmitting} style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '12px 32px' }}>
                      <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                        <path d="m9 11 2 2 4-4" />
                      </svg>
                      {isSubmitting ? 'Đang lưu...' : 'Đặt lại mật khẩu'}
                    </Button>
                  </div>
                </form>
              )}
            </div>
          )}
        </div>
      )}

      {activeTab === 'devices' && (
        <div className="security-devices-card tab-attached-panel tab-attached-panel--compact">
          <h3>Thiết bị đang đăng nhập</h3>
          <p className="card-desc">Quản lý và đăng xuất khỏi các phiên đăng nhập hoạt động trên các thiết bị khác nhau.</p>

          {isLoadingSessions ? (
            <LoadingState message="Đang tải thiết bị..." />
          ) : sessionsError ? (
            <Alert type="error">{sessionsError}</Alert>
          ) : sessions.length === 0 ? (
            <p className="subtle" style={{ textAlign: 'left' }}>Không có thiết bị nào đang đăng nhập.</p>
          ) : (
            <div className="device-list" style={{ marginTop: '0' }}>
              {sessions.map(session => (
                <div key={session.id} className="device-item">
                  <div className="device-info">
                    <div className="device-icon">
                      {session.userAgent?.toLowerCase().includes('iphone') || session.userAgent?.toLowerCase().includes('android') ? (
                        <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <rect x="5" y="2" width="14" height="20" rx="2" ry="2" />
                          <line x1="12" y1="18" x2="12.01" y2="18" />
                        </svg>
                      ) : (
                        <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
                          <line x1="8" y1="21" x2="16" y2="21" />
                          <line x1="12" y1="17" x2="12" y2="21" />
                        </svg>
                      )}
                    </div>
                    <div className="device-details">
                      <div className="device-name">
                        {parseUserAgent(session.userAgent)}
                      </div>
                      <div className="device-meta">
                        IP: {session.ipAddress || 'Không xác định'} • Đăng nhập: {new Date(session.createdAt).toLocaleString()}
                      </div>
                      <div className="device-meta" style={{ fontSize: '11px', color: '#94a3b8' }}>
                        Hết hạn: {new Date(session.expiresAt).toLocaleString()}
                      </div>
                    </div>
                  </div>
                  <div className="device-action">
                    {session.isCurrentSession ? (
                      <span className="device-current-badge">Thiết bị hiện tại</span>
                    ) : (
                      <button
                        type="button"
                        className="device-revoke-btn"
                        onClick={() => handleRevokeSessionClick(session.id, session.isCurrentSession)}
                      >
                        Đăng xuất
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {activeTab === 'activity-logs' && (
        <div className="security-devices-card tab-attached-panel tab-attached-panel--compact">
          <h3>Lịch sử hoạt động</h3>
          <p className="card-desc">Xem nhật ký các hoạt động bảo mật gần đây trên tài khoản của bạn để đảm bảo an toàn.</p>

          <div className="activity-logs-list">
            <div className="activity-log-item">
              <div className="activity-log-icon-box login">
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
                  <polyline points="10 17 15 12 10 7" />
                  <line x1="15" y1="12" x2="3" y2="12" />
                </svg>
              </div>
              <div className="activity-log-details">
                <div className="activity-log-title">Đăng nhập hệ thống thành công</div>
                <div className="activity-log-meta">Chrome trên Windows • IP: 113.161.x.x • Hà Nội, Việt Nam</div>
              </div>
              <div className="activity-log-time">10 phút trước</div>
            </div>

            <div className="activity-log-item">
              <div className="activity-log-icon-box security">
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                  <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                </svg>
              </div>
              <div className="activity-log-details">
                <div className="activity-log-title">Đổi mật khẩu tài khoản</div>
                <div className="activity-log-meta">Firefox trên macOS • IP: 14.161.x.x • Đà Nẵng, Việt Nam</div>
              </div>
              <div className="activity-log-time">Hôm qua, 18:24</div>
            </div>

            <div className="activity-log-item">
              <div className="activity-log-icon-box logout">
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
                  <polyline points="16 17 21 12 16 7" />
                  <line x1="21" y1="12" x2="9" y2="12" />
                </svg>
              </div>
              <div className="activity-log-details">
                <div className="activity-log-title">Đăng xuất khỏi thiết bị (Revoke session)</div>
                <div className="activity-log-meta">Safari trên iOS • IP: 171.244.x.x • TP. Hồ Chí Minh, Việt Nam</div>
              </div>
              <div className="activity-log-time">3 ngày trước</div>
            </div>
          </div>
        </div>
      )}

      {activeTab === 'change-password' && (
        <div className="security-info-banner">
          <div className="security-info-banner-icon">
            <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10" />
              <line x1="12" y1="16" x2="12" y2="12" />
              <line x1="12" y1="8" x2="12.01" y2="8" />
            </svg>
          </div>
          <div className="security-info-banner-content">
            <span className="security-info-banner-title">Mẹo bảo mật</span>
            <span className="security-info-banner-desc">
              Sử dụng mật khẩu mạnh với chữ hoa, chữ thường, số và ký tự đặc biệt để bảo vệ tài khoản của bạn.
            </span>
          </div>
        </div>
      )}

      <ConfirmModal
        isOpen={revokeModalState.isOpen}
        title="Đăng xuất thiết bị"
        message={
          revokeModalState.isCurrentSession
            ? 'Bạn đang đăng xuất khỏi thiết bị hiện tại. Bạn sẽ phải đăng nhập lại. Tiếp tục?'
            : 'Bạn có chắc chắn muốn đăng xuất khỏi thiết bị này?'
        }
        confirmText="Đăng xuất"
        isDanger={true}
        onConfirm={handleConfirmRevoke}
        onCancel={() => setRevokeModalState({ isOpen: false, sessionId: null, isCurrentSession: false })}
      />
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
