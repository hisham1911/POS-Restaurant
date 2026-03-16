export type OrderStatus =
  | "Draft"
  | "Pending"
  | "Completed"
  | "Cancelled"
  | "Refunded"
  | "PartiallyRefunded";

export type OrderType = "DineIn" | "Takeaway" | "Delivery" | "Return";

export type PaymentMethod = "Cash" | "Card" | "Fawry" | "BankTransfer";

// Query parameters for filtering orders
export interface OrdersQueryParams {
  status?: OrderStatus;
  fromDate?: string; // ISO date string
  toDate?: string; // ISO date string
  page?: number;
  pageSize?: number;
}

// Paged result for orders
export interface PagedOrders {
  items: Order[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

// Partial refund item request
export interface RefundItemRequest {
  itemId: number;
  quantity: number;
  reason?: string;
}

export interface OrderItem {
  id: number;
  // ProductId يمكن أن يكون null للمنتجات المخصصة
  productId: number | null;
  // هل هذا منتج مخصص (ليس من الكتالوج)
  isCustomItem?: boolean;
  // Product Snapshot
  productName: string;
  productNameEn?: string;
  productSku?: string;
  productBarcode?: string;
  // Price Snapshot
  unitPrice: number;
  originalPrice: number;
  quantity: number;
  refundedQuantity: number;
  // Discount
  discountType?: string;
  discountValue?: number;
  discountAmount: number;
  discountReason?: string;
  // Tax
  taxRate: number;
  taxAmount: number;
  taxInclusive: boolean;
  subtotal: number;
  total: number;
  notes?: string;
}

export interface Order {
  id: number;
  orderNumber: string;
  status: OrderStatus;
  orderType?: OrderType;
  // Concurrency Token (for optimistic locking)
  rowVersion?: string;
  // Branch Snapshot
  branchId: number;
  branchName?: string;
  branchAddress?: string;
  branchPhone?: string;
  // Currency
  currencyCode: string;
  // Totals
  subtotal: number;
  discountType?: string;
  discountValue?: number;
  discountAmount: number;
  discountCode?: string;
  taxRate: number;
  taxAmount: number;
  serviceChargePercent: number;
  serviceChargeAmount: number;
  total: number;
  amountPaid: number;
  amountDue: number;
  changeAmount: number;
  // Customer
  customerName?: string;
  customerPhone?: string;
  customerId?: number;
  notes?: string;
  // User
  userId: number;
  userName?: string;
  // Timestamps
  createdAt: string;
  completedAt?: string;
  cancelledAt?: string;
  cancellationReason?: string;
  // Refund Information
  refundedAt?: string;
  refundReason?: string;
  refundAmount: number;
  refundedByUserId?: number;
  refundedByUserName?: string;
  originalOrderId?: number;
  // Shift
  shiftId?: number;
  items: OrderItem[];
  payments: Payment[];
}

export interface Payment {
  id: number;
  method: PaymentMethod;
  amount: number;
  reference?: string;
}

export interface CreateOrderRequest {
  orderType?: OrderType;
  items: {
    productId: number;
    quantity: number;
    notes?: string;
    // Item-level discount
    discountType?: "percentage" | "fixed";
    discountValue?: number;
    discountReason?: string;
  }[];
  customerName?: string;
  customerPhone?: string;
  customerId?: number;
  notes?: string;
  // Order-level discount
  discountType?: "Percentage" | "Fixed";
  discountValue?: number;
}

export interface CompleteOrderRequest {
  payments: {
    method: PaymentMethod;
    amount: number;
    reference?: string;
  }[];
}

// طلب إضافة منتج مخصص (ليس من الكتالوج)
export interface AddCustomItemRequest {
  name: string;
  unitPrice: number;
  quantity?: number;
  taxRate?: number;
  notes?: string;
}
