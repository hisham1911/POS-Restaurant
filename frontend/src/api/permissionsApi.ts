import { baseApi } from "./baseApi";
import { ApiResponse } from "../types/api.types";
import {
  PermissionInfo,
  PermissionGroupDto,
  UserPermissions,
  UserPermissionsDto,
  UpdatePermissionsRequest,
  UpdatePermissionsDto,
} from "../types/permission.types";

export const permissionsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // جلب كل الصلاحيات المتاحة مع الوصف
    getAvailablePermissions: builder.query<
      ApiResponse<PermissionInfo[]>,
      void
    >({
      query: () => "/permissions/available",
      providesTags: [{ type: "Permissions", id: "AVAILABLE" }],
    }),

    // جلب كل الكاشيرات مع صلاحياتهم
    getAllCashierPermissions: builder.query<
      ApiResponse<UserPermissions[]>,
      void
    >({
      query: () => "/permissions/users",
      providesTags: (result) =>
        result?.data
          ? [
              ...result.data.map(({ userId }) => ({
                type: "Permissions" as const,
                id: userId,
              })),
              { type: "Permissions", id: "LIST" },
            ]
          : [{ type: "Permissions", id: "LIST" }],
    }),

    // جلب صلاحيات مستخدم معين (legacy string list)
    getUserPermissions: builder.query<ApiResponse<UserPermissions>, number>({
      query: (userId) => `/permissions/user/${userId}`,
      providesTags: (_result, _error, userId) => [
        { type: "Permissions", id: userId },
      ],
    }),

    // جلب صلاحيات مستخدم معين (rich DTO with defaults + role)
    getUserPermissionsDto: builder.query<ApiResponse<UserPermissionsDto>, number>({
      query: (userId) => `/permissions/user/${userId}/dto`,
      providesTags: (_result, _error, userId) => [
        { type: "UserPermissions", id: userId },
      ],
    }),

    // تحديث صلاحيات مستخدم
    updateUserPermissions: builder.mutation<
      ApiResponse<{ message: string }>,
      UpdatePermissionsDto
    >({
      query: ({ userId, permissions }) => ({
        url: `/permissions/user/${userId}`,
        method: "PUT",
        body: { permissions },
      }),
      invalidatesTags: (_result, _error, { userId }) => [
        { type: "Permissions", id: userId },
        { type: "Permissions", id: "LIST" },
        { type: "UserPermissions", id: userId },
      ],
    }),
  }),
});

export const {
  useGetAvailablePermissionsQuery,
  useGetAllCashierPermissionsQuery,
  useGetUserPermissionsQuery,
  useGetUserPermissionsDtoQuery,
  useUpdateUserPermissionsMutation,
} = permissionsApi;
