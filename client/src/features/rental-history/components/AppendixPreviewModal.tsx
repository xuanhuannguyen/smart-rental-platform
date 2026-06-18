import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';
import { contractApi } from '../../contracts/api';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { OtpInput } from '../../../shared/components/ui/OtpInput';
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
  const [error, setError] = useState('');
  const [agreed, setAgreed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [dialogType, setDialogType] = useState<'revision' | 'reject' | 'otp' | 'delete' | null>(null);
  const [reason, setReason] = useState('');
  const [otp, setOtp] = useState('');

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
        setError('');

        const response = await contractApi.getAppendixPreviewPdf(contractId, appendixId);
        objectUrl = URL.createObjectURL(response);
        setPdfUrl(objectUrl);
      } catch (err: any) {
        setError(err.message || 'Không thể tải file PDF phụ lục. Vui lòng thử lại sau.');
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
    if (!agreed) {
      return;
    }

    setDialogType('otp');
    setOtp('');

    try {
      setSubmitting(true);
      setError('');

      await contractApi.requestAppendixSignOtp(contractId, appendixId);
    } catch (err: any) {
      setError(getApiErrorMessage(err, 'Không thể gửi mã OTP. Vui lòng thử lại.'));
      setDialogType(null);
    } finally {
      setSubmitting(false);
    }
  };

  const handleDialogSubmit = async () => {
    if ((dialogType === 'revision' || dialogType === 'reject') && !reason.trim()) {
      setError('Vui lòng nhập lý do.');
      return;
    }
    if (dialogType === 'otp' && otp.length !== 6) {
      setError('Vui lòng nhập đủ 6 số OTP.');
      return;
    }

    try {
      setSubmitting(true);
      setError('');

      if (dialogType === 'revision') {
        await contractApi.requestAppendixRevision(contractId, appendixId, { reason: reason.trim() });
      }

      if (dialogType === 'reject') {
        await contractApi.rejectAppendix(contractId, appendixId, { reason: reason.trim() });
      }

      if (dialogType === 'delete') {
        await contractApi.deleteAppendix(contractId, appendixId);
      }

      if (dialogType === 'otp') {
        await contractApi.signAppendix(contractId, appendixId, { otp });
      }

      onSuccess();
    } catch (err: any) {
      setError(getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'));
      if (dialogType !== 'otp') {
        setDialogType(null);
      }
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
          {error && <Alert type="error">{error}</Alert>}

          <div className="pdf-container" ref={containerRef}>
            {loading ? (
              <div className="loading-preview">Đang tải tài liệu...</div>
            ) : pdfUrl ? (
              <Document
                file={pdfUrl}
                onLoadSuccess={({ numPages: loadedPages }) => setNumPages(loadedPages)}
                loading={<div className="loading-preview">Đang xử lý PDF...</div>}
                error={<div className="loading-preview error">Lỗi khi hiển thị PDF.</div>}
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
                      : dialogType === 'delete'
                        ? 'Hủy phụ lục'
                        : 'Nhập mã OTP'}
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
                ) : (
                  <>
                    <p className="subtle" style={{ marginTop: 0 }}>
                      Mã OTP đã được gửi đến email của bạn. Vui lòng kiểm tra và nhập vào bên dưới.
                    </p>
                    <div style={{ display: 'flex', justifyContent: 'center', margin: '20px 0' }}>
                      <OtpInput value={otp} onChange={setOtp} disabled={submitting} />
                    </div>
                  </>
                )}
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
    </div>
  );
}
