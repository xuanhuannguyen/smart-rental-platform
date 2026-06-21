import { contractApi } from './api';
import type { ContractFileResponse } from './types';

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
  const matchingFiles = files
    .filter((file) => file.rentalContractAppendixId === appendixId)
    .sort(compareAppendixFiles);

  return matchingFiles[0] ?? null;
}

function compareAppendixFiles(
  left: ContractFileResponse,
  right: ContractFileResponse,
): number {
  const variantDifference = getVariantPriority(left) - getVariantPriority(right);
  if (variantDifference !== 0) {
    return variantDifference;
  }

  return new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime();
}

function getVariantPriority(file: ContractFileResponse): number {
  return file.fileVariant === 'Raw' ? 0 : 1;
}
