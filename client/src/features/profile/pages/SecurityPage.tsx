import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';
import { OtpInput } from '../../../shared/components/ui/OtpInput';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import { authApi } from '../../auth/services/authApi';
import { profileApi } from '../services/profileApi';
import type { UserSession } from '../types/profile.types';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import './MyProfilePage.css'; // Reuse CSS

type SecurityMode = 'current-password' | 'email-otp' | 'devices';

export function SecurityPage() {
  const { currentUser, clearSession } = useAuth();
  const navigate = useNavigate();

  const [securityMode, setSecurityMode] = useState<SecurityMode>('current-password');
  
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
    if (securityMode === 'devices') {
      void loadSessions();
    }
  }, [securityMode, loadSessions]);

  if (!currentUser) {
    return null;
  }

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

  return (
    <section className="profile-section" style={{ padding: 0, boxShadow: 'none', background: 'transparent', minHeight: 'auto' }}>
      <h1 style={{ fontSize: '1.5rem', fontWeight: 700, margin: 0, marginBottom: '8px' }}>Quản lý bảo mật</h1>
      <p className="subtle" style={{ color: '#64748b', marginBottom: '24px' }}>Chọn cách đổi mật khẩu phù hợp với tình trạng tài khoản của bạn.</p>

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
              onChange={e => setCurrentPasswordForm(c => ({ ...c, currentPassword: e.target.value }))}
            />
          </FormField>
          <FormField label="Mật khẩu mới" htmlFor="change-new-password">
            <input
              id="change-new-password"
              className="ui-input"
              type="password"
              value={currentPasswordForm.newPassword}
              disabled={isSubmitting}
              onChange={e => setCurrentPasswordForm(c => ({ ...c, newPassword: e.target.value }))}
            />
          </FormField>
          <FormField label="Xác nhận mật khẩu mới" htmlFor="change-confirm-password">
            <input
              id="change-confirm-password"
              className="ui-input"
              type="password"
              value={currentPasswordForm.confirmPassword}
              disabled={isSubmitting}
              onChange={e => setCurrentPasswordForm(c => ({ ...c, confirmPassword: e.target.value }))}
            />
          </FormField>
          <div className="auth-actions" style={{ marginTop: '16px' }}>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Đang lưu...' : 'Lưu mật khẩu mới'}
            </Button>
          </div>
        </form>
      )}

      {securityMode === 'email-otp' && (
        <div className="auth-form">
          <FormField label="Email" htmlFor="reset-email">
            <input
              id="reset-email"
              className="ui-input"
              value={otpForm.email}
              disabled
            />
          </FormField>

          {!otpVerified && (
            <div style={{ display: 'flex', gap: '8px', alignItems: 'flex-end' }}>
              <div style={{ flex: 1 }}>
                <FormField label="Mã OTP từ Email" htmlFor="reset-otp">
                  <OtpInput
                    value={otpForm.otp}
                    onChange={val => setOtpForm(c => ({ ...c, otp: val }))}
                    disabled={isVerifyingOtp}
                  />
                </FormField>
              </div>
              <div style={{ marginBottom: '8px' }}>
                <Button type="button" variant="secondary" onClick={() => void handleSendOtp()} disabled={isSendingOtp}>
                  {isSendingOtp ? 'Đang gửi...' : 'Gửi mã OTP'}
                </Button>
              </div>
            </div>
          )}

          {!otpVerified && (
            <div className="auth-actions" style={{ marginTop: '16px' }}>
              <Button type="button" onClick={() => void handleVerifyOtp()} disabled={isVerifyingOtp}>
                {isVerifyingOtp ? 'Đang xác minh...' : 'Xác minh OTP'}
              </Button>
            </div>
          )}

          {otpVerified && (
            <form onSubmit={handleResetWithOtp} style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '16px' }}>
              <FormField label="Mật khẩu mới" htmlFor="reset-new-password">
                <input
                  id="reset-new-password"
                  className="ui-input"
                  type="password"
                  value={otpForm.newPassword}
                  disabled={isSubmitting}
                  onChange={e => setOtpForm(c => ({ ...c, newPassword: e.target.value }))}
                />
              </FormField>
              <FormField label="Xác nhận mật khẩu mới" htmlFor="reset-confirm-password">
                <input
                  id="reset-confirm-password"
                  className="ui-input"
                  type="password"
                  value={otpForm.confirmPassword}
                  disabled={isSubmitting}
                  onChange={e => setOtpForm(c => ({ ...c, confirmPassword: e.target.value }))}
                />
              </FormField>
              <div className="auth-actions">
                <Button type="submit" disabled={isSubmitting}>
                  {isSubmitting ? 'Đang lưu...' : 'Đặt lại mật khẩu'}
                </Button>
              </div>
            </form>
          )}
        </div>
      )}

      {securityMode === 'devices' && (
        <div className="sessions-list" style={{ marginTop: '16px' }}>
          {isLoadingSessions ? (
            <LoadingState message="Đang tải thiết bị..." />
          ) : sessionsError ? (
            <Alert type="error">{sessionsError}</Alert>
          ) : sessions.length === 0 ? (
            <p className="subtle">Không có thiết bị nào đang đăng nhập.</p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {sessions.map(session => (
                <div 
                  key={session.id} 
                  style={{ 
                    display: 'flex', 
                    justifyContent: 'space-between', 
                    alignItems: 'center',
                    padding: '16px',
                    borderRadius: '8px',
                    border: session.isCurrentSession ? '2px solid #3b82f6' : '1px solid #e2e8f0',
                    background: session.isCurrentSession ? '#eff6ff' : '#ffffff'
                  }}
                >
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                    <div style={{ fontWeight: 600, color: '#0f172a', display: 'flex', alignItems: 'center', gap: '8px' }}>
                      {parseUserAgent(session.userAgent)}
                      {session.isCurrentSession && (
                        <span style={{ fontSize: '0.75rem', padding: '2px 8px', background: '#3b82f6', color: 'white', borderRadius: '12px' }}>Hiện tại</span>
                      )}
                    </div>
                    <div style={{ fontSize: '0.85rem', color: '#64748b' }}>
                      IP: {session.ipAddress || 'Không xác định'}
                    </div>
                    <div style={{ fontSize: '0.85rem', color: '#64748b' }}>
                      Đăng nhập lúc: {new Date(session.createdAt).toLocaleString()}
                    </div>
                    <div style={{ fontSize: '0.85rem', color: '#64748b' }}>
                      Hết hạn: {new Date(session.expiresAt).toLocaleString()}
                    </div>
                  </div>
                  <button
                    type="button"
                    style={{
                      background: 'transparent',
                      border: '1px solid #ef4444',
                      color: '#ef4444',
                      padding: '8px 16px',
                      borderRadius: '6px',
                      cursor: 'pointer',
                      fontWeight: 500,
                      transition: 'all 0.15s'
                    }}
                    onMouseEnter={e => { e.currentTarget.style.background = '#fef2f2'; }}
                    onMouseLeave={e => { e.currentTarget.style.background = 'transparent'; }}
                    onClick={() => void handleRevokeSession(session.id, session.isCurrentSession)}
                  >
                    Đăng xuất
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </section>
  );
}
