// نوع المنتج
export enum ProductType {
  Physical = 1,    // منتج مادي - يتتبع المخزون
  Service = 2,     // خدمة - لا يتتبع المخزون
  RawMaterial = 3, // مادة خام - لا يظهر في POS
  Manufactured = 4, // منتج مصنع - له وصفة
}

// وحدة القياس
export enum UnitOfMeasure {
  Piece = 1,
  Kilogram = 2,
  Gram = 3,
  Liter = 4,
  Milliliter = 5,
  Meter = 6,
  Box = 7,
  Portion = 8,
}

export interface Product {
  id: number;
  name: string;
  nameEn?: string;
  description?: string;
  sku?: string;
  barcode?: string;
  price: number;
  suggestedPrice: number; // السعر المقترح - من الباتش إذا كان المنتج له باتشات، وإلا السعر الأساسي
  cost?: number;
  taxRate?: number;
  // Legacy compatibility field. Prices are always treated as before-tax prices.
  taxInclusive: boolean;
  imageUrl?: string;
  isActive: boolean;
  categoryId: number;
  categoryName?: string;
  // نوع المنتج
  type: ProductType;
  unit: UnitOfMeasure;
  // يتم تحديده تلقائياً بناءً على النوع
  trackInventory: boolean;
  // تتبع الدفعات (Batch/Expiry/FEFO)
  isBatchTracked: boolean;
  /** @deprecated Use BranchInventory endpoint instead. Will be removed next sprint. */
  // الكمية المتاحة في الفرع الحالي (من جدول BranchInventories)
  currentBranchStock?: number;
  lowStockThreshold?: number;
  reorderPoint?: number;
  lastStockUpdate?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateProductRequest {
  name: string;
  nameEn?: string;
  description?: string;
  sku?: string;
  barcode?: string;
  price: number;
  cost?: number;
  taxRate?: number;
  // Legacy compatibility field. Sent as false and ignored by the backend.
  taxInclusive?: boolean;
  imageUrl?: string;
  categoryId: number;
  // نوع المنتج
  type?: ProductType;
  unit?: UnitOfMeasure;
  // تتبع الدفعات (Batch/Expiry/FEFO)
  isBatchTracked?: boolean;
  // الكمية الأولية للفرع الحالي (تُحفظ في BranchInventories)
  initialBranchStock?: number;
  lowStockThreshold?: number;
  reorderPoint?: number;
  branchStockQuantities?: Record<number, number>;
}

export interface UpdateProductRequest {
  name: string;
  nameEn?: string;
  description?: string;
  sku?: string;
  barcode?: string;
  price: number;
  cost?: number;
  taxRate?: number;
  // Legacy compatibility field. Sent as false and ignored by the backend.
  taxInclusive?: boolean;
  imageUrl?: string;
  categoryId: number;
  // نوع المنتج
  type?: ProductType;
  unit?: UnitOfMeasure;
  // تتبع الدفعات (Batch/Expiry/FEFO)
  isBatchTracked?: boolean;
  /** @deprecated Use BranchInventory endpoint instead. Will be removed next sprint. */
  // الكمية في الفرع الحالي (تُحدّث في BranchInventories)
  lowStockThreshold?: number;
  reorderPoint?: number;
  isActive: boolean;
}

export interface ProductsQueryParams {
  categoryId?: number;
  search?: string;
  isActive?: boolean;
  lowStock?: boolean;
  page?: number;
  pageSize?: number;
}

export interface QuickCreateProductRequest {
  name: string;
  price: number;
  categoryId: number;
  imageUrl?: string;
  // نوع المنتج - افتراضياً خدمة للإنشاء السريع
  type?: ProductType;
  unit?: UnitOfMeasure;
  // الكمية الأولية (تُحفظ في BranchInventories للفرع الحالي)
  initialStock?: number;
  sku?: string;
  barcode?: string;
}
