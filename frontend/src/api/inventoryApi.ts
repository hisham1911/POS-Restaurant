import { baseApi } from "./baseApi";
import type { ApiResponse } from "../types/api.types";
import type {
  BranchInventory,
  BranchInventorySummary,
  InventoryTransfer,
  CreateTransferRequest,
  CancelTransferRequest,
  BranchProductPrice,
  SetBranchPriceRequest,
  AdjustInventoryRequest,
  AdjustProductStockRequest,
  StockAdjustResult,
  PaginatedResponse,
  InventoryTransferQueryParams,
} from "../types/inventory.types";

export const inventoryApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // Branch Inventory Queries
    getBranchInventory: builder.query<BranchInventory[], number>({
      query: (branchId) => `/inventory/branch/${branchId}`,
      transformResponse: (response: { data: BranchInventory[] }) =>
        response.data,
      providesTags: (result, error, branchId) => [
        { type: "Inventory", id: `BRANCH-${branchId}` },
        "Inventory",
      ],
    }),

    getProductInventoryAcrossBranches: builder.query<
      BranchInventorySummary,
      number
    >({
      query: (productId) => `/inventory/product/${productId}`,
      transformResponse: (response: { data: BranchInventorySummary }) =>
        response.data,
      providesTags: (result, error, productId) => [
        { type: "Inventory", id: `PRODUCT-${productId}` },
        "Inventory",
      ],
    }),

    getLowStockItems: builder.query<BranchInventory[], number | undefined>({
      query: (branchId) => ({
        url: "/inventory/low-stock",
        params: branchId ? { branchId } : undefined,
      }),
      transformResponse: (response: { data: BranchInventory[] }) =>
        response.data,
      providesTags: ["Inventory"],
    }),

    // Inventory Adjustments
    adjustInventory: builder.mutation<BranchInventory, AdjustInventoryRequest>({
      query: (request) => ({
        url: "/inventory/adjust",
        method: "POST",
        body: request,
      }),
      transformResponse: (response: { data: BranchInventory }) => response.data,
      invalidatesTags: (result, error, request) => [
        { type: "Inventory", id: `BRANCH-${request.branchId}` },
        { type: "Inventory", id: `PRODUCT-${request.productId}` },
        "Inventory",
        "Products",
      ],
    }),

    // Single Product Stock Adjustment (POS)
    adjustProductStock: builder.mutation<
      ApiResponse<StockAdjustResult>,
      { productId: number; data: AdjustProductStockRequest }
    >({
      query: ({ productId, data }) => ({
        url: `/products/${productId}/adjust-stock`,
        method: "POST",
        body: data,
      }),
      invalidatesTags: (result, error, { productId }) => [
        { type: "Products", id: productId },
        { type: "Products", id: "LIST" },
        "Inventory",
      ],
    }),

    // Inventory Transfers
    createTransfer: builder.mutation<InventoryTransfer, CreateTransferRequest>({
      query: (request) => ({
        url: "/inventory/transfer",
        method: "POST",
        body: request,
      }),
      transformResponse: (response: { data: InventoryTransfer }) =>
        response.data,
      invalidatesTags: ["Inventory"],
    }),

    getTransfers: builder.query<
      PaginatedResponse<InventoryTransfer>,
      InventoryTransferQueryParams
    >({
      query: (params) => ({
        url: "/inventory/transfer",
        params: {
          fromBranchId: params.fromBranchId,
          toBranchId: params.toBranchId,
          status: params.status,
          pageNumber: params.pageNumber || 1,
          pageSize: params.pageSize || 20,
        },
      }),
      transformResponse: (response: {
        data: PaginatedResponse<InventoryTransfer>;
      }) => response.data,
      providesTags: ["Inventory"],
    }),

    getTransferById: builder.query<InventoryTransfer, number>({
      query: (id) => `/inventory/transfer/${id}`,
      transformResponse: (response: { data: InventoryTransfer }) =>
        response.data,
      providesTags: (result, error, id) => [
        { type: "Inventory", id: `TRANSFER-${id}` },
      ],
    }),

    approveTransfer: builder.mutation<InventoryTransfer, number>({
      query: (id) => ({
        url: `/inventory/transfer/${id}/approve`,
        method: "POST",
      }),
      transformResponse: (response: { data: InventoryTransfer }) =>
        response.data,
      invalidatesTags: (result, error, id) => [
        { type: "Inventory", id: `TRANSFER-${id}` },
        "Inventory",
      ],
    }),

    receiveTransfer: builder.mutation<InventoryTransfer, number>({
      query: (id) => ({
        url: `/inventory/transfer/${id}/receive`,
        method: "POST",
      }),
      transformResponse: (response: { data: InventoryTransfer }) =>
        response.data,
      invalidatesTags: (result, error, id) => [
        { type: "Inventory", id: `TRANSFER-${id}` },
        "Inventory",
        "Products",
      ],
    }),

    cancelTransfer: builder.mutation<
      InventoryTransfer,
      { id: number; request: CancelTransferRequest }
    >({
      query: ({ id, request }) => ({
        url: `/inventory/transfer/${id}/cancel`,
        method: "POST",
        body: request,
      }),
      transformResponse: (response: { data: InventoryTransfer }) =>
        response.data,
      invalidatesTags: (result, error, { id }) => [
        { type: "Inventory", id: `TRANSFER-${id}` },
        "Inventory",
      ],
    }),

    // Branch Prices
    getBranchPrices: builder.query<BranchProductPrice[], number>({
      query: (branchId) => `/inventory/branch-prices/${branchId}`,
      transformResponse: (response: { data: BranchProductPrice[] }) =>
        response.data,
      providesTags: (result, error, branchId) => [
        { type: "Inventory", id: `PRICES-${branchId}` },
      ],
    }),

    setBranchPrice: builder.mutation<BranchProductPrice, SetBranchPriceRequest>(
      {
        query: (request) => ({
          url: "/inventory/branch-prices",
          method: "POST",
          body: request,
        }),
        transformResponse: (response: { data: BranchProductPrice }) =>
          response.data,
        invalidatesTags: (result, error, request) => [
          { type: "Inventory", id: `PRICES-${request.branchId}` },
          "Products",
        ],
      },
    ),

    removeBranchPrice: builder.mutation<
      boolean,
      { branchId: number; productId: number }
    >({
      query: ({ branchId, productId }) => ({
        url: `/inventory/branch-prices/${branchId}/${productId}`,
        method: "DELETE",
      }),
      transformResponse: (response: { data: boolean }) => response.data,
      invalidatesTags: (result, error, { branchId }) => [
        { type: "Inventory", id: `PRICES-${branchId}` },
        "Products",
      ],
    }),
  }),
});

export const {
  useGetBranchInventoryQuery,
  useGetProductInventoryAcrossBranchesQuery,
  useGetLowStockItemsQuery,
  useAdjustInventoryMutation,
  useAdjustProductStockMutation,
  useCreateTransferMutation,
  useGetTransfersQuery,
  useGetTransferByIdQuery,
  useApproveTransferMutation,
  useReceiveTransferMutation,
  useCancelTransferMutation,
  useGetBranchPricesQuery,
  useSetBranchPriceMutation,
  useRemoveBranchPriceMutation,
} = inventoryApi;

// Alias for backward compatibility
export const useGetLowStockProductsQuery = useGetLowStockItemsQuery;
