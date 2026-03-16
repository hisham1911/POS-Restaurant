import { baseApi } from "./baseApi";
import { ProfitLossReport, ExpensesReport } from "../types/financial-report.types";
import { ApiResponse } from "../types/api.types";

export const financialReportsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // تقرير الأرباح والخسائر
    getProfitLossReport: builder.query<
      ApiResponse<ProfitLossReport>,
      { fromDate: string; toDate: string }
    >({
      query: ({ fromDate, toDate }) => ({
        url: "/financial-reports/profit-loss",
        params: { fromDate, toDate },
      }),
      providesTags: ["Reports"],
    }),

    // تقرير المصروفات
    getExpensesReport: builder.query<
      ApiResponse<ExpensesReport>,
      { fromDate: string; toDate: string }
    >({
      query: ({ fromDate, toDate }) => ({
        url: "/financial-reports/expenses",
        params: { fromDate, toDate },
      }),
      providesTags: ["Reports"],
    }),
  }),
});

export const {
  useGetProfitLossReportQuery,
  useGetExpensesReportQuery,
} = financialReportsApi;
