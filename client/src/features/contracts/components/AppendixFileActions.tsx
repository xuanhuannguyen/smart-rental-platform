import { useState } from 'react';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Button } from '../../../shared/components/ui/Button';
import { contractApi } from '../api';
import { canOpenFinalAppendixFile } from '../appendixRules';
import type { ContractAppendixResponse, ContractFileResponse } from '../types';

interface AppendixFileActionsProps {
  contractId: string;
  contractNumber: string;
  appendix: ContractAppendixResponse;
  file: ContractFileResponse | null;
}

export function AppendixFileActions({
  contractId,
  contractNumber,
  appendix,
  file,
}: AppendixFileActionsProps) {
  const [activeMode, setActiveMode] = useState<'view' | 'download' | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!canOpenFinalAppendixFile(appendix) || !file) {
    return null;
  }

  async function handleFileAction(mode: 'view' | 'download') {
    if (!file) return;

    setActiveMode(mode);
    setError(null);

    try {
      const blob = await contractApi.downloadContractFile(contractId, file.id);
      const url = URL.createObjectURL(blob);

      if (mode === 'view') {
        window.open(url, '_blank', 'noopener,noreferrer');
        window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
        return;
      }

      const link = document.createElement('a');
      link.href = url;
      link.download = buildAppendixFileName(contractNumber, appendix.appendixNumber);
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tải file phụ lục đã ký.'));
    } finally {
      setActiveMode(null);
    }
  }

  return (
    <div style={{ marginTop: '8px' }}>
      <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
        <Button
          variant="outline"
          style={{ padding: '6px 12px', fontSize: '0.85rem' }}
          onClick={() => void handleFileAction('view')}
          disabled={activeMode !== null}
        >
          {activeMode === 'view' ? 'Đang mở...' : 'Xem phụ lục'}
        </Button>
        <Button
          variant="outline"
          style={{ padding: '6px 12px', fontSize: '0.85rem' }}
          onClick={() => void handleFileAction('download')}
          disabled={activeMode !== null}
        >
          {activeMode === 'download' ? 'Đang tải...' : 'Tải PDF'}
        </Button>
      </div>
      {error && (
        <div style={{ color: '#b91c1c', fontSize: '0.8rem', marginTop: '6px' }}>
          {error}
        </div>
      )}
    </div>
  );
}

function buildAppendixFileName(contractNumber: string, appendixNumber: string): string {
  const safeContractNumber = sanitizeFileNamePart(contractNumber);
  const safeAppendixNumber = sanitizeFileNamePart(appendixNumber);
  return `${safeContractNumber}-phu-luc-${safeAppendixNumber}.pdf`;
}

function sanitizeFileNamePart(value: string): string {
  return value.trim().replace(/[<>:"/\\|?*\u0000-\u001F]/g, '-');
}
