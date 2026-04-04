// Cash Register Transaction Type Enum
export type CashRegisterTransactionType =
  | "Opening"
  | "Deposit"
  | "Withdrawal"
  | "Sale"
  | "Refund"
  | "Expense"
  | "SupplierPayment"
  | "Adjustment"
  | "Transfer";

// Cash Register Transaction Interface
export interface CashRegisterTransaction {
  id: number;
  type: CashRegisterTransactionType;
  amount: number;
  balanceBefore: number;
  balanceAfter: number;
  description: string;
  referenceType?: string;
  referenceId?: number;
  shiftId?: number;
  branchId: number;
  branchName: string;
  // Backend currently returns userId/userName for cash register transactions.
  userId?: number;
  userName?: string;
  // Keep backward compatibility with older payloads.
  createdByUserId?: number;
  createdByUserName?: string;
  createdAt: string;
}

// Cash Register Balance Interface
export interface CashRegisterBalance {
  branchId: number;
  branchName: string;
  currentBalance: number;
  lastTransactionDate?: string;
  lastTransactionType?: CashRegisterTransactionType;
}

// Cash Register Summary Interface
export interface CashRegisterSummary {
  branchId: number;
  branchName: string;
  fromDate: string;
  toDate: string;
  openingBalance: number;
  closingBalance: number;
  totalDeposits: number;
  totalWithdrawals: number;
  totalSales: number;
  totalRefunds: number;
  totalExpenses: number;
  totalSupplierPayments: number;
  totalAdjustments: number;
  netChange: number;
  transactionCount: number;
}

// Request DTOs
export interface CreateCashRegisterTransactionRequest {
  type: "Deposit" | "Withdrawal";
  amount: number;
  description: string;
  shiftId?: number;
  branchId?: number;
}

export interface ReconcileCashRegisterRequest {
  actualBalance: number;
  varianceReason?: string;
}

export interface TransferCashRequest {
  fromBranchId: number;
  toBranchId: number;
  amount: number;
  description: string;
  shiftId?: number;
}

// Filters
export interface CashRegisterFilters {
  branchId?: number;
  type?: CashRegisterTransactionType;
  fromDate?: string;
  toDate?: string;
  shiftId?: number;
  pageNumber?: number;
  pageSize?: number;
}
