export interface Supplier {
  id: number;
  name: string;
  nameEn?: string;
  phone?: string;
  email?: string;
  address?: string;
  taxNumber?: string;
  contactPerson?: string;
  notes?: string;
  isActive: boolean;
  createdAt: string;
  totalDue: number;
  totalPaid: number;
  totalPurchases: number;
  lastPurchaseDate?: string;
}

export interface CreateSupplierRequest {
  name: string;
  nameEn?: string;
  phone?: string;
  email?: string;
  address?: string;
  taxNumber?: string;
  contactPerson?: string;
  notes?: string;
}

export interface UpdateSupplierRequest extends CreateSupplierRequest {
  isActive: boolean;
}
