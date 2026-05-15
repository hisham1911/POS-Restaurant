import { baseApi } from "./baseApi";
import type { ApiResponse } from "@/types/api.types";
import type {
  CreateRestaurantTableRequest,
  RestaurantTable,
  RestaurantTableStatus,
  UpdateRestaurantTableRequest,
} from "@/types/restaurant.types";

export const restaurantTablesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getRestaurantTables: builder.query<ApiResponse<RestaurantTable[]>, number | void>({
      query: (branchId) => ({
        url: "/restaurant-tables",
        params: branchId ? { branchId } : undefined,
      }),
      providesTags: ["RestaurantTables"],
    }),
    createRestaurantTable: builder.mutation<
      ApiResponse<RestaurantTable>,
      CreateRestaurantTableRequest
    >({
      query: (body) => ({
        url: "/restaurant-tables",
        method: "POST",
        body,
      }),
      invalidatesTags: ["RestaurantTables"],
    }),
    updateRestaurantTable: builder.mutation<
      ApiResponse<RestaurantTable>,
      { id: number; body: UpdateRestaurantTableRequest }
    >({
      query: ({ id, body }) => ({
        url: `/restaurant-tables/${id}`,
        method: "PUT",
        body,
      }),
      invalidatesTags: ["RestaurantTables"],
    }),
    deleteRestaurantTable: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/restaurant-tables/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: ["RestaurantTables"],
    }),
    setRestaurantTableStatus: builder.mutation<
      ApiResponse<RestaurantTable>,
      { id: number; status: RestaurantTableStatus }
    >({
      query: ({ id, status }) => ({
        url: `/restaurant-tables/${id}/status`,
        method: "POST",
        body: { status },
      }),
      invalidatesTags: ["RestaurantTables"],
    }),
  }),
});

export const {
  useGetRestaurantTablesQuery,
  useCreateRestaurantTableMutation,
  useUpdateRestaurantTableMutation,
  useDeleteRestaurantTableMutation,
  useSetRestaurantTableStatusMutation,
} = restaurantTablesApi;
