export type StockTakingStatus = "InProgress" | "Completed" | "Cancelled";
export type StockTakingType = "Full" | "Partial";

export interface StockTaking {
  id: number;
  stockTakingNumber: string;
  type: StockTakingType;
  typeName: string;
  categoryId?: number;
  categoryName?: string;
  status: StockTakingStatus;
  statusName: string;
  startedAt: string;
  completedAt?: string;
  createdByUserId: number;
  createdByUserName?: string;
  completedByUserId?: number;
  completedByUserName?: string;
  notes?: string;
  itemCount: number;
  totalDifference: number;
  items: StockTakingItem[];
}

export interface StockTakingItem {
  id: number;
  productId: number;
  productName: string;
  productSku?: string;
  systemQuantity: number;
  actualQuantity: number;
  difference: number;
  reason?: string;
  batchId?: number;
  batchNumber?: string;
}

export interface CreateStockTakingRequest {
  type: StockTakingType;
  categoryId?: number;
  notes?: string;
}

export interface UpsertStockTakingItemRequest {
  productId: number;
  actualQuantity: number;
  reason?: string;
  batchId?: number;
}

export interface CompleteStockTakingRequest {
  applyAdjustments?: boolean;
  notes?: string;
}

export interface StockTakingPagedResult {
  items: StockTaking[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface StockTakingReviewSummary {
  totalItems: number;
  positiveDiffCount: number;
  negativeDiffCount: number;
  zeroDiffCount: number;
  totalPositiveDiff: number;
  totalNegativeDiff: number;
  largeDifferences: StockTakingItem[];
}
