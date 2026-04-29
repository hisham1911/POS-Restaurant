import { baseApi } from './baseApi';
import type { ApiResponse } from '../types/api.types';
import type {
  ProductBatch,
  BatchExpirySummary,
  CreateProductBatchRequest,
  ProductBatchFilters,
} from '../types/productBatch.types';

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export const productBatchApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getProductBatches: builder.query<
      ApiResponse<PagedResult<ProductBatch>>,
      ProductBatchFilters | void
    >({
      query: (filters) => {
        const params = new URLSearchParams();
        if (filters) {
          if (filters.productId) params.append('productId', filters.productId.toString());
          if (filters.branchId) params.append('branchId', filters.branchId.toString());
          if (filters.status) params.append('status', filters.status);
          if (filters.page) params.append('page', filters.page.toString());
          if (filters.pageSize) params.append('pageSize', filters.pageSize.toString());
        }
        return { url: `/productbatches?${params.toString()}`, method: 'GET' };
      },
      providesTags: (result) =>
        result?.data?.items
          ? [
              ...result.data.items.map(({ id }) => ({ type: 'ProductBatch' as const, id })),
              { type: 'ProductBatch', id: 'LIST' },
            ]
          : [{ type: 'ProductBatch', id: 'LIST' }],
    }),

    getProductBatchById: builder.query<ApiResponse<ProductBatch>, number>({
      query: (id) => ({ url: `/productbatches/${id}`, method: 'GET' }),
      providesTags: (result, error, id) => [{ type: 'ProductBatch', id }],
    }),

    getBatchesByProduct: builder.query<ApiResponse<ProductBatch[]>, { productId: number; branchId?: number }>({
      query: ({ productId, branchId }) => {
        const params = branchId ? `?branchId=${branchId}` : '';
        return { url: `/productbatches/product/${productId}${params}`, method: 'GET' };
      },
      providesTags: (result, error, { productId }) => [
        { type: 'ProductBatch', id: `PRODUCT-${productId}` },
      ],
    }),

    getExpiryAlerts: builder.query<ApiResponse<BatchExpirySummary>, number | void>({
      query: (branchId) => {
        const params = branchId ? `?branchId=${branchId}` : '';
        return { url: `/productbatches/alerts/expiry${params}`, method: 'GET' };
      },
      providesTags: [{ type: 'ProductBatch', id: 'ALERTS' }],
    }),

    createProductBatch: builder.mutation<ApiResponse<ProductBatch>, CreateProductBatchRequest>({
      query: (data) => ({ url: '/productbatches', method: 'POST', body: data }),
      invalidatesTags: [{ type: 'ProductBatch', id: 'LIST' }],
    }),

    deleteProductBatch: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({ url: `/productbatches/${id}`, method: 'DELETE' }),
      invalidatesTags: (result, error, id) => [
        { type: 'ProductBatch', id },
        { type: 'ProductBatch', id: 'LIST' },
      ],
    }),
  }),
});

export const {
  useGetProductBatchesQuery,
  useGetProductBatchByIdQuery,
  useGetBatchesByProductQuery,
  useGetExpiryAlertsQuery,
  useCreateProductBatchMutation,
  useDeleteProductBatchMutation,
} = productBatchApi;
