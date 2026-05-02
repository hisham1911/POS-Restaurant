// Branch Inventory Types
export interface BranchInventory {
  id: number;
  branchId: number;
  branchName: string;
  productId: number;
  productName: string;
  productSku?: string;
  productBarcode?: string;
  quantity: number;
  batchAvailableQuantity?: number;
  reorderLevel: number;
  isLowStock: boolean;
  isBatchTracked: boolean;
  lastUpdatedAt: string;
}

export interface BranchInventorySummary {
  productId: number;
  productName: string;
  productSku?: string;
  totalQuantity: number;
  branchInventories: BranchInventory[];
}

// Inventory Transfer Types
export interface InventoryTransfer {
  id: number;
  transferNumber: string;
  fromBranchId: number;
  fromBranchName: string;
  toBranchId: number;
  toBranchName: string;
  productId: number;
  productName: string;
  productSku?: string;
  quantity: number;
  status: TransferStatus;
  reason: string;
  notes?: string;
  createdByUserName: string;
  createdAt: string;
  approvedByUserName?: string;
  approvedAt?: string;
  receivedByUserName?: string;
  receivedAt?: string;
  cancelledByUserName?: string;
  cancelledAt?: string;
  cancellationReason?: string;
}

export type TransferStatus = "Pending" | "Approved" | "Completed" | "Cancelled";

export interface CreateTransferRequest {
  fromBranchId: number;
  toBranchId: number;
  productId: number;
  quantity: number;
  reason: string;
  notes?: string;
}

export interface CancelTransferRequest {
  reason: string;
}

// Branch Product Price Types
export interface BranchProductPrice {
  id: number;
  branchId: number;
  branchName: string;
  productId: number;
  productName: string;
  price: number;
  defaultPrice: number;
  effectiveFrom: string;
  effectiveTo?: string;
  isActive: boolean;
}

export interface SetBranchPriceRequest {
  branchId: number;
  productId: number;
  price: number;
  effectiveFrom?: string;
}

// Stock Adjustment Types (single product)
export type StockAdjustmentType =
  | "Receiving"
  | "Damage"
  | "Adjustment"
  | "Transfer";

export interface AdjustProductStockRequest {
  quantity: number;
  reason: string;
  adjustmentType: StockAdjustmentType;
}

export interface StockAdjustResult {
  newBalance: number;
  previousBalance: number;
  change: number;
}

// Inventory Adjustment Types
export interface AdjustInventoryRequest {
  branchId: number;
  productId: number;
  quantityChange: number; // Can be positive or negative
  reason: string;
  notes?: string;
}

// Paginated Response
export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

// Query Params
export interface InventoryTransferQueryParams {
  fromBranchId?: number;
  toBranchId?: number;
  status?: TransferStatus;
  pageNumber?: number;
  pageSize?: number;
}
