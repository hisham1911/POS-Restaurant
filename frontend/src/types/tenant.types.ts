export interface Tenant {
  id: number;
  name: string;
  nameEn?: string;
  slug: string;
  logoUrl?: string;
  currency: string;
  timezone: string;
  isActive: boolean;
  // Tax Settings
  taxRate: number;
  isTaxEnabled: boolean;
  // Inventory Settings
  allowNegativeStock: boolean;
  // Receipt Settings
  receiptPaperSize: string;
  receiptCustomWidth?: number;
  receiptHeaderFontSize: number;
  receiptBodyFontSize: number;
  receiptTotalFontSize: number;
  receiptShowBranchName: boolean;
  receiptShowCashier: boolean;
  receiptShowThankYou: boolean;
  receiptFooterMessage?: string;
  receiptPhoneNumber?: string;
  receiptShowCustomerName: boolean;
  receiptShowLogo: boolean;
  // Print Routing Settings
  printRoutingMode: 'BranchOnly' | 'BranchWithFallback' | 'AllDevices' | 'Disabled';
  autoPrintOnSale: boolean;
  autoPrintOnDebtPayment: boolean;
  autoPrintDailyReports: boolean;
  createdAt: string;
}

export interface UpdateTenantRequest {
  name: string;
  nameEn?: string;
  logoUrl?: string;
  currency: string;
  timezone: string;
  // Tax Settings
  taxRate?: number;
  isTaxEnabled?: boolean;
  // Inventory Settings
  allowNegativeStock?: boolean;
  // Receipt Settings
  receiptPaperSize?: string;
  receiptCustomWidth?: number;
  receiptHeaderFontSize?: number;
  receiptBodyFontSize?: number;
  receiptTotalFontSize?: number;
  receiptShowBranchName?: boolean;
  receiptShowCashier?: boolean;
  receiptShowThankYou?: boolean;
  receiptFooterMessage?: string;
  receiptPhoneNumber?: string;
  receiptShowCustomerName?: boolean;
  receiptShowLogo?: boolean;
  // Print Routing Settings
  printRoutingMode?: 'BranchOnly' | 'BranchWithFallback' | 'AllDevices' | 'Disabled';
  autoPrintOnSale?: boolean;
  autoPrintOnDebtPayment?: boolean;
  autoPrintDailyReports?: boolean;
}
