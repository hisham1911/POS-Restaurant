import { baseApi } from './baseApi';
import type {
  PurchaseInvoice,
  PurchaseInvoicePreview,
  CreatePurchaseInvoiceRequest,
  UpdatePurchaseInvoiceRequest,
  AddPaymentRequest,
  CancelInvoiceRequest,
  PurchaseInvoicePayment,
  PurchaseInvoiceFilters,
} from '../types/purchaseInvoice.types';
import type { ApiResponse } from '../types/api.types';

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  totalAmount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export const purchaseInvoiceApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // Get all purchase invoices with filters and pagination
    getPurchaseInvoices: builder.query<
      ApiResponse<PagedResult<PurchaseInvoice>>,
      PurchaseInvoiceFilters | void
    >({
      query: (filters) => {
        const params = new URLSearchParams();
        
        if (filters) {
          if (filters.supplierId) params.append('supplierId', filters.supplierId.toString());
          if (filters.status) params.append('status', filters.status);
          if (filters.fromDate) params.append('fromDate', filters.fromDate);
          if (filters.toDate) params.append('toDate', filters.toDate);
          if (filters.pageNumber) params.append('pageNumber', filters.pageNumber.toString());
          if (filters.pageSize) params.append('pageSize', filters.pageSize.toString());
        }
        
        return {
          url: `/purchaseinvoices?${params.toString()}`,
          method: 'GET',
        };
      },
      providesTags: (result) =>
        result?.data?.items
          ? [
              ...result.data.items.map(({ id }) => ({ type: 'PurchaseInvoice' as const, id })),
              { type: 'PurchaseInvoice', id: 'LIST' },
            ]
          : [{ type: 'PurchaseInvoice', id: 'LIST' }],
    }),

    preparePurchaseInvoice: builder.mutation<
      ApiResponse<PurchaseInvoicePreview>,
      CreatePurchaseInvoiceRequest
    >({
      query: (data) => ({
        url: '/purchaseinvoices/prepare',
        method: 'POST',
        body: data,
      }),
    }),

    // Get purchase invoice by ID
    getPurchaseInvoiceById: builder.query<ApiResponse<PurchaseInvoice>, number>({
      query: (id) => ({
        url: `/purchaseinvoices/${id}`,
        method: 'GET',
      }),
      providesTags: (result, error, id) => [{ type: 'PurchaseInvoice', id }],
    }),

    // Create new purchase invoice
    createPurchaseInvoice: builder.mutation<
      ApiResponse<PurchaseInvoice>,
      CreatePurchaseInvoiceRequest
    >({
      query: (data) => ({
        url: '/purchaseinvoices',
        method: 'POST',
        body: data,
      }),
      invalidatesTags: [{ type: 'PurchaseInvoice', id: 'LIST' }],
    }),

    // Update purchase invoice
    updatePurchaseInvoice: builder.mutation<
      ApiResponse<PurchaseInvoice>,
      { id: number; data: UpdatePurchaseInvoiceRequest }
    >({
      query: ({ id, data }) => ({
        url: `/purchaseinvoices/${id}`,
        method: 'PUT',
        body: data,
      }),
      invalidatesTags: (result, error, { id }) => [
        { type: 'PurchaseInvoice', id },
        { type: 'PurchaseInvoice', id: 'LIST' },
      ],
    }),

    // Delete purchase invoice
    deletePurchaseInvoice: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/purchaseinvoices/${id}`,
        method: 'DELETE',
      }),
      invalidatesTags: (result, error, id) => [
        { type: 'PurchaseInvoice', id },
        { type: 'PurchaseInvoice', id: 'LIST' },
      ],
    }),

    // Confirm purchase invoice
    confirmPurchaseInvoice: builder.mutation<ApiResponse<PurchaseInvoice>, number>({
      query: (id) => ({
        url: `/purchaseinvoices/${id}/confirm`,
        method: 'POST',
      }),
      invalidatesTags: (result, error, id) => [
        { type: 'PurchaseInvoice', id },
        { type: 'PurchaseInvoice', id: 'LIST' },
        { type: 'Products', id: 'LIST' }, // Invalidate products because inventory changed
      ],
    }),

    // Cancel purchase invoice
    cancelPurchaseInvoice: builder.mutation<
      ApiResponse<PurchaseInvoice>,
      { id: number; data: CancelInvoiceRequest }
    >({
      query: ({ id, data }) => ({
        url: `/purchaseinvoices/${id}/cancel`,
        method: 'POST',
        body: data,
      }),
      invalidatesTags: (result, error, { id }) => [
        { type: 'PurchaseInvoice', id },
        { type: 'PurchaseInvoice', id: 'LIST' },
        { type: 'Products', id: 'LIST' }, // Invalidate products if inventory adjusted
      ],
    }),

    // Add payment to purchase invoice
    addPayment: builder.mutation<
      ApiResponse<PurchaseInvoicePayment>,
      { invoiceId: number; payment: AddPaymentRequest }
    >({
      query: ({ invoiceId, payment }) => ({
        url: `/purchaseinvoices/${invoiceId}/payments`,
        method: 'POST',
        body: payment,
      }),
      invalidatesTags: (result, error, { invoiceId }) => [
        { type: 'PurchaseInvoice', id: invoiceId },
        { type: 'PurchaseInvoice', id: 'LIST' },
      ],
    }),

    // Delete payment from purchase invoice
    deletePayment: builder.mutation<
      ApiResponse<boolean>,
      { invoiceId: number; paymentId: number }
    >({
      query: ({ invoiceId, paymentId }) => ({
        url: `/purchaseinvoices/${invoiceId}/payments/${paymentId}`,
        method: 'DELETE',
      }),
      invalidatesTags: (result, error, { invoiceId }) => [
        { type: 'PurchaseInvoice', id: invoiceId },
        { type: 'PurchaseInvoice', id: 'LIST' },
      ],
    }),
  }),
});

export const {
  useGetPurchaseInvoicesQuery,
  usePreparePurchaseInvoiceMutation,
  useGetPurchaseInvoiceByIdQuery,
  useCreatePurchaseInvoiceMutation,
  useUpdatePurchaseInvoiceMutation,
  useDeletePurchaseInvoiceMutation,
  useConfirmPurchaseInvoiceMutation,
  useCancelPurchaseInvoiceMutation,
  useAddPaymentMutation,
  useDeletePaymentMutation,
} = purchaseInvoiceApi;
