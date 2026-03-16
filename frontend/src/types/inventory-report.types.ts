// Branch Inventory Report Types
export interface BranchInventoryItem {
  productId: number;
  productName: string;
  productSku?: string;
  categoryName?: string;
  quantity: number;
  reorderLevel: number;
  isLowStock: boolean;
  averageCost?: number;
  totalValue?: number;
  lastUpdatedAt: string;
}

export interface BranchInventoryReport {
  branchId: number;
  branchName: string;
  totalProducts: number;
  totalQuantity: number;
  lowStockCount: number;
  totalValue: number;
  items: BranchInventoryItem[];
}

// Unified Inventory Report Types
export interface BranchStock {
  branchId: number;
  branchName: string;
  quantity: number;
  reorderLevel: number;
  isLowStock: boolean;
}

export interface UnifiedInventoryReport {
  productId: number;
  productName: string;
  productSku?: string;
  categoryName?: string;
  totalQuantity: number;
  averageCost?: number;
  totalValue?: number;
  branchCount: number;
  lowStockBranchCount: number;
  branchStocks: BranchStock[];
}

// Transfer History Report Types
export interface TransferSummary {
  id: number;
  transferNumber: string;
  createdAt: string;
  fromBranchName: string;
  toBranchName: string;
  productName: string;
  quantity: number;
  status: string;
  reason: string;
  completedAt?: string;
}

export interface BranchTransferStats {
  branchId: number;
  branchName: string;
  transfersSent: number;
  transfersReceived: number;
  quantitySent: number;
  quantityReceived: number;
  netChange: number;
}

export interface TransferHistoryReport {
  fromDate: string;
  toDate: string;
  totalTransfers: number;
  completedTransfers: number;
  pendingTransfers: number;
  cancelledTransfers: number;
  totalQuantityTransferred: number;
  transfers: TransferSummary[];
  branchStats: BranchTransferStats[];
}

// Low Stock Summary Report Types
export interface BranchLowStockDetail {
  branchId: number;
  branchName: string;
  quantity: number;
  reorderLevel: number;
  shortage: number;
  isCritical: boolean;
}

export interface LowStockItem {
  productId: number;
  productName: string;
  productSku?: string;
  categoryName?: string;
  totalQuantity: number;
  totalReorderLevel: number;
  shortage: number;
  averageCost?: number;
  estimatedRestockCost?: number;
  branchDetails: BranchLowStockDetail[];
}

export interface BranchLowStockStats {
  branchId: number;
  branchName: string;
  lowStockCount: number;
  criticalCount: number;
  estimatedRestockValue: number;
}

export interface LowStockSummaryReport {
  totalLowStockItems: number;
  affectedBranches: number;
  criticalItems: number;
  estimatedRestockValue: number;
  items: LowStockItem[];
  branchStats: BranchLowStockStats[];
}
