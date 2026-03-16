import { baseApi } from './baseApi';

export interface SystemUser {
  id: number;
  name: string;
  email: string;
  phone: string | null;
  role: string;
  tenantId: number | null;
  tenantName: string;
  branchId: number | null;
  branchName: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface UpdateSystemUserRequest {
  name?: string;
  email?: string;
  phone?: string;
  isActive?: boolean;
}

export interface ResetPasswordRequest {
  newPassword: string;
}

export const systemUsersApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getAllSystemUsers: builder.query<SystemUser[], void>({
      query: () => '/system/users',
      transformResponse: (response: { data: SystemUser[] }) => response.data,
      providesTags: ['SystemUsers'],
    }),

    updateSystemUser: builder.mutation<void, { userId: number; data: UpdateSystemUserRequest }>({
      query: ({ userId, data }) => ({
        url: `/system/users/${userId}`,
        method: 'PUT',
        body: data,
      }),
      invalidatesTags: ['SystemUsers'],
    }),

    toggleSystemUserStatus: builder.mutation<void, number>({
      query: (userId) => ({
        url: `/system/users/${userId}/toggle-status`,
        method: 'PATCH',
      }),
      invalidatesTags: ['SystemUsers'],
    }),

    resetSystemUserPassword: builder.mutation<void, { userId: number; data: ResetPasswordRequest }>({
      query: ({ userId, data }) => ({
        url: `/system/users/${userId}/reset-password`,
        method: 'POST',
        body: data,
      }),
    }),
  }),
});

export const {
  useGetAllSystemUsersQuery,
  useUpdateSystemUserMutation,
  useToggleSystemUserStatusMutation,
  useResetSystemUserPasswordMutation,
} = systemUsersApi;
