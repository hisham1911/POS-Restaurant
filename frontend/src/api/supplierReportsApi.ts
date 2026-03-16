import { baseApi } from "./baseApi";
import {
  SupplierPurchasesReport,
  SupplierDebtsReport,
  SupplierPerformanceReport,
} from "../types/supplier-report.types";
import { ApiResponse } from "../types/api.types";

export const supplierReportsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getSupplierPurchasesReport: builder.query<
      ApiResponse<SupplierPurchasesReport>,
      { fromDate: string; toDate: string }
    >({
      query: ({ fromDate, toDate }) => ({
        url: "/supplier-reports/purchases",
        params: { fromDate, toDate },
      }),
      providesTags: ["Reports"],
    }),
    getSupplierDebtsReport: builder.query<
      ApiResponse<SupplierDebtsReport>,
      void
    >({
      query: () => "/supplier-reports/debts",
      providesTags: ["Reports"],
    }),
    getSupplierPerformanceReport: builder.query<
      ApiResponse<SupplierPerformanceReport>,
      { fromDate: string; toDate: string }
    >({
      query: ({ fromDate, toDate }) => ({
        url: "/supplier-reports/performance",
        params: { fromDate, toDate },
      }),
      providesTags: ["Reports"],
    }),
  }),
});

export const {
  useGetSupplierPurchasesReportQuery,
  useGetSupplierDebtsReportQuery,
  useGetSupplierPerformanceReportQuery,
} = supplierReportsApi;
