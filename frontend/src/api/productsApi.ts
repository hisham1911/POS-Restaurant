import { baseApi } from "./baseApi";
import {
  Product,
  CreateProductRequest,
  UpdateProductRequest,
  ProductsQueryParams,
  QuickCreateProductRequest,
} from "../types/product.types";
import { ApiResponse, PagedResult } from "../types/api.types";

export const productsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getProducts: builder.query<
      ApiResponse<PagedResult<Product>>,
      ProductsQueryParams | void
    >({
      query: (params) => {
        const safeParams: ProductsQueryParams = (params ??
          {}) as ProductsQueryParams;
        const queryParams = new URLSearchParams();

        if (
          safeParams.categoryId !== undefined &&
          safeParams.categoryId !== null
        ) {
          queryParams.append("categoryId", safeParams.categoryId.toString());
        }

        if (
          safeParams.search !== undefined &&
          safeParams.search !== null &&
          safeParams.search.trim() !== ""
        ) {
          queryParams.append("search", safeParams.search.trim());
        }

        if (safeParams.isActive !== undefined) {
          queryParams.append("isActive", safeParams.isActive.toString());
        }

        if (safeParams.lowStock !== undefined) {
          queryParams.append("lowStock", safeParams.lowStock.toString());
        }

        if (safeParams.page !== undefined && safeParams.page !== null) {
          queryParams.append("page", safeParams.page.toString());
        }

        if (safeParams.pageSize !== undefined && safeParams.pageSize !== null) {
          queryParams.append("pageSize", safeParams.pageSize.toString());
        }

        const queryString = queryParams.toString();
        return `/products${queryString ? `?${queryString}` : ""}`;
      },
      providesTags: (result) => {
        const items = result?.data?.items ?? [];

        return [
          ...items.map(({ id }: { id: number }) => ({
            type: "Products" as const,
            id,
          })),
          { type: "Products" as const, id: "LIST" },
        ];
      },
    }),

    getProduct: builder.query<ApiResponse<Product>, number>({
      query: (id) => `/products/${id}`,
      providesTags: (_result, _error, id) => [{ type: "Products", id }],
    }),

    createProduct: builder.mutation<ApiResponse<Product>, CreateProductRequest>(
      {
        query: (product) => ({
          url: "/products",
          method: "POST",
          body: product,
        }),
        invalidatesTags: [{ type: "Products", id: "LIST" }],
      },
    ),

    updateProduct: builder.mutation<
      ApiResponse<Product>,
      { id: number; data: UpdateProductRequest }
    >({
      query: ({ id, data }) => ({
        url: `/products/${id}`,
        method: "PUT",
        body: data,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: "Products", id },
        { type: "Products", id: "LIST" },
      ],
    }),

    deleteProduct: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/products/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: [{ type: "Products", id: "LIST" }],
    }),

    quickCreateProduct: builder.mutation<
      ApiResponse<Product>,
      QuickCreateProductRequest
    >({
      query: (product) => ({
        url: "/products/quick-create",
        method: "POST",
        body: product,
      }),
      invalidatesTags: [{ type: "Products", id: "LIST" }],
    }),
  }),
});

export const {
  useGetProductsQuery,
  useGetProductQuery,
  useCreateProductMutation,
  useUpdateProductMutation,
  useDeleteProductMutation,
  useQuickCreateProductMutation,
} = productsApi;
