export interface TopProduct {
  productId: number;
  productName: string;
  quantitySold: number;
  totalSales: number;
}

export interface HourlySales {
  hour: number;
  orderCount: number;
  sales: number;
}

export interface ShiftSummary {
  shiftId: number;
  userName: string;
  openedAt: string;
  closedAt: string;
  totalOrders: number;
  totalCash: number;
  totalCard: number;
  totalFawry: number;
  totalOther: number;
  totalSales: number;
  totalCollected: number;
  deferredAmount: number;
  collectedCash: number;
  collectedCard: number;
  collectedFawry: number;
  collectedOther: number;
  isForceClosed: boolean;
  forceCloseReason?: string;
}

export interface DailyReport {
  date: string;
  branchId: number;
  branchName?: string;
  // Shift Information
  totalShifts: number;
  shifts: ShiftSummary[];
  // Order Counts
  totalOrders: number;
  completedOrders: number;
  cancelledOrders: number;
  pendingOrders: number;
  // Sales Totals
  grossSales: number;
  totalDiscount: number;
  netSales: number;
  totalTax: number;
  totalSales: number;
  actualTotalSales: number;
  totalRefunds: number;
  // Payment Breakdown
  totalCash: number;
  totalCard: number;
  totalFawry: number;
  totalOther: number;
  totalCollected: number;
  totalDeferred: number;
  // Details
  topProducts: TopProduct[];
  hourlySales: HourlySales[];
}

export interface SalesReport {
  fromDate: string;
  toDate: string;
  totalSales: number;
  totalCost: number;
  grossProfit: number;
  totalOrders: number;
  averageOrderValue: number;
  dailySales: DailySales[];
}

export interface DailySales {
  date: string;
  sales: number;
  orders: number;
}
