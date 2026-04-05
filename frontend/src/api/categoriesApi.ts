import { baseApi } from "./baseApi";
import { Category } from "../types/category.types";
import { ApiResponse } from "../types/api.types";

export const categoriesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // جلب كل التصنيفات
    getCategories: builder.query<
      ApiResponse<Category[]>,
      { search?: string; page?: number; pageSize?: number } | void
    >({
      query: (params) => {
        const queryParams: Record<string, any> = {};

        if (params) {
          if (params.search) queryParams.search = params.search;
          if (params.page) queryParams.page = params.page;
          if (params.pageSize) queryParams.pageSize = params.pageSize;
        }

        return {
          url: "/categories",
          params: Object.keys(queryParams).length > 0 ? queryParams : undefined,
        };
      },
      providesTags: (result) =>
        result?.data
          ? [
              ...result.data.map(({ id }) => ({
                type: "Categories" as const,
                id,
              })),
              { type: "Categories", id: "LIST" },
            ]
          : [{ type: "Categories", id: "LIST" }],
    }),

    // جلب تصنيف واحد
    getCategory: builder.query<ApiResponse<Category>, number>({
      query: (id) => `/categories/${id}`,
      providesTags: (_result, _error, id) => [{ type: "Categories", id }],
    }),

    // إضافة تصنيف
    createCategory: builder.mutation<
      ApiResponse<Category>,
      {
        name: string;
        nameEn?: string;
        description?: string;
        imageUrl?: string;
        sortOrder?: number;
      }
    >({
      query: (category) => ({
        url: "/categories",
        method: "POST",
        body: category,
      }),
      invalidatesTags: [{ type: "Categories", id: "LIST" }],
    }),

    // تحديث تصنيف
    updateCategory: builder.mutation<
      ApiResponse<Category>,
      { id: number; data: Partial<Category> }
    >({
      query: ({ id, data }) => ({
        url: `/categories/${id}`,
        method: "PUT",
        body: data,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: "Categories", id },
        { type: "Categories", id: "LIST" },
      ],
    }),

    // حذف تصنيف
    deleteCategory: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/categories/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: [{ type: "Categories", id: "LIST" }],
    }),
  }),
});

export const {
  useGetCategoriesQuery,
  useGetCategoryQuery,
  useCreateCategoryMutation,
  useUpdateCategoryMutation,
  useDeleteCategoryMutation,
} = categoriesApi;
