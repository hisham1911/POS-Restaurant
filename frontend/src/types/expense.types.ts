// Expense Status Enum
export type ExpenseStatus = "Draft" | "Approved" | "Paid" | "Rejected";

// Expense Category Interface
export interface ExpenseCategory {
  id: number;
  name: string;
  nameEn?: string;
  description?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

// Expense Attachment Interface
export interface ExpenseAttachment {
  id: number;
  expenseId: number;
  fileName: string;
  fileUrl: string;
  fileSize: number;
  contentType: string;
  uploadedAt: string;
}

// Expense Interface
export interface Expense {
  id: number;
  expenseNumber: string;
  categoryId: number;
  categoryName: string;
  categoryIcon?: string;
  categoryColor?: string;
  amount: number;
  description: string;
  expenseDate: string;
  status: ExpenseStatus;
  notes?: string;
  referenceNumber?: string;
  beneficiary?: string;
  vendorName?: string;
  receiptNumber?: string;
  branchName?: string;
  paymentMethod?: string;
  paymentDate?: string;
  paymentReferenceNumber?: string;
  shiftId?: number;
  shiftNumber?: string;
  // Approval fields
  approvedByUserName?: string;
  approvedAt?: string;
  rejectionReason?: string;
  rejectedByUserName?: string;
  rejectedAt?: string;
  // Payment fields
  paidByUserName?: string;
  paidAt?: string;
  // Audit fields
  createdByUserName: string;
  createdAt: string;
  updatedAt: string;
  // Attachments
  attachments: ExpenseAttachment[];
}

// Request DTOs
export interface CreateExpenseRequest {
  categoryId: number;
  amount: number;
  description: string;
  expenseDate: string;
  notes?: string;
  referenceNumber?: string;
  beneficiary?: string;
}

export interface UpdateExpenseRequest {
  categoryId: number;
  amount: number;
  description: string;
  expenseDate: string;
  notes?: string;
  referenceNumber?: string;
  beneficiary?: string;
}

export interface ApproveExpenseRequest {
  notes?: string;
}

export interface RejectExpenseRequest {
  reason: string;
}

export interface PayExpenseRequest {
  paymentMethod: "Cash" | "Card" | "Fawry";
  paymentReferenceNumber?: string;
  notes?: string;
}

// Expense Category Request DTOs
export interface CreateExpenseCategoryRequest {
  name: string;
  nameEn?: string;
  description?: string;
}

export interface UpdateExpenseCategoryRequest {
  name: string;
  nameEn?: string;
  description?: string;
  isActive: boolean;
}

// Filters
export interface ExpenseFilters {
  categoryId?: number;
  status?: ExpenseStatus;
  fromDate?: string;
  toDate?: string;
  branchId?: number;
  pageNumber?: number;
  pageSize?: number;
}

export interface PagedExpensesResult {
  items: Expense[];
  totalCount: number;
  totalAmount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
