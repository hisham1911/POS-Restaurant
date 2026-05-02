import { baseApi } from "./baseApi";
import { Category } from "../types/category.types";
import { ApiResponse } from "../types/api.types";

type CategoryQueryParams = {
  search?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
};

type CategoryMutationRequest = {
  name: string;
  nameEn?: string;
  description?: string;
  imageUrl?: string;
  sortOrder?: number;
  isActive?: boolean;
};

export const categoriesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getCategories: builder.query<
      ApiResponse<Category[]>,
      CategoryQueryParams | void
    >({
      query: (params) => {
        const queryParams: Record<string, string | number> = {};

        if (params) {
          if (params.search) queryParams.search = params.search;
          if (params.isActive !== undefined) {
            queryParams.isActive = params.isActive.toString();
          }
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

    getCategory: builder.query<ApiResponse<Category>, number>({
      query: (id) => `/categories/${id}`,
      providesTags: (_result, _error, id) => [{ type: "Categories", id }],
    }),

    createCategory: builder.mutation<
      ApiResponse<Category>,
      CategoryMutationRequest
    >({
      query: (category) => ({
        url: "/categories",
        method: "POST",
        body: category,
      }),
      invalidatesTags: [{ type: "Categories", id: "LIST" }],
    }),

    updateCategory: builder.mutation<
      ApiResponse<Category>,
      { id: number; data: CategoryMutationRequest }
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
