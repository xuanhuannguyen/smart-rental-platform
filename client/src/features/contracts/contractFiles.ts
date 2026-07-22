import type { ContractFilePurpose, ContractFileResponse } from './types';

const DISPLAY_PURPOSE_PRIORITY: Record<ContractFilePurpose, number> = {
  SignedLegalDocument: 0,
  Preview: 1,
  MaskedReference: 2,
  UnsignedForESign: 3,
  ProviderEvidence: 4,
};

const DISPLAYABLE_CONTRACT_PURPOSES = new Set<ContractFilePurpose>([
  'SignedLegalDocument',
  'MaskedReference',
]);

export function findAccessibleContractFile(
  files: readonly ContractFileResponse[],
): ContractFileResponse | null {
  return findAccessibleDocumentFile(files, null);
}

export function findAccessibleDocumentFile(
  files: readonly ContractFileResponse[],
  appendixId: string | null,
): ContractFileResponse | null {
  const matchingFiles = files
    .filter((file) =>
      (file.rentalContractAppendixId ?? null) === appendixId &&
      DISPLAYABLE_CONTRACT_PURPOSES.has(file.purpose))
    .sort(compareContractFiles);

  return matchingFiles[0] ?? null;
}

function compareContractFiles(
  left: ContractFileResponse,
  right: ContractFileResponse,
): number {
  const purposeDifference =
    DISPLAY_PURPOSE_PRIORITY[left.purpose] - DISPLAY_PURPOSE_PRIORITY[right.purpose];
  if (purposeDifference !== 0) {
    return purposeDifference;
  }

  return new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime();
}
