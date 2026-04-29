// Product Batch / Expiry Types (FEFO)

export type BatchStatus = 'Active' | 'Expired' | 'Depleted';

export interface ProductBatch {
  id: number;
  batchNumber: string;
  productId: number;
  productName: string;
  quantity: number;
  initialQuantity: number;
  expiryDate: string;
  purchaseDate: string;
  productionDate?: string;
  costPrice?: number;
  supplierName?: string;
  status: BatchStatus;
  notes?: string;
  daysUntilExpiry: number;
  branchId?: number;
  branchName?: string;
}

export interface BatchExpiryAlert {
  id: number;
  batchNumber: string;
  productId: number;
  productName: string;
  quantity: number;
  expiryDate: string;
  daysUntilExpiry: number;
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
  batchNumber: string;
  quantity: number;
  expiryDate: string;
  productionDate?: string;
  costPrice?: number;
  supplierName?: string;
  notes?: string;
}

export interface ProductBatchFilters {
  productId?: number;
  branchId?: number;
  status?: BatchStatus;
  page?: number;
  pageSize?: number;
}
