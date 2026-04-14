import { baseApi } from "./baseApi";
import type { ApiResponse } from "@/types/api.types";
import type { PrinterStatus } from "@/types/printer.types";

export const printerApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getPrinterStatus: builder.query<ApiResponse<PrinterStatus>, void>({
      query: () => "/printer-status",
      providesTags: ["PrinterStatus"],
    }),
  }),
});

export const { useGetPrinterStatusQuery } = printerApi;
