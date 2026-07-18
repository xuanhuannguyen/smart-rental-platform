import { Alert } from '../../../shared/components/ui/Alert';
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';
import { ENDPOINTS } from '../../../shared/api/endpoints';
import { apiClient } from '../../../shared/api/apiClient';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Toast } from '../../../shared/components/ui/Toast';
import { Button } from '../../../shared/components/ui/Button';
import { contractApi } from '../../contracts/api';
import type { ESignOtpMethod } from '../../contracts/types';
import { SignatureInput } from '../../contracts/components/SignatureInput';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import '../../../shared/components/ui/ESignOtpDialog.css';
import './ContractPreviewModal.css';

pdfjs.GlobalWorkerOptions.workerSrc = `//unpkg.com/pdfjs-dist@${pdfjs.version}/build/pdf.worker.min.mjs`;

interface ContractPreviewModalProps {
  contractId: string;
  role: 'landlord' | 'tenant';
  onClose: () => void;
  onSuccess: () => void;
}

export function ContractPreviewModal({ contractId, role, onClose, onSuccess }: ContractPreviewModalProps) {
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [numPages, setNumPages] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  const [agreed, setAgreed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [dialogType, setDialogType] = useState<'revision' | 'reject' | null>(null);
  const [reason, setReason] = useState('');
  const [revisionType, setRevisionType] = useState<'Occupants' | 'ContractTerms'>('ContractTerms');

  const [showOtpDialog, setShowOtpDialog] = useState(false);
  const [otpCode, setOtpCode] = useState('');
  const [otpSubmitting, setOtpSubmitting] = useState(false);
  const [otpRequested, setOtpRequested] = useState(false);
  const [otpMethod, setOtpMethod] = useState<ESignOtpMethod>(3);
  const [maskedDestination, setMaskedDestination] = useState('');
  const [signatureImageBase64, setSignatureImageBase64] = useState('');

  const containerRef = useRef<HTMLDivElement>(null);
  const [containerWidth, setContainerWidth] = useState(800);

  useLayoutEffect(() => {
    const updateWidth = () => {
      if (containerRef.current) {
        setContainerWidth(containerRef.current.clientWidth - 48);
      }
    };

    updateWidth();
    window.addEventListener('resize', updateWidth);
    return () => window.removeEventListener('resize', updateWidth);
  }, []);

  useEffect(() => {
    let objectUrl: string | null = null;

    async function fetchPdf() {
      try {
        setLoading(true);
        setToast(null);

        const response = await apiClient<Blob>(ENDPOINTS.CONTRACTS.PREVIEW_PDF(contractId), {
          auth: true,
          responseType: 'blob'
        });

        objectUrl = URL.createObjectURL(response);
        setPdfUrl(objectUrl);
      } catch (err: any) {
        setToast({ message: err?.response?.data?.message || 'Không thể tải file PDF hợp đồng. Vui lòng thử lại sau.', type: 'error' });
      } finally {
        setLoading(false);
      }
    }

    void fetchPdf();

    return () => {
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [contractId]);

  const handleSign = async () => {
    if (!agreed || submitting) {
      return;
    }

    try {
      setSubmitting(true);
      setToast(null);

      const response = await contractApi.startESignEnvelope(contractId, {
        agreedToTerms: true,
        returnUrl: role === 'landlord'
          ? window.location.origin + ROUTE_PATHS.LANDLORD.ESIGN_RETURN
          : window.location.origin + ROUTE_PATHS.ACCOUNT.ESIGN_RETURN
      });

      const envelope = response.data;
      if (envelope.requiresOtp) {
        setOtpRequested(false);
        setOtpCode('');
        setSignatureImageBase64('');
        setShowOtpDialog(true);
        return;
      }

      const currentUserParticipant = envelope.participants.find(p => p.signerRole.toLowerCase() === role);

      if (currentUserParticipant && currentUserParticipant.signingUrl) {
        window.location.href = currentUserParticipant.signingUrl;
      } else {
        if (role === 'tenant') {
          setToast({ message: 'Hợp đồng đang chờ Chủ nhà ký trước. Sau khi Chủ nhà ký, VNPT sẽ tự động gửi email/SMS chứa đường dẫn ký số cho bạn. Vui lòng kiểm tra hộp thư của bạn.', type: 'info' });
        } else {
          setToast({ message: 'Không tìm thấy đường dẫn ký số. Vui lòng thử lại.', type: 'error' });
        }
      }
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Không thể khởi tạo phiên ký số. Vui lòng thử lại.'), type: 'error' });
    } finally {
      setSubmitting(false);
    }
  };

  const handleRequestOtp = async () => {
    if (otpSubmitting || !signatureImageBase64) return;

    try {
      setOtpSubmitting(true);
      setToast(null);
      const response = await contractApi.requestESignOtp(contractId, otpMethod);
      setMaskedDestination(response.data.maskedDestination);
      setOtpRequested(true);
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Không thể yêu cầu VNPT gửi OTP.'), type: 'error' });
    } finally {
      setOtpSubmitting(false);
    }
  };

  const handleOtpSubmit = async () => {
    if (!otpCode.trim() || otpSubmitting) return;

    try {
      setOtpSubmitting(true);
      setToast(null);

      await contractApi.submitESignOtp(contractId, {
        otpCode: otpCode.trim(),
        signatureImageBase64
      });

      setShowOtpDialog(false);
      onSuccess();
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Lỗi xác thực OTP. Vui lòng thử lại.'), type: 'error' });
    } finally {
      setOtpSubmitting(false);
    }
  };

  const handleDialogSubmit = async () => {
    if ((dialogType === 'revision' || dialogType === 'reject') && !reason.trim()) {
      setToast({ message: 'Vui lòng nhập lý do.', type: 'info' });
      return;
    }

    try {
      setSubmitting(true);
      setToast(null);

      if (dialogType === 'revision') {
        await apiClient(ENDPOINTS.CONTRACTS.REVISION_REQUEST(contractId), {
          method: 'POST',
          auth: true,
          body: { 
            reason: reason.trim(), 
            revisionType: role === 'tenant' ? revisionType : 'Occupants' 
          }
        });
      }

      if (dialogType === 'reject') {
        await apiClient(ENDPOINTS.CONTRACTS.REJECT(contractId), {
          method: 'POST',
          auth: true,
          body: { reason: reason.trim() }
        });
      }

      onSuccess();
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'), type: 'error' });
      setDialogType(null);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="contract-preview-modal-overlay">
      <div className="contract-preview-modal">
        <div className="modal-header">
          <h3>Xem trước và ký hợp đồng</h3>
          <button className="close-btn" onClick={onClose}>&times;</button>
        </div>

        <div className="modal-body" style={{ position: 'relative' }}>

          <div className="pdf-container" ref={containerRef}>
            {loading ? (
              <div className="loading-preview">Đang tải tài liệu...</div>
            ) : pdfUrl ? (
              <Document
                file={pdfUrl}
                onLoadSuccess={({ numPages: loadedPages }) => setNumPages(loadedPages)}
                loading={<div className="loading-preview">Đang xử lý PDF...</div>}
                error={<Alert type="error">Lỗi khi hiển thị PDF.</Alert>}
              >
                {numPages &&
                  Array.from(new Array(numPages), (_, index) => (
                    <Page
                      key={`page_${index + 1}`}
                      pageNumber={index + 1}
                      className="pdf-page"
                      renderTextLayer
                      renderAnnotationLayer
                      width={containerWidth || 800}
                    />
                  ))}
              </Document>
            ) : (
              <div className="loading-preview">Không có dữ liệu hợp đồng.</div>
            )}
          </div>

          <div className="agreement-section">
            <input
              type="checkbox"
              id="agree-checkbox"
              checked={agreed}
              onChange={(event) => setAgreed(event.target.checked)}
            />
            <label htmlFor="agree-checkbox">Tôi đã đọc và đồng ý với điều khoản hợp đồng</label>
          </div>

          {dialogType && (
            <div className="reason-dialog-overlay">
              <div className="reason-dialog">
                <h4>
                  {dialogType === 'revision'
                    ? 'Yêu cầu sửa đổi hợp đồng'
                    : 'Hủy hợp đồng'}
                </h4>
                {dialogType === 'revision' ? (
                  <>
                    {role === 'tenant' && (
                      <div className="revision-type-options" style={{ marginBottom: '16px', display: 'flex', flexDirection: 'column', gap: '8px', textAlign: 'left' }}>
                        <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                          <input 
                            type="radio" 
                            name="revisionType" 
                            value="Occupants" 
                            checked={revisionType === 'Occupants'} 
                            onChange={() => setRevisionType('Occupants')} 
                          /> 
                          <span>Sửa thông tin người ở</span>
                        </label>
                        <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                          <input 
                            type="radio" 
                            name="revisionType" 
                            value="ContractTerms" 
                            checked={revisionType === 'ContractTerms'} 
                            onChange={() => setRevisionType('ContractTerms')} 
                          /> 
                          <span>Yêu cầu chủ trọ sửa điều khoản (ngày bắt đầu, kết thúc, ngày thanh toán...)</span>
                        </label>
                      </div>
                    )}
                    <textarea
                      placeholder="Nhập lý do chi tiết..."
                      value={reason}
                      onChange={(event) => setReason(event.target.value)}
                      disabled={submitting}
                    />
                  </>
                ) : dialogType === 'reject' ? (
                  <>
                    <p className="reject-contract-warning">
                      {role === 'landlord' 
                        ? 'Bạn bắt buộc phải hoàn cọc cho khách nếu như muốn hủy hợp đồng. Bạn có chắc chắn muốn hủy hợp đồng không?'
                        : 'Tiền cọc đang giữ sẽ được hoàn về ví của bạn sau khi hủy hợp đồng. Bạn có chắc chắn muốn từ chối ký và hủy hợp đồng không?'
                      }
                    </p>
                    <textarea
                      placeholder="Nhập lý do hủy hợp đồng..."
                      value={reason}
                      onChange={(event) => setReason(event.target.value)}
                      disabled={submitting}
                    />
                  </>
                ) : null}
                <div className="reason-actions">
                  <Button variant="secondary" onClick={() => setDialogType(null)} disabled={submitting}>
                    Đóng
                  </Button>
                  <Button
                    variant={dialogType === 'reject' ? 'danger' : undefined}
                    onClick={() => void handleDialogSubmit()}
                    disabled={submitting}
                  >
                    {submitting ? 'Đang xử lý...' : dialogType === 'reject' ? 'Hủy hợp đồng' : 'Xác nhận'}
                  </Button>
                </div>
              </div>
            </div>
          )}
        </div>

        <div className="modal-footer">
          <Button variant="secondary" onClick={onClose} disabled={submitting}>
            Đóng
          </Button>
          <div className="footer-actions-group">
            <Button variant="danger" onClick={() => setDialogType('reject')} disabled={submitting}>
              Hủy hợp đồng
            </Button>
            <Button variant="secondary" onClick={() => setDialogType('revision')} disabled={submitting}>
              Yêu cầu sửa đổi
            </Button>
            <Button onClick={() => void handleSign()} disabled={!agreed || submitting}>
              {submitting && !dialogType ? 'Đang xử lý...' : 'Ký hợp đồng'}
            </Button>
          </div>
        </div>
      </div>

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
      {showOtpDialog && (
        <div className="esign-otp-overlay">
          <div className="esign-otp-dialog" role="dialog" aria-modal="true" aria-labelledby="contract-otp-title">
            <div className="esign-otp-header">
              <h2 id="contract-otp-title">Xác thực ký số OTP</h2>
              <button
                type="button"
                onClick={() => setShowOtpDialog(false)}
                disabled={otpSubmitting}
                className="esign-otp-close"
                aria-label="Đóng"
              >
                &times;
              </button>
            </div>
            <div className="esign-otp-content">

              {!otpRequested ? (
                <>
                  <p className="esign-otp-description">Chọn phương thức để VNPT gửi OTP và cung cấp ảnh chữ ký sẽ hiển thị trên hợp đồng.</p>
                  <div className="esign-otp-field">
                    <label className="esign-otp-label" htmlFor="contract-otp-method">Phương thức OTP</label>
                    <select
                      id="contract-otp-method"
                      value={otpMethod}
                      onChange={(event) => setOtpMethod(Number(event.target.value) as ESignOtpMethod)}
                      disabled={otpSubmitting}
                      className="esign-otp-control"
                    >
                      <option value={3}>Email OTP</option>
                    </select>
                  </div>
                  <SignatureInput
                    idPrefix="contract"
                    value={signatureImageBase64}
                    onChange={(base64) => {
                      setSignatureImageBase64(base64);
                      setToast(null);
                    }}
                    onError={(message) => setToast({ message, type: 'error' })}
                    disabled={otpSubmitting}
                  />
                </>
              ) : (
                <>
                  <p className="esign-otp-description">VNPT đã gửi OTP đến <strong>{maskedDestination}</strong>.</p>
                  <div className="esign-otp-field">
                    <label className="esign-otp-label" htmlFor="contract-otp-code">Mã OTP</label>
                    <input
                      id="contract-otp-code"
                      type="text"
                      inputMode="numeric"
                      value={otpCode}
                      onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, ''))}
                      disabled={otpSubmitting}
                      className="esign-otp-control"
                      placeholder="Nhập mã OTP..."
                      autoFocus
                    />
                  </div>
                </>
              )}
              <div className="esign-otp-actions">
                <Button
                  variant="outline"
                  onClick={() => setShowOtpDialog(false)}
                  disabled={otpSubmitting}
                >
                  Hủy
                </Button>
                {otpRequested ? (
                  <Button onClick={handleOtpSubmit} disabled={!otpCode.trim() || otpSubmitting}>
                    {otpSubmitting ? 'Đang xử lý...' : 'Xác nhận ký'}
                  </Button>
                ) : (
                  <Button onClick={handleRequestOtp} disabled={!signatureImageBase64 || otpSubmitting}>
                    {otpSubmitting ? 'Đang gửi...' : 'Gửi OTP'}
                  </Button>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
