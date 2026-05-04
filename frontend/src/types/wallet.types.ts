export interface Wallet {
  id: number;
  name: string;
  accountNumber?: string;
  type: string;
  currentBalance: number;
  isActive: boolean;
  notes?: string;
  createdAt: string;
}

export interface WalletTransaction {
  id: number;
  walletId: number;
  walletName: string;
  type: string;
  amount: number;
  balanceBefore: number;
  balanceAfter: number;
  referenceType?: string;
  referenceId?: number;
  referenceNumber?: string;
  description?: string;
  userName?: string;
  createdAt: string;
}

export interface CreateWalletRequest {
  name: string;
  accountNumber?: string;
  type: string;
  initialBalance: number;
  notes?: string;
}

export interface UpdateWalletRequest {
  name: string;
  accountNumber?: string;
  isActive: boolean;
  notes?: string;
}

export interface WalletDepositWithdrawRequest {
  amount: number;
  description?: string;
}

export interface WalletTransactionFilters {
  page?: number;
  pageSize?: number;
  type?: string;
  dateFrom?: string;
  dateTo?: string;
}

export interface PagedWalletTransactions {
  items: WalletTransaction[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
