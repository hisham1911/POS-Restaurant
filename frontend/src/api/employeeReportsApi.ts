import { baseApi } from "./baseApi";
import {
  CashierPerformanceReport,
  DetailedShiftsReport,
  SalesByEmployeeReport,
} from "../types/employee-report.types";
import { ApiResponse } from "../types/api.types";

export const employeeReportsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getCashierPerformanceReport: builder.query<
      ApiResponse<CashierPerformanceReport>,
      { fromDate: string; toDate: string }
    >({
      query: ({ fromDate, toDate }) => ({
        url: "/employee-reports/cashier-performance",
        params: { fromDate, toDate },
      }),
      providesTags: ["Reports"],
    }),
    getDetailedShiftsReport: builder.query<
      ApiResponse<DetailedShiftsReport>,
      { fromDate: string; toDate: string; userId?: number }
    >({
      query: ({ fromDate, toDate, userId }) => ({
        url: "/employee-reports/shifts",
        params: { fromDate, toDate, userId },
      }),
      providesTags: ["Reports"],
    }),
    getSalesByEmployeeReport: builder.query<
      ApiResponse<SalesByEmployeeReport>,
      { fromDate: string; toDate: string }
    >({
      query: ({ fromDate, toDate }) => ({
        url: "/employee-reports/sales",
        params: { fromDate, toDate },
      }),
      providesTags: ["Reports"],
    }),
  }),
});

export const {
  useGetCashierPerformanceReportQuery,
  useGetDetailedShiftsReportQuery,
  useGetSalesByEmployeeReportQuery,
} = employeeReportsApi;
