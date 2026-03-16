import { baseApi } from "./baseApi";
import {
  TopCustomersReport,
  CustomerDebtsReport,
  CustomerActivityReport,
} from "../types/customer-report.types";
import { ApiResponse } from "../types/api.types";

export const customerReportsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // تقرير أفضل العملاء
    getTopCustomersReport: builder.query<
      ApiResponse<TopCustomersReport>,
      { fromDate: string; toDate: string; topCount?: number }
    >({
      query: ({ fromDate, toDate, topCount = 20 }) => ({
        url: "/customer-reports/top-customers",
        params: { fromDate, toDate, topCount },
      }),
      providesTags: ["Reports"],
    }),

    // تقرير ديون العملاء
    getCustomerDebtsReport: builder.query<ApiResponse<CustomerDebtsReport>, void>({
      query: () => "/customer-reports/debts",
      providesTags: ["Reports"],
    }),

    // تقرير نشاط العملاء
    getCustomerActivityReport: builder.query<
      ApiResponse<CustomerActivityReport>,
      { fromDate: string; toDate: string }
    >({
      query: ({ fromDate, toDate }) => ({
        url: "/customer-reports/activity",
        params: { fromDate, toDate },
      }),
      providesTags: ["Reports"],
    }),
  }),
});

export const {
  useGetTopCustomersReportQuery,
  useGetCustomerDebtsReportQuery,
  useGetCustomerActivityReportQuery,
} = customerReportsApi;
