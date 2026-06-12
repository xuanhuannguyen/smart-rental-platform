import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';
import { ENDPOINTS } from '../../../shared/api/endpoints';
import { apiClient } from '../../../shared/api/apiClient';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import './ContractPreviewModal.css';

pdfjs.GlobalWorkerOptions.workerSrc = `//unpkg.com/pdfjs-dist@${pdfjs.version}/build/pdf.worker.min.mjs`;

interface ContractPreviewModalProps {
  contractId: string;
  onClose: () => void;
  onSuccess: () => void;
}

export function ContractPreviewModal({ contractId, onClose, onSuccess }: ContractPreviewModalProps) {
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [numPages, setNumPages] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [agreed, setAgreed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [dialogType, setDialogType] = useState<'revision' | 'reject' | null>(null);
  const [reason, setReason] = useState('');

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

        const response = await apiClient<Blob>(ENDPOINTS.CONTRACTS.PREVIEW_PDF(contractId), {
          auth: true,
          responseType: 'blob'
        });

        objectUrl = URL.createObjectURL(response);
        setPdfUrl(objectUrl);
      } catch (err: any) {
        setError(err?.response?.data?.message || 'Không thể tải file PDF hợp đồng. Vui lòng thử lại sau.');
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
    if (!agreed) {
      return;
    }

    try {
      setSubmitting(true);
      setError('');

      await apiClient(ENDPOINTS.CONTRACTS.LANDLORD_SIGN(contractId), {
        method: 'POST',
        auth: true,
        body: { otp: '000000' }
      });

      onSuccess();
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Có lỗi xảy ra khi ký hợp đồng.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleDialogSubmit = async () => {
    if ((dialogType === 'revision' || dialogType === 'reject') && !reason.trim()) {
      setError('Vui lòng nhập lý do.');
      return;
    }

    try {
      setSubmitting(true);
      setError('');

      if (dialogType === 'revision') {
        await apiClient(ENDPOINTS.CONTRACTS.REVISION_REQUEST(contractId), {
          method: 'POST',
          auth: true,
          body: { reason: reason.trim(), revisionType: 'Occupants' }
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
      setError(err?.response?.data?.message || 'Có lỗi xảy ra. Vui lòng thử lại.');
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
                <h4>{dialogType === 'revision' ? 'Yêu cầu sửa đổi hợp đồng' : 'Hủy hợp đồng'}</h4>
                {dialogType === 'revision' ? (
                  <textarea
                    placeholder="Nhập lý do chi tiết..."
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                    disabled={submitting}
                  />
                ) : (
                  <>
                    <p className="reject-contract-warning">
                      Bạn bắt buộc phải hoàn cọc cho khách nếu như muốn hủy hợp đồng.
                      Bạn có chắc chắn muốn hủy hợp đồng không?
                    </p>
                    <textarea
                      placeholder="Nhập lý do hủy hợp đồng..."
                      value={reason}
                      onChange={(event) => setReason(event.target.value)}
                      disabled={submitting}
                    />
                  </>
                )}
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
    </div>
  );
}
