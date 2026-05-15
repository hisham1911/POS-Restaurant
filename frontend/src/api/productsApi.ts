import { baseApi } from "./baseApi";
import {
  Product,
  CreateProductRequest,
  UpdateProductRequest,
  ProductsQueryParams,
  QuickCreateProductRequest,
  ProductType,
  UnitOfMeasure,
} from "../types/product.types";
import { ApiResponse, PagedResult } from "../types/api.types";

const normalizeEnumValue = <TEnum extends Record<string, string | number>>(
  enumObject: TEnum,
  value: unknown,
): number => {
  if (typeof value === "number") {
    return value;
  }

  if (typeof value === "string") {
    const normalized = enumObject[value as keyof TEnum];
    if (typeof normalized === "number") {
      return normalized;
    }

    const asNumber = Number(value);
    if (!Number.isNaN(asNumber)) {
      return asNumber;
    }
  }

  return 0;
};

const normalizeProduct = (product: Product): Product => ({
  ...product,
  type: normalizeEnumValue(ProductType, product.type) as ProductType,
  unit: normalizeEnumValue(UnitOfMeasure, product.unit) as UnitOfMeasure,
});

const normalizeProductListResponse = (
  response: ApiResponse<PagedResult<Product>>,
): ApiResponse<PagedResult<Product>> => ({
  ...response,
  data: response.data
    ? {
        ...response.data,
        items: response.data.items.map(normalizeProduct),
      }
    : response.data,
});

const normalizeProductResponse = (
  response: ApiResponse<Product>,
): ApiResponse<Product> => ({
  ...response,
  data: response.data ? normalizeProduct(response.data) : response.data,
});

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
      transformResponse: normalizeProductListResponse,
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
      transformResponse: normalizeProductResponse,
      providesTags: (_result, _error, id) => [{ type: "Products", id }],
    }),

    createProduct: builder.mutation<ApiResponse<Product>, CreateProductRequest>(
      {
        query: (product) => ({
          url: "/products",
          method: "POST",
          body: product,
        }),
        transformResponse: normalizeProductResponse,
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
      transformResponse: normalizeProductResponse,
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
      transformResponse: normalizeProductResponse,
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
