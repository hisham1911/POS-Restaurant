import { baseApi } from "./baseApi";
import {
  Shift,
  OpenShiftRequest,
  CloseShiftRequest,
  ForceCloseShiftRequest,
  HandoverShiftRequest,
  ShiftWarning,
} from "../types/shift.types";
import { ApiResponse } from "../types/api.types";

export const shiftsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // جلب الوردية الحالية
    getCurrentShift: builder.query<ApiResponse<Shift | null>, void>({
      query: () => "/shifts/current",
      providesTags: [{ type: "Shifts", id: "CURRENT" }],
    }),

    // جلب سجل الورديات
    getShifts: builder.query<ApiResponse<Shift[]>, void>({
      query: () => "/shifts/history",
      providesTags: ["Shifts"],
    }),

    // فتح وردية
    openShift: builder.mutation<ApiResponse<Shift>, OpenShiftRequest>({
      query: (data) => ({
        url: "/shifts/open",
        method: "POST",
        body: data,
      }),
      invalidatesTags: ["Shifts"],
    }),

    // إغلاق وردية
    closeShift: builder.mutation<ApiResponse<Shift>, CloseShiftRequest>({
      query: (data) => ({
        url: "/shifts/close",
        method: "POST",
        body: data,
      }),
      invalidatesTags: ["Shifts"],
    }),

    // إغلاق وردية بالقوة (Admin only)
    forceCloseShift: builder.mutation<
      ApiResponse<Shift>,
      { id: number; request: ForceCloseShiftRequest }
    >({
      query: ({ id, request }) => ({
        url: `/shifts/${id}/force-close`,
        method: "POST",
        body: request,
      }),
      invalidatesTags: ["Shifts"],
    }),

    // تسليم وردية
    handoverShift: builder.mutation<
      ApiResponse<Shift>,
      { id: number; request: HandoverShiftRequest }
    >({
      query: ({ id, request }) => ({
        url: `/shifts/${id}/handover`,
        method: "POST",
        body: request,
      }),
      invalidatesTags: ["Shifts"],
    }),

    // تحديث النشاط
    updateShiftActivity: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/shifts/${id}/update-activity`,
        method: "POST",
      }),
      // لا نحتاج invalidate لأنه مجرد timestamp update
    }),

    // جلب الورديات المفتوحة
    getActiveShifts: builder.query<ApiResponse<Shift[]>, void>({
      query: () => "/shifts/active",
      providesTags: ["Shifts"],
    }),

    // جلب تحذيرات الوردية
    getShiftWarnings: builder.query<ApiResponse<ShiftWarning>, void>({
      query: () => "/shifts/warnings",
      providesTags: [{ type: "Shifts", id: "WARNINGS" }],
    }),
  }),
});

export const {
  useGetCurrentShiftQuery,
  useGetShiftsQuery,
  useOpenShiftMutation,
  useCloseShiftMutation,
  useForceCloseShiftMutation,
  useHandoverShiftMutation,
  useUpdateShiftActivityMutation,
  useGetActiveShiftsQuery,
  useGetShiftWarningsQuery,
} = shiftsApi;
