import { baseApi } from "./baseApi";
import { ApiResponse } from "../types/api.types";
import {
  StockTaking,
  StockTakingItem,
  StockTakingPagedResult,
  CreateStockTakingRequest,
  UpsertStockTakingItemRequest,
  CompleteStockTakingRequest,
} from "../types/stockTaking.types";

export const stockTakingApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getStockTakings: builder.query<
      ApiResponse<StockTakingPagedResult>,
      { page?: number; pageSize?: number; status?: string } | void
    >({
      query: (params) => ({
        url: "stocktaking",
        params: params ?? {},
      }),
      providesTags: (result) =>
        result?.data?.items
          ? [
              ...result.data.items.map(({ id }) => ({ type: "StockTaking" as const, id })),
              { type: "StockTaking", id: "LIST" },
            ]
          : [{ type: "StockTaking", id: "LIST" }],
    }),

    getStockTakingById: builder.query<ApiResponse<StockTaking>, number>({
      query: (id) => `stocktaking/${id}`,
      providesTags: (_result, _error, id) => [{ type: "StockTaking", id }],
    }),

    createStockTaking: builder.mutation<ApiResponse<StockTaking>, CreateStockTakingRequest>({
      query: (body) => ({
        url: "stocktaking",
        method: "POST",
        body,
      }),
      invalidatesTags: [{ type: "StockTaking", id: "LIST" }],
    }),

    upsertStockTakingItem: builder.mutation<
      ApiResponse<StockTakingItem>,
      { stockTakingId: number; body: UpsertStockTakingItemRequest }
    >({
      query: ({ stockTakingId, body }) => ({
        url: `stocktaking/${stockTakingId}/items`,
        method: "POST",
        body,
      }),
      invalidatesTags: (_result, _error, { stockTakingId }) => [
        { type: "StockTaking", id: stockTakingId },
      ],
    }),

    removeStockTakingItem: builder.mutation<
      ApiResponse<boolean>,
      { stockTakingId: number; itemId: number }
    >({
      query: ({ stockTakingId, itemId }) => ({
        url: `stocktaking/${stockTakingId}/items/${itemId}`,
        method: "DELETE",
      }),
      invalidatesTags: (_result, _error, { stockTakingId }) => [
        { type: "StockTaking", id: stockTakingId },
      ],
    }),

    completeStockTaking: builder.mutation<
      ApiResponse<StockTaking>,
      { id: number; body: CompleteStockTakingRequest }
    >({
      query: ({ id, body }) => ({
        url: `stocktaking/${id}/complete`,
        method: "POST",
        body,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: "StockTaking", id },
        { type: "StockTaking", id: "LIST" },
        { type: "StockTaking", id: "LATEST" },
        { type: "Inventory", id: "LIST" },
      ],
    }),

    cancelStockTaking: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `stocktaking/${id}/cancel`,
        method: "POST",
      }),
      invalidatesTags: (_result, _error, id) => [
        { type: "StockTaking", id },
        { type: "StockTaking", id: "LIST" },
      ],
    }),

    getLatestCompletedStockTaking: builder.query<ApiResponse<StockTaking | null>, void>({
      query: () => `stocktaking/latest-completed`,
      providesTags: [{ type: "StockTaking", id: "LATEST" }],
    }),
  }),
});

export const {
  useGetStockTakingsQuery,
  useGetStockTakingByIdQuery,
  useCreateStockTakingMutation,
  useUpsertStockTakingItemMutation,
  useRemoveStockTakingItemMutation,
  useCompleteStockTakingMutation,
  useCancelStockTakingMutation,
  useGetLatestCompletedStockTakingQuery,
} = stockTakingApi;
