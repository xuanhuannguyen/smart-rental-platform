import { useEffect, useState } from 'react';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { buildPrivateMediaViewUrl } from '../../../shared/api/media';
import { type FileUploadResponse, uploadPdf } from '../../files/api';
import { upsertRoomingHouseRule, previewRoomingHouseRule } from '../api';
import { Toast } from '../../../shared/components/ui/Toast';
import type {
  HouseRuleSourceType,
  RoomingHouseRule,
  UpsertRoomingHouseRuleRequest,
} from '../types';
import './RoomingHouseRuleEditor.css';

type RoomingHouseRuleEditorProps = {
  roomingHouseId: string;
  houseRule?: RoomingHouseRule | null;
  onSaved?: (houseRule: RoomingHouseRule) => void;
};

const emptyRuleForm: UpsertRoomingHouseRuleRequest = {
  sourceType: 'FormGenerated',
  generalRules: '',
  quietHours: '',
  securityPolicy: '',
  cleaningPolicy: '',
  guestPolicy: '',
  parkingPolicy: '',
  utilityPolicy: '',
  damageCompensationPolicy: '',
  additionalNotes: '',
};

export default function RoomingHouseRuleEditor({
  roomingHouseId,
  houseRule,
  onSaved,
}: RoomingHouseRuleEditorProps) {
  const lockedSourceType = houseRule?.sourceType;
  const [sourceType, setSourceType] = useState<HouseRuleSourceType>(
    lockedSourceType ?? 'PdfUpload'
  );
  const [pdfMediaAssetId, setPdfMediaAssetId] = useState(houseRule?.mediaAssetId ?? null);
  const [uploadedPdf, setUploadedPdf] = useState<FileUploadResponse | null>(null);
  const [form, setForm] = useState<UpsertRoomingHouseRuleRequest>(() =>
    buildForm(houseRule)
  );
  const [validationError, setValidationError] = useState('');
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  const [saving, setSaving] = useState(false);
  const [previewing, setPreviewing] = useState(false);

  useEffect(() => {
    setSourceType(houseRule?.sourceType ?? 'PdfUpload');
    setPdfMediaAssetId(houseRule?.mediaAssetId ?? null);
    setUploadedPdf(null);
    setForm(buildForm(houseRule));
    setValidationError('');
  }, [houseRule]);

  async function uploadRulePdf(file: File | null) {
    if (!file) return;
    setSaving(true);
    setValidationError('');
    try {
      const uploaded = await uploadPdf(file, 'HouseRule');
      setPdfMediaAssetId(uploaded.mediaAssetId || null);
      setUploadedPdf(uploaded);
      setToast({ message: 'Đã tải PDF lên. Bấm lưu để áp dụng luật khu trọ.', type: 'success' });
    } catch (error) {
      setToast({ message: getApiErrorMessage(error, 'Không thể tải PDF luật khu trọ.'), type: 'error' });
    } finally {
      setSaving(false);
    }
  }

  async function saveRule() {
    const isEmpty = sourceType === 'PdfUpload'
      ? !pdfMediaAssetId
      : !form.generalRules && !form.quietHours && !form.securityPolicy && !form.cleaningPolicy && !form.guestPolicy && !form.parkingPolicy && !form.utilityPolicy && !form.damageCompensationPolicy && !form.additionalNotes;

    if (isEmpty) {
      setValidationError('Vui lòng nhập ít nhất một nội dung luật khu trọ.');
      return;
    }

    setSaving(true);
    setValidationError('');
    try {
      const payload: UpsertRoomingHouseRuleRequest =
        sourceType === 'PdfUpload'
          ? { sourceType, pdfMediaAssetId }
          : { ...form, sourceType: 'FormGenerated' };
      const saved = await upsertRoomingHouseRule(roomingHouseId, payload);
      setUploadedPdf(null);
      onSaved?.(saved);
      setToast({ message: 'Đã lưu luật khu trọ.', type: 'success' });
    } catch (error) {
      setToast({ message: getApiErrorMessage(error, 'Không thể lưu luật khu trọ.'), type: 'error' });
    } finally {
      setSaving(false);
    }
  }

  async function handlePreview() {
    setPreviewing(true);
    setValidationError('');
    try {
      const payload: UpsertRoomingHouseRuleRequest = {
        ...form,
        sourceType: 'FormGenerated',
      };
      const blob = await previewRoomingHouseRule(roomingHouseId, payload);
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (error) {
      setToast({ message: getApiErrorMessage(error, 'Không thể hiển thị bản xem trước PDF.'), type: 'error' });
    } finally {
      setPreviewing(false);
    }
  }

  const canChooseSource = !lockedSourceType;

  const isEmpty = sourceType === 'PdfUpload'
    ? !pdfMediaAssetId
    : !form.generalRules && !form.quietHours && !form.securityPolicy && !form.cleaningPolicy && !form.guestPolicy && !form.parkingPolicy && !form.utilityPolicy && !form.damageCompensationPolicy && !form.additionalNotes;
  const pdfLink = uploadedPdf?.url || houseRule?.pdfUrl || (pdfMediaAssetId ? buildPrivateMediaViewUrl(pdfMediaAssetId) : '');
  const hasPdf = Boolean(pdfMediaAssetId || pdfLink);

  return (
    <div className="rooming-house-rule-editor">
      {/* Alert banner */}
      {(() => {
        if (validationError) {
          return (
            <div className="rooming-house-rule-editor__alert rooming-house-rule-editor__alert--danger">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10" />
                <line x1="12" y1="8" x2="12" y2="12" />
                <line x1="12" y1="16" x2="12.01" y2="16" />
              </svg>
              <span>{validationError}</span>
            </div>
          );
        }
        
        if (isEmpty) {
          return (
            <div className="rooming-house-rule-editor__alert rooming-house-rule-editor__alert--warning">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                <line x1="12" y1="9" x2="12" y2="13" />
                <line x1="12" y1="17" x2="12.01" y2="17" />
              </svg>
              <span>Vui lòng nhập ít nhất một nội dung luật khu trọ.</span>
            </div>
          );
        }
        
        return null;
      })()}

      {!canChooseSource && (
        <p className="rooming-house-rule-editor__note">
          Luật khu trọ đã được tạo bằng{' '}
          {lockedSourceType === 'PdfUpload' ? 'PDF tải lên' : 'form'}. Các lần chỉnh sửa sau giữ nguyên cách tạo này.
        </p>
      )}

      {canChooseSource && (
        <div className="rooming-house-rule-editor__mode">
          <button
            className={sourceType === 'PdfUpload' ? 'active' : ''}
            type="button"
            onClick={() => setSourceType('PdfUpload')}
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M20.39 18.39A5 5 0 0 0 18 9h-1.26A8 8 0 1 0 3 16.3" />
              <polyline points="16 16 12 12 8 16" />
              <line x1="12" y1="12" x2="12" y2="21" />
            </svg>
            Tải PDF lên
          </button>
          <button
            className={sourceType === 'FormGenerated' ? 'active' : ''}
            type="button"
            onClick={() => setSourceType('FormGenerated')}
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
              <polyline points="14 2 14 8 20 8" />
              <line x1="16" y1="13" x2="8" y2="13" />
              <line x1="16" y1="17" x2="8" y2="17" />
            </svg>
            Nhập bằng form
          </button>
        </div>
      )}

      {sourceType === 'PdfUpload' ? (
        <div className="rooming-house-rule-pdf-section">
          {hasPdf ? (
            <>
              <p className="pdf-status-text">Đã tải lên PDF luật khu trọ.</p>
              <div className="pdf-upload-dropzone">
                <div className="pdf-upload-dropzone__left">
                  <div className="pdf-icon-wrapper">
                    <svg width="40" height="48" viewBox="0 0 40 48" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M26 2H6C3.79086 2 2 3.79086 2 6V42C2 44.2091 3.79086 46 6 46H34C36.2091 46 38 44.2091 38 42V14L26 2Z" stroke="#3b82f6" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" fill="#eff6ff" />
                      <path d="M26 2V14H38" stroke="#3b82f6" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
                      <rect x="2" y="28" width="18" height="10" rx="2" fill="#2563eb" />
                      <text x="4" y="35" fill="white" fontSize="7" fontWeight="bold" fontFamily="system-ui, sans-serif">PDF</text>
                    </svg>
                  </div>
                </div>
                <div className="pdf-upload-dropzone__middle">
                  <h5>Luật_khu_trọ.pdf</h5>
                  <p>
                    <a href={pdfLink} target="_blank" rel="noreferrer" className="pdf-view-link">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '4px', verticalAlign: 'middle' }}>
                        <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
                        <polyline points="15 3 21 3 21 9" />
                        <line x1="10" y1="14" x2="21" y2="3" />
                      </svg>
                      Xem PDF hiện tại
                    </a>
                  </p>
                </div>
                <div className="pdf-upload-dropzone__right">
                  <label className="choose-file-btn choose-file-btn--secondary">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                      <polyline points="17 8 12 3 7 8" />
                      <line x1="12" y1="3" x2="12" y2="15" />
                    </svg>
                    Thay đổi tệp
                    <input
                      accept="application/pdf"
                      type="file"
                      style={{ display: 'none' }}
                      onChange={(event) => {
                        void uploadRulePdf(event.target.files?.[0] ?? null);
                        event.target.value = '';
                      }}
                    />
                  </label>
                </div>
              </div>
            </>
          ) : (
            <>
              <p className="pdf-status-text">Chưa có PDF luật khu trọ.</p>
              <div className="pdf-upload-dropzone">
                <div className="pdf-upload-dropzone__left">
                  <div className="pdf-icon-wrapper">
                    <svg width="40" height="48" viewBox="0 0 40 48" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M26 2H6C3.79086 2 2 3.79086 2 6V42C2 44.2091 3.79086 46 6 46H34C36.2091 46 38 44.2091 38 42V14L26 2Z" stroke="#cbd5e1" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" fill="#f8fafc" />
                      <path d="M26 2V14H38" stroke="#cbd5e1" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
                      <rect x="2" y="28" width="18" height="10" rx="2" fill="#2563eb" />
                      <text x="4" y="35" fill="white" fontSize="7" fontWeight="bold" fontFamily="system-ui, sans-serif">PDF</text>
                    </svg>
                  </div>
                </div>
                <div className="pdf-upload-dropzone__middle">
                  <h5>Tải PDF lên</h5>
                  <p>Chọn tệp PDF từ thiết bị của bạn hoặc kéo thả vào đây.</p>
                </div>
                <div className="pdf-upload-dropzone__right">
                  <label className="choose-file-btn">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
                    </svg>
                    Chọn tệp
                    <input
                      accept="application/pdf"
                      type="file"
                      style={{ display: 'none' }}
                      onChange={(event) => {
                        void uploadRulePdf(event.target.files?.[0] ?? null);
                        event.target.value = '';
                      }}
                    />
                  </label>
                </div>
              </div>
            </>
          )}
        </div>
      ) : (
        <div className="rooming-house-rule-editor__form">
          <RuleTextarea
            label="Quy định chung"
            placeholder="Nhập quy định chung của khu trọ..."
            value={form.generalRules ?? ''}
            onChange={(value) => setForm({ ...form, generalRules: value })}
            icon={
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="3" y="3" width="7" height="9" />
                <rect x="14" y="3" width="7" height="5" />
                <rect x="14" y="12" width="7" height="9" />
                <rect x="3" y="16" width="7" height="5" />
              </svg>
            }
          />
          <RuleTextarea
            label="Giờ giấc yên tĩnh"
            placeholder="Nhập quy định về giờ giấc yên tĩnh..."
            value={form.quietHours ?? ''}
            onChange={(value) => setForm({ ...form, quietHours: value })}
            icon={
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10" />
                <polyline points="12 6 12 12 16 14" />
              </svg>
            }
          />
          <RuleTextarea
            label="An ninh"
            placeholder="Nhập quy định về an ninh, an toàn..."
            value={form.securityPolicy ?? ''}
            onChange={(value) => setForm({ ...form, securityPolicy: value })}
            icon={
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
              </svg>
            }
          />
          <RuleTextarea
            label="Vệ sinh"
            placeholder="Nhập quy định về vệ sinh chung..."
            value={form.cleaningPolicy ?? ''}
            onChange={(value) => setForm({ ...form, cleaningPolicy: value })}
            icon={
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364-6.364l-.707.707M6.343 17.657l-.707.707m0-12.728l.707.707m11.314 11.314l.707.707M12 8a4 4 0 100 8 4 4 0 000-8z" />
              </svg>
            }
          />
          <RuleTextarea
            label="Khách ra vào"
            placeholder="Nhập quy định về khách ra vào..."
            value={form.guestPolicy ?? ''}
            onChange={(value) => setForm({ ...form, guestPolicy: value })}
            icon={
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                <circle cx="9" cy="7" r="4" />
                <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                <path d="M16 3.13a4 4 0 0 1 0 7.75" />
              </svg>
            }
          />
          <RuleTextarea
            label="Gửi xe"
            placeholder="Nhập quy định về gửi xe..."
            value={form.parkingPolicy ?? ''}
            onChange={(value) => setForm({ ...form, parkingPolicy: value })}
            icon={
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="6" cy="18" r="3" />
                <circle cx="18" cy="18" r="3" />
                <path d="M6 18h12M12 9l-4 5h8zM18 18v-5l-4-4H9.5" />
              </svg>
            }
          />
          <RuleTextarea
            label="Điện, nước và tiện ích"
            placeholder="Nhập quy định về điện, nước và các tiện ích..."
            value={form.utilityPolicy ?? ''}
            onChange={(value) => setForm({ ...form, utilityPolicy: value })}
            icon={
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z" />
              </svg>
            }
          />
          <RuleTextarea
            label="Bồi thường hư hỏng"
            placeholder="Nhập quy định về bồi thường hư hỏng..."
            value={form.damageCompensationPolicy ?? ''}
            onChange={(value) => setForm({ ...form, damageCompensationPolicy: value })}
            icon={
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M14.5 10l-9 9-2.5-2.5 9-9zM15 9l3-3 2.5 2.5-3 3z" />
              </svg>
            }
          />
          <RuleTextarea
            label="Ghi chú bổ sung"
            placeholder="Nhập ghi chú hoặc thông tin bổ sung khác (nếu có)..."
            value={form.additionalNotes ?? ''}
            onChange={(value) => setForm({ ...form, additionalNotes: value })}
            icon={
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                <polyline points="14 2 14 8 20 8" />
                <line x1="16" y1="13" x2="8" y2="13" />
                <line x1="16" y1="17" x2="8" y2="17" />
              </svg>
            }
            className="rooming-house-rule-card--full"
          />
        </div>
      )}

      <div className="rooming-house-rule-editor__actions">
        {sourceType === 'FormGenerated' && (
          <button
            className="rooming-house-rule-editor__preview-btn"
            type="button"
            disabled={saving || previewing}
            onClick={handlePreview}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" width="18" height="18">
              <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
              <circle cx="12" cy="12" r="3" />
            </svg>
            <span>{previewing ? 'Đang tải...' : 'Xem trước'}</span>
          </button>
        )}
        <button
          className="rooming-house-editor__primary primary-action"
          type="button"
          disabled={saving || previewing}
          onClick={saveRule}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" width="18" height="18">
            <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
            <polyline points="17 21 17 13 7 13 7 21" />
            <polyline points="7 3 7 8 15 8" />
          </svg>
          <span>{saving ? 'Đang lưu...' : 'Lưu luật khu trọ'}</span>
        </button>
      </div>

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}

