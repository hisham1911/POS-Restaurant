import { baseApi } from "./baseApi";
import {
  BranchInventoryReport,
  UnifiedInventoryReport,
  TransferHistoryReport,
  LowStockSummaryReport,
} from "../types/inventory-report.types";
import { ApiResponse } from "../types/api.types";

export const inventoryReportsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // تقرير مخزون الفرع
    getBranchInventoryReport: builder.query<
      ApiResponse<BranchInventoryReport>,
      { branchId: number; categoryId?: number; lowStockOnly?: boolean }
    >({
      query: ({ branchId, categoryId, lowStockOnly }) => ({
        url: `/inventory-reports/branch/${branchId}`,
        params: { categoryId, lowStockOnly },
      }),
      providesTags: ["Reports"],
    }),

    // تقرير المخزون الموحد
    getUnifiedInventoryReport: builder.query<
      ApiResponse<UnifiedInventoryReport[]>,
      { categoryId?: number; lowStockOnly?: boolean }
    >({
      query: ({ categoryId, lowStockOnly }) => ({
        url: "/inventory-reports/unified",
        params: { categoryId, lowStockOnly },
      }),
      providesTags: ["Reports"],
    }),

    // تقرير تاريخ التحويلات
    getTransferHistoryReport: builder.query<
      ApiResponse<TransferHistoryReport>,
      { fromDate?: string; toDate?: string; branchId?: number }
    >({
      query: ({ fromDate, toDate, branchId }) => ({
        url: "/inventory-reports/transfer-history",
        params: { fromDate, toDate, branchId },
      }),
      providesTags: ["Reports"],
    }),

    // تقرير المخزون المنخفض
    getLowStockSummaryReport: builder.query<
      ApiResponse<LowStockSummaryReport>,
      { branchId?: number }
    >({
      query: ({ branchId }) => ({
        url: "/inventory-reports/low-stock-summary",
        params: { branchId },
      }),
      providesTags: ["Reports"],
    }),
  }),
});

export const {
  useGetBranchInventoryReportQuery,
  useGetUnifiedInventoryReportQuery,
  useGetTransferHistoryReportQuery,
  useGetLowStockSummaryReportQuery,
} = inventoryReportsApi;
