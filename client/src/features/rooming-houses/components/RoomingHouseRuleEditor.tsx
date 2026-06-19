import { useEffect, useState } from 'react';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { uploadPdf } from '../../files/api';
import { upsertRoomingHouseRule } from '../api';
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
  const [pdfObjectKey, setPdfObjectKey] = useState(houseRule?.pdfObjectKey ?? '');
  const [form, setForm] = useState<UpsertRoomingHouseRuleRequest>(() =>
    buildForm(houseRule)
  );
  const [message, setMessage] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setSourceType(houseRule?.sourceType ?? 'PdfUpload');
    setPdfObjectKey(houseRule?.pdfObjectKey ?? '');
    setForm(buildForm(houseRule));
    setMessage('');
  }, [houseRule]);

  async function uploadRulePdf(file: File | null) {
    if (!file) return;
    setSaving(true);
    setMessage('');
    try {
      const uploaded = await uploadPdf(file, 'HouseRule');
      setPdfObjectKey(uploaded.objectKey);
      setMessage('Đã tải PDF lên. Bấm lưu để áp dụng luật khu trọ.');
    } catch (error) {
      setMessage(getApiErrorMessage(error, 'Không thể tải PDF luật khu trọ.'));
    } finally {
      setSaving(false);
    }
  }

  async function saveRule() {
    setSaving(true);
    setMessage('');
    try {
      const payload: UpsertRoomingHouseRuleRequest =
        sourceType === 'PdfUpload'
          ? { sourceType, pdfObjectKey }
          : { ...form, sourceType: 'FormGenerated' };
      const saved = await upsertRoomingHouseRule(roomingHouseId, payload);
      onSaved?.(saved);
      setMessage('Đã lưu luật khu trọ.');
    } catch (error) {
      setMessage(getApiErrorMessage(error, 'Không thể lưu luật khu trọ.'));
    } finally {
      setSaving(false);
    }
  }

  const canChooseSource = !lockedSourceType;

  return (
    <div className="rooming-house-rule-editor">
      {message && <p className="rooming-house-rule-editor__message">{message}</p>}
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
            Tải PDF lên
          </button>
          <button
            className={sourceType === 'FormGenerated' ? 'active' : ''}
            type="button"
            onClick={() => setSourceType('FormGenerated')}
          >
            Nhập bằng form
          </button>
        </div>
      )}

      {sourceType === 'PdfUpload' ? (
        <div className="rooming-house-rule-editor__pdf">
          {pdfObjectKey ? (
            <a href={toAssetUrl(pdfObjectKey)} target="_blank" rel="noreferrer">
              Xem PDF hiện tại
            </a>
          ) : (
            <p>Chưa có PDF luật khu trọ.</p>
          )}
          <label className="property-image-editor__upload">
            <span>{pdfObjectKey ? 'Thay PDF' : 'Tải PDF lên'}</span>
            <input
              accept="application/pdf"
              type="file"
              onChange={(event) => {
                void uploadRulePdf(event.target.files?.[0] ?? null);
                event.target.value = '';
              }}
            />
          </label>
        </div>
      ) : (
        <div className="rooming-house-rule-editor__form">
          <RuleTextarea
            label="Quy định chung"
            value={form.generalRules ?? ''}
            onChange={(value) => setForm({ ...form, generalRules: value })}
          />
          <RuleTextarea
            label="Giờ giấc yên tĩnh"
            value={form.quietHours ?? ''}
            onChange={(value) => setForm({ ...form, quietHours: value })}
          />
          <RuleTextarea
            label="An ninh"
            value={form.securityPolicy ?? ''}
            onChange={(value) => setForm({ ...form, securityPolicy: value })}
          />
          <RuleTextarea
            label="Vệ sinh"
            value={form.cleaningPolicy ?? ''}
            onChange={(value) => setForm({ ...form, cleaningPolicy: value })}
          />
          <RuleTextarea
            label="Khách ra vào"
            value={form.guestPolicy ?? ''}
            onChange={(value) => setForm({ ...form, guestPolicy: value })}
          />
          <RuleTextarea
            label="Gửi xe"
            value={form.parkingPolicy ?? ''}
            onChange={(value) => setForm({ ...form, parkingPolicy: value })}
          />
          <RuleTextarea
            label="Điện, nước và tiện ích"
            value={form.utilityPolicy ?? ''}
            onChange={(value) => setForm({ ...form, utilityPolicy: value })}
          />
          <RuleTextarea
            label="Bồi thường hư hỏng"
            value={form.damageCompensationPolicy ?? ''}
            onChange={(value) => setForm({ ...form, damageCompensationPolicy: value })}
          />
          <RuleTextarea
            label="Ghi chú bổ sung"
            value={form.additionalNotes ?? ''}
            onChange={(value) => setForm({ ...form, additionalNotes: value })}
          />
        </div>
      )}

      <div className="rooming-house-rule-editor__actions">
        <button className="rooming-house-editor__primary primary-action" disabled={saving} onClick={saveRule}>
          Lưu luật khu trọ
        </button>
      </div>
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
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="rooming-house-rule-editor__field">
      <span>{label}</span>
      <textarea value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}
