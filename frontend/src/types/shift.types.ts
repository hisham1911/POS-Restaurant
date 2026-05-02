export interface Shift {
  id: number;
  userId?: number;
  openingBalance: number;
  closingBalance: number;
  expectedBalance: number;
  difference: number;
  openedAt: string;
  closedAt?: string;
  isClosed: boolean;
  notes?: string;
  totalCash: number;
  totalCard: number;
  totalFawry: number;
  totalBankTransfer: number;
  totalSales: number;
  totalCollected: number;
  deferredAmount: number;
  collectedCash: number;
  collectedCard: number;
  collectedFawry: number;
  collectedBankTransfer: number;
  totalOrders: number;
  userName?: string;
  orders?: ShiftOrder[];

  // Activity tracking
  lastActivityAt: string;
  inactiveHours: number;

  // Force close
  isForceClosed: boolean;
  forceClosedByUserName?: string;
  forceClosedAt?: string;
  forceCloseReason?: string;

  // Handover
  isHandedOver: boolean;
  handedOverFromUserName?: string;
  handedOverToUserName?: string;
  handedOverAt?: string;
  handoverBalance: number;
  handoverNotes?: string;

  // Calculated fields
  durationHours: number;
  durationMinutes: number;

  // Reconciliation
  isReconciled?: boolean;
  reconciledByUserName?: string;
  reconciledAt?: string;

  // Detailed sales by payment method
  totalCashSales?: number;
  totalCardSales?: number;
  totalFawrySales?: number;
  totalBankTransferSales?: number;
  totalVodafoneCashSales?: number;

  // Refunds & orders breakdown
  totalRefunds?: number;
  refundsCount?: number;
  totalOrdersCount?: number;
  completedOrdersCount?: number;
  cancelledOrdersCount?: number;
  refundedOrdersCount?: number;

  // Credit & net
  totalCreditSales?: number;
  creditOrdersCount?: number;
  netCash?: number;

  // Expenses
  totalExpenses?: number;

  // Debt Payments (سداد الديون)
  totalDebtPayments?: number;
  debtPaymentsCount?: number;
  totalDebtPaymentsCash?: number;
  totalDebtPaymentsCard?: number;
  totalDebtPaymentsFawry?: number;
  totalDebtPaymentsBankTransfer?: number;

  // Concurrency Token (for optimistic locking)
  rowVersion?: string;
}

export interface ShiftOrder {
  id: number;
  orderNumber: string;
  status: string;
  orderType?: string;
  total: number;
  customerName?: string;
  createdAt: string;
  completedAt?: string;
  paymentMethod?: string;
}

export interface OpenShiftRequest {
  openingBalance: number;
}

export interface CloseShiftRequest {
  closingBalance: number;
  notes?: string;
  // Concurrency Token (must be included for updates)
  rowVersion?: string;
}

export interface ForceCloseShiftRequest {
  reason: string;
  actualBalance?: number;
  notes?: string;
}

export interface HandoverShiftRequest {
  toUserId: number;
  currentBalance: number;
  notes?: string;
}

export interface ShiftWarning {
  level: "None" | "Warning" | "Critical";
  message: string;
  hoursOpen: number;
  shouldWarn: boolean;
  isCritical: boolean;
  shiftId?: number;
}