function buildForm(rule?: RoomingHouseRule | null): UpsertRoomingHouseRuleRequest {
  if (!rule || rule.sourceType !== 'FormGenerated') {
    return emptyRuleForm;
  }

  return {
    sourceType: 'FormGenerated',
    generalRules: rule.generalRules ?? '',
    quietHours: rule.quietHours ?? '',
    securityPolicy: rule.securityPolicy ?? '',
    cleaningPolicy: rule.cleaningPolicy ?? '',
    guestPolicy: rule.guestPolicy ?? '',
    parkingPolicy: rule.parkingPolicy ?? '',
    utilityPolicy: rule.utilityPolicy ?? '',
    damageCompensationPolicy: rule.damageCompensationPolicy ?? '',
    additionalNotes: rule.additionalNotes ?? '',
  };
}

function RuleTextarea({
  label,
  placeholder,
  value,
  onChange,
  icon,
  className = '',
}: {
  label: string;
  placeholder?: string;
  value: string;
  onChange: (value: string) => void;
  icon?: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={`rooming-house-rule-card ${className}`}>
      <div className="rooming-house-rule-card__header">
        {icon && <div className="rooming-house-rule-card__icon">{icon}</div>}
        <span className="rooming-house-rule-card__title">{label}</span>
      </div>
      <div className="rooming-house-rule-card__body">
        <textarea
          placeholder={placeholder}
          value={value}
          onChange={(event) => onChange(event.target.value)}
        />
      </div>
    </div>
  );
}
