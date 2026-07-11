import { Alert } from '../../../shared/components/ui/Alert';
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';
import { contractApi } from '../../contracts/api';
import type { ESignOtpMethod } from '../../contracts/types';
import { SignatureInput } from '../../contracts/components/SignatureInput';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Toast } from '../../../shared/components/ui/Toast';
import { Button } from '../../../shared/components/ui/Button';
import '../../../shared/components/ui/ESignOtpDialog.css';
import './AppendixPreviewModal.css';

pdfjs.GlobalWorkerOptions.workerSrc = `//unpkg.com/pdfjs-dist@${pdfjs.version}/build/pdf.worker.min.mjs`;

interface AppendixPreviewModalProps {
  contractId: string;
  appendixId: string;
  isCreator?: boolean;
  hasNoSignatures?: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function AppendixPreviewModal({ contractId, appendixId, isCreator, hasNoSignatures, onClose, onSuccess }: AppendixPreviewModalProps) {
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [numPages, setNumPages] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  const [agreed, setAgreed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [dialogType, setDialogType] = useState<'revision' | 'reject' | 'delete' | null>(null);
  const [reason, setReason] = useState('');
  const [showOtpDialog, setShowOtpDialog] = useState(false);
  const [otpRequested, setOtpRequested] = useState(false);
  const [otpSubmitting, setOtpSubmitting] = useState(false);
  const [otpMethod, setOtpMethod] = useState<ESignOtpMethod>(3);
  const [otpCode, setOtpCode] = useState('');
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

        const response = await contractApi.getAppendixPreviewPdf(contractId, appendixId);
        objectUrl = URL.createObjectURL(response);
        setPdfUrl(objectUrl);
      } catch (err: any) {
        setToast({ message: err.message || 'Không thể tải file PDF phụ lục. Vui lòng thử lại sau.', type: 'error' });
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
  }, [contractId, appendixId]);

  const handleSign = async () => {
    if (!agreed || submitting) {
      return;
    }

    try {
      setSubmitting(true);
      setToast(null);

      const response = await contractApi.startAppendixESignEnvelope(contractId, appendixId, {
        agreedToTerms: true
      });

      if (response.data.requiresOtp) {
        setOtpRequested(false);
        setOtpCode('');
        setSignatureImageBase64('');
        setShowOtpDialog(true);
      }
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Không thể khởi tạo phiên ký số. Vui lòng thử lại.'), type: 'error' });
    } finally {
      setSubmitting(false);
    }
  };

  const handleRequestOtp = async () => {
    if (!signatureImageBase64 || otpSubmitting) return;
    try {
      setOtpSubmitting(true);
      setToast(null);
      const response = await contractApi.requestAppendixESignOtp(contractId, appendixId, otpMethod);
      setMaskedDestination(response.data.maskedDestination);
      setOtpRequested(true);
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Không thể yêu cầu VNPT gửi OTP.'), type: 'error' });
    } finally {
      setOtpSubmitting(false);
    }
  };

