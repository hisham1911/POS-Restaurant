import { baseApi } from "./baseApi";
import { LoginRequest, LoginResponse, User } from "../types/auth.types";
import { ApiResponse } from "../types/api.types";

export const authApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // تسجيل الدخول
    login: builder.mutation<ApiResponse<LoginResponse>, LoginRequest>({
      query: (credentials) => ({
        url: "/auth/login",
        method: "POST",
        body: credentials,
      }),
      invalidatesTags: ["User"],
    }),

    // الحصول على بيانات المستخدم
    getMe: builder.query<ApiResponse<User>, void>({
      query: () => "/auth/me",
      providesTags: ["User"],
    }),

    // تسجيل مستخدم جديد
    register: builder.mutation<
      ApiResponse<boolean>,
      { name: string; email: string; password: string; role?: string }
    >({
      query: (data) => ({
        url: "/auth/register",
        method: "POST",
        body: data,
      }),
    }),
  }),
});

export const { useLoginMutation, useGetMeQuery, useRegisterMutation } = authApi;
