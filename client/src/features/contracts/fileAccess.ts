import { contractApi } from './api';
import type { ContractFileResponse } from './types';

export async function openContractFileForView(
  contractId: string,
  file: ContractFileResponse,
): Promise<void> {
  const access = await contractApi.getContractFileViewUrl(contractId, file.id);

  if (access.data?.deliveryMode === 'signed-url') {
    window.open(access.data.url, '_blank', 'noopener,noreferrer');
    return;
  }

  const blob = await contractApi.downloadContractFile(contractId, file.id);
  const objectUrl = URL.createObjectURL(blob);
  window.open(objectUrl, '_blank', 'noopener,noreferrer');
  window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
}