  const handleSubmitOtp = async () => {
    if (!otpCode.trim() || otpSubmitting) return;
    try {
      setOtpSubmitting(true);
      setToast(null);
      await contractApi.submitAppendixESignOtp(contractId, appendixId, {
        otpCode: otpCode.trim(),
        signatureImageBase64
      });
      setShowOtpDialog(false);
      onSuccess();
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'VNPT không thể xác thực OTP.'), type: 'error' });
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
        await contractApi.requestAppendixRevision(contractId, appendixId, { reason: reason.trim() });
      }

      if (dialogType === 'reject') {
        await contractApi.rejectAppendix(contractId, appendixId, { reason: reason.trim() });
      }

      if (dialogType === 'delete') {
        await contractApi.deleteAppendix(contractId, appendixId);
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
    <div className="appendix-preview-modal-overlay">
      <div className="appendix-preview-modal">
        <div className="modal-header">
          <h3>Xem trước và ký phụ lục</h3>
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
              <div className="loading-preview">Không có dữ liệu phụ lục.</div>
            )}
          </div>

          <div className="agreement-section">
            <input
              type="checkbox"
              id="agree-checkbox"
              checked={agreed}
              onChange={(event) => setAgreed(event.target.checked)}
            />
            <label htmlFor="agree-checkbox">Tôi đã đọc và đồng ý với nội dung phụ lục</label>
          </div>

          {dialogType && (
            <div className="reason-dialog-overlay">
              <div className="reason-dialog">
                <h4>
                  {dialogType === 'revision'
                    ? 'Yêu cầu sửa đổi phụ lục'
                    : dialogType === 'reject'
                      ? 'Từ chối phụ lục'
                      : 'Hủy phụ lục'}
                </h4>
                {dialogType === 'revision' ? (
                  <textarea
                    placeholder="Nhập lý do chi tiết..."
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                    disabled={submitting}
                  />
                ) : dialogType === 'reject' || dialogType === 'delete' ? (
                  <>
                    <p className="reject-appendix-warning">
                      Bạn có chắc chắn muốn {dialogType === 'delete' ? 'hủy bỏ' : 'từ chối'} phụ lục này không? Hành động này không thể hoàn tác.
                    </p>
                    {dialogType === 'reject' && (
                      <textarea
                        placeholder="Nhập lý do từ chối..."
                        value={reason}
                        onChange={(event) => setReason(event.target.value)}
                        disabled={submitting}
                      />
                    )}
                  </>
                ) : null}
                <div className="reason-actions">
                  <Button variant="secondary" onClick={() => setDialogType(null)} disabled={submitting}>
                    Đóng
                  </Button>
                  <Button
                    variant={(dialogType === 'reject' || dialogType === 'delete') ? 'danger' : undefined}
                    onClick={() => void handleDialogSubmit()}
                    disabled={submitting}
                  >
                    {submitting ? 'Đang xử lý...' : dialogType === 'reject' ? 'Từ chối phụ lục' : dialogType === 'delete' ? 'Xác nhận hủy' : 'Xác nhận'}
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
            {isCreator && hasNoSignatures ? (
              <Button variant="danger" onClick={() => setDialogType('delete')} disabled={submitting}>
                Hủy phụ lục
              </Button>
            ) : (
              <Button variant="danger" onClick={() => setDialogType('reject')} disabled={submitting}>
                Từ chối
              </Button>
            )}
            {!isCreator && (
              <Button variant="secondary" onClick={() => setDialogType('revision')} disabled={submitting}>
                Yêu cầu sửa đổi
              </Button>
            )}
            <Button onClick={() => void handleSign()} disabled={!agreed || submitting}>
              {submitting && !dialogType ? 'Đang xử lý...' : 'Ký phụ lục'}
            </Button>
          </div>
        </div>
      </div>

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
      {showOtpDialog && (
        <div className="esign-otp-overlay">
          <div className="esign-otp-dialog" role="dialog" aria-modal="true" aria-labelledby="appendix-otp-title">
            <div className="esign-otp-header">
              <h2 id="appendix-otp-title">Xác thực chữ ký với VNPT</h2>
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
                  <p className="esign-otp-description">Chọn phương thức để VNPT gửi OTP và cung cấp ảnh chữ ký sẽ hiển thị trên phụ lục.</p>
                  <div className="esign-otp-field">
                    <label className="esign-otp-label" htmlFor="appendix-otp-method">Phương thức OTP</label>
                    <select
                      id="appendix-otp-method"
                      className="esign-otp-control"
                      value={otpMethod}
                      onChange={(event) => setOtpMethod(Number(event.target.value) as ESignOtpMethod)}
                      disabled={otpSubmitting}
                    >
                      <option value={3}>Email OTP</option>
                    </select>
                  </div>
                  <SignatureInput
                    idPrefix="appendix"
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
                    <label className="esign-otp-label" htmlFor="appendix-otp-code">Mã OTP</label>
                    <input
                      id="appendix-otp-code"
                      className="esign-otp-control"
                      type="text"
                      inputMode="numeric"
                      value={otpCode}
                      onChange={(event) => setOtpCode(event.target.value.replace(/\D/g, ''))}
                      disabled={otpSubmitting}
                      placeholder="Nhập mã OTP"
                    />
                  </div>
                </>
              )}
              <div className="esign-otp-actions">
                <Button variant="secondary" onClick={() => setShowOtpDialog(false)} disabled={otpSubmitting}>
                  Đóng
                </Button>
                {otpRequested ? (
                  <Button onClick={() => void handleSubmitOtp()} disabled={!otpCode.trim() || otpSubmitting}>
                    {otpSubmitting ? 'Đang xử lý...' : 'Xác nhận ký'}
                  </Button>
                ) : (
                  <Button onClick={() => void handleRequestOtp()} disabled={!signatureImageBase64 || otpSubmitting}>
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
