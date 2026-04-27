import { baseApi } from "./baseApi";
import type { ApiResponse } from "../types/api.types";
import type {
  Branch,
  CreateBranchRequest,
  UpdateBranchRequest,
} from "../types/branch.types";
import type { Tenant, UpdateTenantRequest } from "../types/tenant.types";
import { toast } from "sonner";

export const branchesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // Branches
    getBranches: builder.query<ApiResponse<Branch[]>, void>({
      query: () => "/branches",
      providesTags: ["Branches"],
    }),
    getBranch: builder.query<ApiResponse<Branch>, number>({
      query: (id) => `/branches/${id}`,
      providesTags: (_result, _error, id) => [{ type: "Branches", id }],
    }),
    createBranch: builder.mutation<ApiResponse<Branch>, CreateBranchRequest>({
      query: (body) => ({
        url: "/branches",
        method: "POST",
        body,
      }),
      invalidatesTags: ["Branches"],
    }),
    updateBranch: builder.mutation<
      ApiResponse<Branch>,
      { id: number; data: UpdateBranchRequest }
    >({
      query: ({ id, data }) => ({
        url: `/branches/${id}`,
        method: "PUT",
        body: data,
      }),
      invalidatesTags: ["Branches"],
    }),
    deleteBranch: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/branches/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: ["Branches"],
      async onQueryStarted(_id, { queryFulfilled }) {
        try {
          await queryFulfilled;
          toast.success("تم حذف الفرع بنجاح");
        } catch {
          // Error toast already shown by baseApi global handler
        }
      },
    }),

    // Tenant
    getCurrentTenant: builder.query<ApiResponse<Tenant>, void>({
      query: () => "/tenants/current",
      providesTags: ["Tenant"],
    }),
    updateCurrentTenant: builder.mutation<
      ApiResponse<Tenant>,
      UpdateTenantRequest
    >({
      query: (body) => ({
        url: "/tenants/current",
        method: "PUT",
        body,
      }),
      invalidatesTags: ["Tenant"],
    }),
    uploadLogo: builder.mutation<ApiResponse<{ logoUrl: string }>, FormData>({
      query: (formData) => ({
        url: "/tenants/current/logo",
        method: "POST",
        body: formData,
      }),
      invalidatesTags: ["Tenant"],
    }),
  }),
});

export const {
  useGetBranchesQuery,
  useGetBranchQuery,
  useCreateBranchMutation,
  useUpdateBranchMutation,
  useDeleteBranchMutation,
  useGetCurrentTenantQuery,
  useUpdateCurrentTenantMutation,
  useUploadLogoMutation,
} = branchesApi;
