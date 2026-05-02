// Product Batch / Expiry Types (FEFO)

export type BatchStatus = 'Active' | 'Expired' | 'Depleted' | 'OnHold';

export interface ProductBatch {
  id: number;
  batchNumber?: string;
  productId: number;
  productName: string;
  quantity: number;
  initialQuantity: number;
  expiryDate?: string;
  purchaseDate: string;
  productionDate?: string;
  costPrice?: number;
  sellingPrice?: number; // ✅ NEW - Batch-specific selling price
  supplierName?: string;
  status: BatchStatus;
  notes?: string;
  daysUntilExpiry?: number;
  branchId?: number;
  branchName?: string;
  isRecommended?: boolean; // ✅ NEW - For FEFO UI hint (first batch)
}

export interface BatchExpiryAlert {
  id: number;
  batchNumber?: string;
  productId: number;
  productName: string;
  quantity: number;
  expiryDate?: string;
  daysUntilExpiry?: number;
  alertLevel: 'critical' | 'warning' | 'info';
}

export interface BatchExpirySummary {
  totalBatches: number;
  expiredBatches: number;
  nearExpiryBatches: number;
  alerts: BatchExpiryAlert[];
}

export interface CreateProductBatchRequest {
  productId: number;
  batchNumber?: string;
  quantity: number;
  expiryDate?: string;
  productionDate?: string;
  costPrice?: number;
  sellingPrice?: number; // ✅ NEW - Batch-specific selling price
  supplierName?: string;
  notes?: string;
}

export interface UpdateProductBatchRequest {
  batchNumber?: string;
  expiryDate?: string;
  productionDate?: string;
  sellingPrice?: number; // ✅ NEW - Batch-specific selling price
  notes?: string;
}

export interface HoldBatchRequest {
  reason: string;
}

export interface ProductBatchFilters {
  productId?: number;
  branchId?: number;
  status?: BatchStatus;
  page?: number;
  pageSize?: number;
}
