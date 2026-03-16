// Purchase Invoice Types

export type PurchaseInvoiceStatus = 
  | 'Draft' 
  | 'Confirmed' 
  | 'Paid' 
  | 'PartiallyPaid' 
  | 'Cancelled' 
  | 'Returned' 
  | 'PartiallyReturned';

export type PaymentMethod = 'Cash' | 'Card' | 'Fawry';

export interface PurchaseInvoiceItem {
  id: number;
  productId: number;
  productName: string;
  productSku?: string;
  quantity: number;
  purchasePrice: number;
  sellingPrice: number;
  total: number;
  notes?: string;
}

export interface PurchaseInvoicePayment {
  id: number;
  amount: number;
  paymentDate: string;
  method: string;
  referenceNumber?: string;
  notes?: string;
  createdByUserName: string;
  createdAt: string;
}

export interface PurchaseInvoice {
  id: number;
  invoiceNumber: string;
  supplierId: number;
  supplierName: string;
  supplierPhone?: string;
  invoiceDate: string;
  status: string;
  subtotal: number;
  taxRate: number;
  taxAmount: number;
  total: number;
  amountPaid: number;
  amountDue: number;
  notes?: string;
  createdByUserName: string;
  confirmedByUserName?: string;
  confirmedAt?: string;
  createdAt: string;
  items: PurchaseInvoiceItem[];
  payments: PurchaseInvoicePayment[];
}

export interface CreatePurchaseInvoiceItemRequest {
  productId: number;
  quantity: number;
  purchasePrice: number;
  sellingPrice: number;
  notes?: string;
}

export interface CreatePurchaseInvoiceRequest {
  supplierId: number;
  invoiceDate: string;
  items: CreatePurchaseInvoiceItemRequest[];
  notes?: string;
}

export interface UpdatePurchaseInvoiceItemRequest {
  id?: number;
  productId: number;
  quantity: number;
  purchasePrice: number;
  sellingPrice: number;
  notes?: string;
}

export interface UpdatePurchaseInvoiceRequest {
  supplierId: number;
  invoiceDate: string;
  items: UpdatePurchaseInvoiceItemRequest[];
  notes?: string;
}

export interface AddPaymentRequest {
  amount: number;
  paymentDate: string;
  method: PaymentMethod;
  referenceNumber?: string;
  notes?: string;
}

export interface CancelInvoiceRequest {
  reason: string;
  adjustInventory: boolean;
}

export interface PurchaseInvoiceFilters {
  supplierId?: number;
  status?: PurchaseInvoiceStatus;
  fromDate?: string;
  toDate?: string;
  pageNumber?: number;
  pageSize?: number;
}
