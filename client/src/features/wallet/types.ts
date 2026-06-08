export interface Wallet {
  id: string;
  userId: string;
  balance: number;
  reservedBalance: number;
  availableBalance: number;
  currency: string;
  status: string;
  createdAt: string;
  updatedAt: string;
  canTopUp?: boolean;
  kycStatus?: string;
}

export interface WalletTransaction {
  id: string;
  walletAccountId: string;
  userId: string;
  transferGroupId?: string | null;
  transactionType: string;
  direction: string;
  amount: number;
  balanceBefore: number;
  balanceAfter: number;
  reservedBalanceBefore: number;
  reservedBalanceAfter: number;
  relatedEntityType?: string | null;
  relatedEntityId?: string | null;
  description?: string | null;
  status: string;
  createdAt: string;
}

export interface PagedWalletTransactions {
  items: WalletTransaction[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface CreatePayOSTopUpRequest {
  amount: number;
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

export interface MockPaymentResponse {
  paymentTransactionId?: string | null;
  webhookLogId?: string | null;
  processingStatus: string;
  signatureStatus: string;
  paymentStatus?: string | null;
  providerOrderCode?: string | null;
  message?: string | null;
}
