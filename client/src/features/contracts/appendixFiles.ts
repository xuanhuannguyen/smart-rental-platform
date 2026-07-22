import { contractApi } from './api';
import type { ContractFileResponse } from './types';
import { findAccessibleDocumentFile } from './contractFiles';

export async function loadAccessibleContractFiles(
  contractId: string,
): Promise<ContractFileResponse[]> {
  const response = await contractApi.getContractFiles(contractId);
  return response.data ?? [];
}

export function findAccessibleAppendixFile(
  files: readonly ContractFileResponse[],
  appendixId: string,
): ContractFileResponse | null {
  return findAccessibleDocumentFile(files, appendixId);
}
