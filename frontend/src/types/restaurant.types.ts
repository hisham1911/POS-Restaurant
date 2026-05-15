export type RestaurantTableStatus = "Available" | "Occupied" | 0 | 1;

export interface RestaurantTable {
  id: number;
  tenantId: number;
  branchId: number;
  number: string;
  sortOrder: number;
  status: RestaurantTableStatus;
  isActive: boolean;
  openOrderId?: number | null;
  openOrderNumber?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateRestaurantTableRequest {
  branchId: number;
  number: string;
  sortOrder: number;
}

export interface UpdateRestaurantTableRequest {
  number: string;
  sortOrder: number;
  isActive: boolean;
}

export interface SavedOrderNote {
  id: number;
  tenantId: number;
  branchId: number;
  text: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateSavedOrderNoteRequest {
  branchId: number;
  text: string;
  sortOrder: number;
}

export interface UpdateSavedOrderNoteRequest {
  text: string;
  sortOrder: number;
  isActive: boolean;
}
