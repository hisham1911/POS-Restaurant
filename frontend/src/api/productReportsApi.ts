import { baseApi } from "./baseApi";
import {
  ProductMovementReport,
  ProfitableProductsReport,
  SlowMovingProductsReport,
  CogsReport,
} from "../types/product-report.types";
import { ApiResponse } from "../types/api.types";

export const productReportsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getProductMovementReport: builder.query<
      ApiResponse<ProductMovementReport>,
      { fromDate: string; toDate: string; categoryId?: number }
    >({
      query: ({ fromDate, toDate, categoryId }) => ({
        url: "/product-reports/movement",
        params: { fromDate, toDate, categoryId },
      }),
      providesTags: ["Reports"],
    }),
    getProfitableProductsReport: builder.query<
      ApiResponse<ProfitableProductsReport>,
      { fromDate: string; toDate: string; topCount?: number }
    >({
      query: ({ fromDate, toDate, topCount = 10 }) => ({
        url: "/product-reports/profitability",
        params: { fromDate, toDate, topCount },
      }),
      providesTags: ["Reports"],
    }),
    getSlowMovingProductsReport: builder.query<
      ApiResponse<SlowMovingProductsReport>,
      { daysThreshold?: number }
    >({
      query: ({ daysThreshold = 30 }) => ({
        url: "/product-reports/slow",
        params: { daysThreshold },
      }),
      providesTags: ["Reports"],
    }),
    getCogsReport: builder.query<
      ApiResponse<CogsReport>,
      { fromDate: string; toDate: string }
    >({
      query: ({ fromDate, toDate }) => ({
        url: "/product-reports/cogs",
        params: { fromDate, toDate },
      }),
      providesTags: ["Reports"],
    }),
  }),
});

export const {
  useGetProductMovementReportQuery,
  useGetProfitableProductsReportQuery,
  useGetSlowMovingProductsReportQuery,
  useGetCogsReportQuery,
} = productReportsApi;
