// Employee Report Types

export interface CashierPerformanceDetail {
  userId: number;
  userName: string;
  email: string;
  totalShifts: number;
  completedShifts: number;
  forceClosedShifts: number;
  averageShiftDuration: number;
  totalOrders: number;
  totalRevenue: number;
  averageOrderValue: number;
  ordersPerHour: number;
  completedOrders: number;
  cancelledOrders: number;
  refundedOrders: number;
  cancellationRate: number;
  cashSales: number;
  cardSales: number;
  fawrySales: number;
  performanceScore: number;
  performanceRating: string;
}

export interface CashierPerformanceReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  totalCashiers: number;
  totalShifts: number;
  totalRevenue: number;
  totalOrders: number;
  cashierPerformance: CashierPerformanceDetail[];
}

export interface DetailedShift {
  shiftId: number;
  userName: string;
  openedAt: string;
  closedAt?: string;
  duration: number;
  openingBalance: number;
  closingBalance: number;
  expectedBalance: number;
  variance: number;
  totalOrders: number;
  totalCash: number;
  totalCard: number;
  totalFawry: number;
  totalSales: number;
  isForceClosed: boolean;
  forceCloseReason?: string;
  closedByUserName?: string;
}

export interface DetailedShiftsReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  totalShifts: number;
  completedShifts: number;
  forceClosedShifts: number;
  totalRevenue: number;
  averageShiftRevenue: number;
  shifts: DetailedShift[];
}

export interface DailyEmployeeSales {
  date: string;
  orders: number;
  revenue: number;
}

export interface EmployeeSalesDetail {
  userId: number;
  userName: string;
  role: string;
  totalOrders: number;
  totalRevenue: number;
  averageOrderValue: number;
  revenuePercentage: number;
  dailySales: DailyEmployeeSales[];
}

export interface SalesByEmployeeReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  totalRevenue: number;
  totalOrders: number;
  totalEmployees: number;
  employeeSales: EmployeeSalesDetail[];
}
