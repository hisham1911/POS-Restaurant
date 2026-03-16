// Top Customers Report Types
export interface TopCustomer {
  customerId: number;
  customerName: string;
  phone: string;
  totalOrders: number;
  totalSpent: number;
  averageOrderValue: number;
  lastOrderDate?: string;
  outstandingBalance: number;
}

export interface TopCustomersReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  
  totalCustomers: number;
  activeCustomers: number;
  newCustomers: number;
  totalRevenue: number;
  averageCustomerValue: number;
  
  topCustomers: TopCustomer[];
}

// Customer Debts Report Types
export interface CustomerDebtDetail {
  customerId: number;
  customerName: string;
  phone: string;
  totalDue: number;
  creditLimit: number;
  daysSinceLastOrder: number;
  lastOrderDate?: string;
  oldestUnpaidOrderDate?: string;
  unpaidOrdersCount: number;
  isOverLimit: boolean;
}

export interface AgingBracket {
  bracket: string;
  customerCount: number;
  totalAmount: number;
  percentage: number;
}

export interface CustomerDebtsReport {
  reportDate: string;
  branchId: number;
  branchName?: string;
  
  totalCustomersWithDebt: number;
  totalOutstandingAmount: number;
  totalOverdueAmount: number;
  overdueCustomersCount: number;
  
  customerDebts: CustomerDebtDetail[];
  agingAnalysis: AgingBracket[];
}

// Customer Activity Report Types
export interface CustomerSegment {
  segmentName: string;
  customerCount: number;
  totalRevenue: number;
  averageOrderValue: number;
  totalOrders: number;
}

export interface CustomerActivityReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  
  newCustomers: number;
  returningCustomers: number;
  inactiveCustomers: number;
  
  newCustomerRevenue: number;
  returningCustomerRevenue: number;
  averageNewCustomerValue: number;
  averageReturningCustomerValue: number;
  
  retentionRate: number;
  churnRate: number;
  
  customerSegments: CustomerSegment[];
}
