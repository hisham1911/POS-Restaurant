/**
 * Customer Types - Matches backend CustomerDto
 */

export interface Customer {
  id: number;
  phone: string;
  name?: string;
  email?: string;
  address?: string;
  notes?: string;
  loyaltyPoints: number;
  totalOrders: number;
  totalSpent: number;
  lastOrderAt?: string;
  isActive: boolean;
  createdAt: string;
  // Credit Sales Fields
  totalDue: number;
  creditLimit: number;
  // Concurrency Token (for optimistic locking)
  rowVersion?: string;
}

export interface CustomerSummary {
  id: number;
  phone: string;
  name?: string;
  loyaltyPoints: number;
  totalDue: number;
  creditLimit: number;
}

export interface CreateCustomerRequest {
  phone: string;
  name?: string;
  email?: string;
  address?: string;
  notes?: string;
}

export interface UpdateCustomerRequest {
  name?: string;
  email?: string;
  address?: string;
  notes?: string;
  isActive?: boolean;
  creditLimit?: number;
  // Concurrency Token (must be included for updates)
  rowVersion?: string;
}

export interface GetOrCreateCustomerRequest {
  phone: string;
  name?: string;
}

export interface GetOrCreateCustomerResponse {
  success: boolean;
  data: Customer;
  wasCreated: boolean;
  message?: string;
}

export interface LoyaltyPointsRequest {
  points: number;
}

export interface CustomersPagedResult {
  items: Customer[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface CustomersQueryParams {
  page?: number;
  pageSize?: number;
  search?: string;
}

// Debt Payment Types
export interface PayDebtRequest {
  amount: number;
  paymentMethod: 'Cash' | 'Card' | 'BankTransfer' | 'Fawry';
  referenceNumber?: string;
  notes?: string;
}

export interface PayDebtResponse {
  paymentId: number;
  amountPaid: number;
  balanceBefore: number;
  balanceAfter: number;
  remainingDebt: number;
  message: string;
}

export interface DebtPaymentDto {
  id: number;
  customerId: number;
  amount: number;
  paymentMethod: 'Cash' | 'Card' | 'BankTransfer' | 'Fawry';
  referenceNumber?: string;
  notes?: string;
  recordedByUserId: number;
  recordedByUserName?: string;
  balanceBefore: number;
  balanceAfter: number;
  createdAt: string;
}
