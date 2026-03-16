// Supplier Report Types

export interface SupplierPurchaseDetail {
  supplierId: number;
  supplierName: string;
  phone?: string;
  invoiceCount: number;
  totalPurchases: number;
  totalPaid: number;
  outstanding: number;
  lastPurchaseDate?: string;
  productCount: number;
}

export interface SupplierPurchasesReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  totalSuppliers: number;
  activeSuppliers: number;
  totalPurchases: number;
  totalPaid: number;
  totalOutstanding: number;
  totalInvoices: number;
  supplierDetails: SupplierPurchaseDetail[];
}

export interface SupplierDebtDetail {
  supplierId: number;
  supplierName: string;
  phone?: string;
  totalDue: number;
  unpaidInvoicesCount: number;
  oldestUnpaidInvoiceDate?: string;
  daysSinceOldestInvoice: number;
  lastPaymentDate?: string;
}

export interface SupplierDebtsReport {
  reportDate: string;
  branchId: number;
  branchName?: string;
  totalSuppliersWithDebt: number;
  totalOutstandingAmount: number;
  totalOverdueAmount: number;
  overdueInvoicesCount: number;
  supplierDebts: SupplierDebtDetail[];
}

export interface SupplierPerformanceDetail {
  supplierId: number;
  supplierName: string;
  totalInvoices: number;
  totalPurchaseValue: number;
  averageInvoiceValue: number;
  uniqueProductsSupplied: number;
  onTimePaymentRate: number;
  daysAveragePaymentDelay: number;
  reliabilityScore: string;
}

export interface SupplierPerformanceReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  supplierPerformance: SupplierPerformanceDetail[];
}
