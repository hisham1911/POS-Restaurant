import { baseApi } from "./baseApi";
import type { ApiResponse, PagedResult } from "../types/api.types";
import type {
  ProductBatch,
  BatchExpirySummary,
  CreateProductBatchRequest,
  ProductBatchFilters,
  UpdateProductBatchRequest,
  HoldBatchRequest,
} from "../types/productBatch.types";

export const productBatchApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getProductBatches: builder.query<
      ApiResponse<PagedResult<ProductBatch>>,
      ProductBatchFilters | void
    >({
      query: (filters) => {
        const params = new URLSearchParams();
        if (filters) {
          if (filters.productId) {
            params.append("productId", filters.productId.toString());
          }
          if (filters.branchId) {
            params.append("branchId", filters.branchId.toString());
          }
          if (filters.status) params.append("status", filters.status);
          if (filters.page) params.append("page", filters.page.toString());
          if (filters.pageSize) {
            params.append("pageSize", filters.pageSize.toString());
          }
        }
        return { url: `/productbatches?${params.toString()}`, method: "GET" };
      },
      providesTags: (result) =>
        result?.data?.items
          ? [
              ...result.data.items.map(({ id }) => ({
                type: "ProductBatch" as const,
                id,
              })),
              { type: "ProductBatch", id: "LIST" },
            ]
          : [{ type: "ProductBatch", id: "LIST" }],
    }),

    getProductBatchById: builder.query<ApiResponse<ProductBatch>, number>({
      query: (id) => ({ url: `/productbatches/${id}`, method: "GET" }),
      providesTags: (_result, _error, id) => [{ type: "ProductBatch", id }],
    }),

    getBatchesByProduct: builder.query<
      ApiResponse<ProductBatch[]>,
      { productId: number; branchId?: number }
    >({
      query: ({ productId, branchId }) => {
        const params = branchId ? `?branchId=${branchId}` : "";
        return {
          url: `/productbatches/product/${productId}${params}`,
          method: "GET",
        };
      },
      providesTags: (_result, _error, { productId }) => [
        { type: "ProductBatch", id: `PRODUCT-${productId}` },
      ],
    }),

    getAvailableBatches: builder.query<
      ApiResponse<ProductBatch[]>,
      { productId: number; branchId: number }
    >({
      query: ({ productId, branchId }) => ({
        url: "/productbatches/available",
        method: "GET",
        params: { productId, branchId },
      }),
      providesTags: (_result, _error, { productId }) => [
        { type: "ProductBatch", id: `AVAILABLE-${productId}` },
      ],
    }),

    getExpiryAlerts: builder.query<ApiResponse<BatchExpirySummary>, number | void>(
      {
        query: (branchId) => {
          const params = branchId ? `?branchId=${branchId}` : "";
          return { url: `/productbatches/alerts/expiry${params}`, method: "GET" };
        },
        providesTags: [{ type: "ProductBatch", id: "ALERTS" }],
      },
    ),

    createProductBatch: builder.mutation<
      ApiResponse<ProductBatch>,
      CreateProductBatchRequest
    >({
      query: (data) => ({ url: "/productbatches", method: "POST", body: data }),
      invalidatesTags: (_result, _error, data) => [
        { type: "ProductBatch", id: "LIST" },
        { type: "ProductBatch", id: "ALERTS" },
        { type: "ProductBatch", id: `PRODUCT-${data.productId}` },
        { type: "ProductBatch", id: `AVAILABLE-${data.productId}` },
      ],
    }),

    updateProductBatch: builder.mutation<
      ApiResponse<ProductBatch>,
      { id: number; data: UpdateProductBatchRequest }
    >({
      query: ({ id, data }) => ({
        url: `/productbatches/${id}`,
        method: "PUT",
        body: data,
      }),
      invalidatesTags: (result, _error, { id }) => [
        { type: "ProductBatch", id },
        { type: "ProductBatch", id: "LIST" },
        { type: "ProductBatch", id: "ALERTS" },
        ...(result?.data?.productId
          ? [
              { type: "ProductBatch" as const, id: `PRODUCT-${result.data.productId}` },
              { type: "ProductBatch" as const, id: `AVAILABLE-${result.data.productId}` },
            ]
          : []),
      ],
    }),

    holdBatch: builder.mutation<
      ApiResponse<ProductBatch>,
      { id: number; data: HoldBatchRequest }
    >({
      query: ({ id, data }) => ({
        url: `/productbatches/${id}/hold`,
        method: "PATCH",
        body: data,
      }),
      invalidatesTags: (result, _error, { id }) => [
        { type: "ProductBatch", id },
        { type: "ProductBatch", id: "LIST" },
        { type: "ProductBatch", id: "ALERTS" },
        ...(result?.data?.productId
          ? [
              { type: "ProductBatch" as const, id: `PRODUCT-${result.data.productId}` },
              { type: "ProductBatch" as const, id: `AVAILABLE-${result.data.productId}` },
            ]
          : []),
      ],
    }),

    releaseBatch: builder.mutation<
      ApiResponse<ProductBatch>,
      { id: number; data: HoldBatchRequest }
    >({
      query: ({ id, data }) => ({
        url: `/productbatches/${id}/release`,
        method: "PATCH",
        body: data,
      }),
      invalidatesTags: (result, _error, { id }) => [
        { type: "ProductBatch", id },
        { type: "ProductBatch", id: "LIST" },
        { type: "ProductBatch", id: "ALERTS" },
        ...(result?.data?.productId
          ? [
              { type: "ProductBatch" as const, id: `PRODUCT-${result.data.productId}` },
              { type: "ProductBatch" as const, id: `AVAILABLE-${result.data.productId}` },
            ]
          : []),
      ],
    }),

    deleteProductBatch: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({ url: `/productbatches/${id}`, method: "DELETE" }),
      invalidatesTags: (_result, _error, id) => [
        { type: "ProductBatch", id },
        { type: "ProductBatch", id: "LIST" },
        { type: "ProductBatch", id: "ALERTS" },
      ],
    }),
  }),
});

export const {
  useGetProductBatchesQuery,
  useGetProductBatchByIdQuery,
  useGetBatchesByProductQuery,
  useGetAvailableBatchesQuery,
  useGetExpiryAlertsQuery,
  useCreateProductBatchMutation,
  useUpdateProductBatchMutation,
  useHoldBatchMutation,
  useReleaseBatchMutation,
  useDeleteProductBatchMutation,
} = productBatchApi;
