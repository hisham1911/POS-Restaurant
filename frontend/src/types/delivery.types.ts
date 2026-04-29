export interface DeliveryPerson {
  id: number;
  name: string;
  phone: string;
  vehicleInfo?: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateDeliveryPersonRequest {
  name: string;
  phone: string;
  vehicleInfo?: string;
}

export interface UpdateDeliveryPersonRequest {
  name: string;
  phone: string;
  vehicleInfo?: string;
  isActive: boolean;
}

export interface AssignDeliveryRequest {
  deliveryPersonId: number;
  deliveryNotes?: string;
}

export interface UpdateDeliveryStatusRequest {
  deliveryStatus: string; // PendingAssignment, Assigned, OutForDelivery, Delivered, Cancelled
  deliveryNotes?: string;
}

export interface DeliveryPersonFilters {
  page?: number;
  pageSize?: number;
  search?: string;
}

export interface DeliveryOrderFilters {
  page?: number;
  pageSize?: number;
  status?: string;
  deliveryPersonId?: number;
  date?: string;
}

export interface DeliveryOrderDto {
  id: number;
  orderNumber: string;
  status: string;
  orderType: string;
  customerName?: string;
  customerPhone?: string;
  deliveryAddress?: string;
  deliveryFee: number;
  deliveryStatus?: string;
  deliveryNotes?: string;
  deliveryPersonId?: number;
  deliveryPersonName?: string;
  assignedAt?: string;
  deliveredAt?: string;
  total: number;
  createdAt: string;
}

export interface PagedDeliveryPersons {
  items: DeliveryPerson[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
