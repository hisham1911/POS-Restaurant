import { baseApi } from "./baseApi";
import {
  Product,
  CreateProductRequest,
  UpdateProductRequest,
  ProductsQueryParams,
  QuickCreateProductRequest,
} from "../types/product.types";
import { ApiResponse } from "../types/api.types";

interface PagedProductsResult {
  items: Product[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

const normalizeProductsResponse = (
  response: ApiResponse<Product[] | PagedProductsResult>,
): ApiResponse<Product[]> => {
  const rawData = response.data;
  if (Array.isArray(rawData)) {
    return response as ApiResponse<Product[]>;
  }

  return {
    ...response,
    data: rawData?.items ?? [],
  };
};

export const productsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // جلب كل المنتجات مع الفلاتر
    getProducts: builder.query<
      ApiResponse<Product[]>,
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
        if (safeParams.isActive !== undefined)
          queryParams.append("isActive", safeParams.isActive.toString());
        if (safeParams.lowStock !== undefined)
          queryParams.append("lowStock", safeParams.lowStock.toString());

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
        const items = Array.isArray(result?.data)
          ? result.data
          : ((result?.data as any)?.items ?? []);

        return [
          ...items.map(({ id }: { id: number }) => ({
            type: "Products" as const,
            id,
          })),
          { type: "Products" as const, id: "LIST" },
        ];
      },
      transformResponse: normalizeProductsResponse,
    }),

    // جلب منتج واحد
    getProduct: builder.query<ApiResponse<Product>, number>({
      query: (id) => `/products/${id}`,
      providesTags: (_result, _error, id) => [{ type: "Products", id }],
    }),

    // إضافة منتج
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

    // تحديث منتج
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

    // حذف منتج
    deleteProduct: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/products/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: [{ type: "Products", id: "LIST" }],
    }),

    // إنشاء سريع من POS
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
