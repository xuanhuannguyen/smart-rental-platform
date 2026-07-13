export type WalletStatus = 'Active' | 'Locked';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface WalletResponse {
  id: string;
  userId: string;
  balance: number;
  reservedBalance: number;
  availableBalance: number;
  currency: string;
  status: WalletStatus;
  createdAt: string;
  updatedAt: string;
}

export type WalletTransactionType =
  | 'WalletTopUp'
  | 'DepositPayment'
  | 'DepositReceive'
  | 'InvoicePayment'
  | 'InvoiceReceive'
  | 'DepositRefundDebit'
  | 'DepositRefundCredit'
  | 'DepositForfeitRelease'
  | 'ManualAdjustment'
  | 'WalletWithdrawalReserved'
  | 'WalletWithdrawalSucceeded'
  | 'WalletWithdrawalRefund';

export type WalletTransactionDirection = 'Debit' | 'Credit';
export type WalletTransactionStatus = 'Succeeded' | 'Pending' | 'Failed' | 'Expired' | 'Cancelled';

export interface WalletTransactionResponse {
  id: string;
  walletAccountId: string;
  userId: string;
  transferGroupId?: string | null;
  transactionType: WalletTransactionType;
  direction: WalletTransactionDirection;
  amount: number;
  balanceBefore: number;
  balanceAfter: number;
  reservedBalanceBefore: number;
  reservedBalanceAfter: number;
  relatedEntityType?: string | null;
  relatedEntityId?: string | null;
  description?: string | null;
  status: WalletTransactionStatus;
  createdAt: string;
}

export interface CreatePayOSTopUpRequest {
  amount: number;
  returnUrl: string;
  cancelUrl: string;
  note?: string;
  idempotencyKey?: string;
}

export interface CreatePayOSTopUpResponse {
  paymentTransactionId: string;
  amount: number;
  status: string;
  providerOrderCode: string;
  paymentUrl?: string | null;
  qrCode?: string | null;
  expiredAt: string;
}

export type WalletTopUpStatus = 'Pending' | 'Succeeded' | 'Failed' | 'Expired' | 'Cancelled';

export interface WalletTopUpResponse {
  id: string;
  amount: number;
  currency: string;
  paymentMethod: string;
  providerOrderCode: string;
  providerCheckoutUrl?: string | null;
  status: WalletTopUpStatus;
  expiresAt?: string | null;
  paidAt?: string | null;
  failedAt?: string | null;
  confirmedAt?: string | null;
  createdAt: string;
  gatewayResponseCode?: string | null;
  gatewayResponseMessage?: string | null;
}

export type WithdrawalStatus = 'PendingApproval' | 'Processing' | 'Succeeded' | 'Failed' | 'Rejected';

export interface CreateWithdrawalRequest {
  amount: number;
  bankBin: string;
  accountNumber: string;
  accountName: string;
}

export interface WithdrawalRequestResponse {
  id: string;
  walletAccountId: string;
  amount: number;
  fee: number;
  status: WithdrawalStatus;
  providerOrderCode: string;
  bankBin: string;
  accountName: string;
  accountNumber: string;
  description?: string | null;
  createdAt: string;
  updatedAt: string;
}
