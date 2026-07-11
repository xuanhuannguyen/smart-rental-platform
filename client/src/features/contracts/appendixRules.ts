import type {
  ContractAppendixResponse,
  ContractSignerRole,
} from './types';

export function shouldShowAppendixToCurrentUser(
  appendix: ContractAppendixResponse,
  currentUserId: string,
): boolean {
  const isCreator = appendix.createdByUserId === currentUserId;

  if (appendix.status === 'Draft') {
    return isCreator;
  }

  if (appendix.status !== 'PendingSignature' || isCreator) {
    return true;
  }

  return appendix.signatures.some(
    (signature) => signature.signerUserId === appendix.createdByUserId,
  );
}

export function isBlockingAppendix(appendix: ContractAppendixResponse): boolean {
  return appendix.status === 'PendingSignature'
    || appendix.status === 'LandlordRevisionRequested'
    || appendix.status === 'TenantRevisionRequested';
}

export function canOpenFinalAppendixFile(appendix: ContractAppendixResponse): boolean {
  return appendix.status === 'Active' || appendix.status === 'Cancelled';
}

export function canTenantOpenAppendixForSigning(
  appendix: ContractAppendixResponse,
): boolean {
  return canPartyOpenAppendixForSigning(appendix, 'Tenant');
}

export function canLandlordOpenAppendixForSigning(
  appendix: ContractAppendixResponse,
): boolean {
  return canPartyOpenAppendixForSigning(appendix, 'Landlord');
}

export function formatAppendixStatus(
  appendix: ContractAppendixResponse,
  viewerRole: ContractSignerRole,
): string {
  if (appendix.status === 'PendingSignature') {
    const viewerHasSigned = hasRoleSigned(appendix, viewerRole);
    const otherRole: ContractSignerRole = viewerRole === 'Tenant' ? 'Landlord' : 'Tenant';
    const otherPartyHasSigned = hasRoleSigned(appendix, otherRole);

    if (!viewerHasSigned) {
      return 'Chờ bạn ký';
    }

    if (!otherPartyHasSigned) {
      return viewerRole === 'Tenant' ? 'Chờ chủ trọ ký' : 'Chờ khách thuê ký';
    }

    return 'Chờ hoàn tất';
  }

  if (appendix.status === 'LandlordRevisionRequested') {
    return viewerRole === 'Tenant' ? 'Đang chờ bạn sửa' : 'Đang chờ khách thuê sửa';
  }

  if (appendix.status === 'TenantRevisionRequested') {
    return viewerRole === 'Landlord' ? 'Đang chờ bạn sửa' : 'Đang chờ chủ trọ sửa';
  }

  switch (appendix.status) {
    case 'Draft':
      return 'Bản nháp';
    case 'Active':
      return 'Đang hiệu lực';
    case 'Rejected':
      return 'Đã từ chối';
    case 'Cancelled':
      return 'Đã kết thúc';
  }
}

function canPartyOpenAppendixForSigning(
  appendix: ContractAppendixResponse,
  signerRole: ContractSignerRole,
): boolean {
  return appendix.status === 'PendingSignature' && !hasRoleSigned(appendix, signerRole);
}

function hasRoleSigned(
  appendix: ContractAppendixResponse,
  signerRole: ContractSignerRole,
): boolean {
  return appendix.signatures.some((signature) => signature.signerRole === signerRole && signature.signedAt != null);
}
