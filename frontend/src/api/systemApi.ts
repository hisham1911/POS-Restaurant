import { baseApi } from "./baseApi";
import type {
  CreateTenantRequest,
  CreateTenantResponse,
  SetTenantStatusRequest,
  SystemTenantSummary,
} from "../types/system";
import type { ApiResponse } from "../types/api.types";

export interface SystemInfo {
  lanIp: string;
  hostname: string;
  port: number;
  url: string;
  environment: string;
  timestamp: string;
  isOffline: boolean;
}

export interface HealthCheck {
  success: boolean;
  status: string;
  timestamp: string;
}

export const systemApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getTenants: builder.query<
      { success: boolean; data: SystemTenantSummary[]; message?: string },
      void
    >({
      query: () => ({
        url: "/system/tenants",
        method: "GET",
      }),
    }),
    createTenant: builder.mutation<
      { success: boolean; data: CreateTenantResponse; message: string },
      CreateTenantRequest
    >({
      query: (data) => ({
        url: "/system/tenants",
        method: "POST",
        body: data,
      }),
    }),
    setTenantStatus: builder.mutation<
      { success: boolean; data: boolean; message: string },
      { tenantId: number; body: SetTenantStatusRequest }
    >({
      query: ({ tenantId, body }) => ({
        url: `/system/tenants/${tenantId}/status`,
        method: "PATCH",
        body,
      }),
    }),

    // System Info (IP, Network, Environment)
    getSystemInfo: builder.query<
      { success: boolean; data: SystemInfo },
      void
    >({
      query: () => "/system/info",
    }),

    // Health Check (for network status monitoring)
    health: builder.query<HealthCheck, void>({
      query: () => "/system/health",
    }),
  }),
});

export const {
  useGetTenantsQuery,
  useCreateTenantMutation,
  useSetTenantStatusMutation,
  useGetSystemInfoQuery,
  useHealthQuery,
} = systemApi;
