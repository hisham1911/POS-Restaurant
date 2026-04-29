import { baseApi } from "@/api/baseApi";
import type { ApiResponse, PagedResult } from "@/types/api.types";
import type {
  AssignDeliveryRequest,
  CreateDeliveryPersonRequest,
  DeliveryOrderDto,
  DeliveryOrderFilters,
  DeliveryPerson,
  DeliveryPersonFilters,
  UpdateDeliveryPersonRequest,
  UpdateDeliveryStatusRequest,
} from "@/types/delivery.types";
import type { Order } from "@/types/order.types";

export const deliveryApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getDeliveryPersons: builder.query<
      ApiResponse<PagedResult<DeliveryPerson>>,
      DeliveryPersonFilters
    >({
      query: (filters) => ({
        url: "delivery/persons",
        params: {
          page: filters.page ?? 1,
          pageSize: filters.pageSize ?? 20,
          search: filters.search,
        },
      }),
      providesTags: (result) =>
        result?.data?.items
          ? [
              ...result.data.items.map(({ id }) => ({
                type: "DeliveryPerson" as const,
                id,
              })),
              { type: "DeliveryPerson", id: "LIST" },
            ]
          : [{ type: "DeliveryPerson", id: "LIST" }],
    }),

    getDeliveryPersonById: builder.query<ApiResponse<DeliveryPerson>, number>({
      query: (id) => `delivery/persons/${id}`,
      providesTags: (_result, _error, id) => [{ type: "DeliveryPerson", id }],
    }),

    getActiveDeliveryPersons: builder.query<ApiResponse<DeliveryPerson[]>, void>(
      {
        query: () => "delivery/persons/active",
        providesTags: [{ type: "DeliveryPerson", id: "ACTIVE" }],
      },
    ),

    getDeliveryOrders: builder.query<
      ApiResponse<PagedResult<DeliveryOrderDto>>,
      DeliveryOrderFilters
    >({
      query: (filters) => ({
        url: "delivery/orders",
        params: filters,
      }),
      providesTags: (result) =>
        result?.data?.items
          ? [
              ...result.data.items.map(({ id }) => ({
                type: "DeliveryOrders" as const,
                id,
              })),
              { type: "DeliveryOrders", id: "LIST" },
            ]
          : [{ type: "DeliveryOrders", id: "LIST" }],
    }),

    createDeliveryPerson: builder.mutation<
      ApiResponse<DeliveryPerson>,
      CreateDeliveryPersonRequest
    >({
      query: (body) => ({
        url: "delivery/persons",
        method: "POST",
        body,
      }),
      invalidatesTags: [
        { type: "DeliveryPerson", id: "LIST" },
        { type: "DeliveryPerson", id: "ACTIVE" },
      ],
    }),

    updateDeliveryPerson: builder.mutation<
      ApiResponse<DeliveryPerson>,
      { id: number; body: UpdateDeliveryPersonRequest }
    >({
      query: ({ id, body }) => ({
        url: `delivery/persons/${id}`,
        method: "PUT",
        body,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: "DeliveryPerson", id },
        { type: "DeliveryPerson", id: "LIST" },
        { type: "DeliveryPerson", id: "ACTIVE" },
      ],
    }),

    deleteDeliveryPerson: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `delivery/persons/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: [
        { type: "DeliveryPerson", id: "LIST" },
        { type: "DeliveryPerson", id: "ACTIVE" },
      ],
    }),

    assignDeliveryPerson: builder.mutation<
      ApiResponse<Order>,
      { orderId: number; body: AssignDeliveryRequest }
    >({
      query: ({ orderId, body }) => ({
        url: `delivery/orders/${orderId}/assign`,
        method: "POST",
        body,
      }),
      invalidatesTags: (_result, _error, { orderId }) => [
        { type: "Orders", id: orderId },
        { type: "Orders", id: "LIST" },
        { type: "DeliveryOrders", id: "LIST" },
      ],
    }),

    updateDeliveryStatus: builder.mutation<
      ApiResponse<Order>,
      { orderId: number; body: UpdateDeliveryStatusRequest }
    >({
      query: ({ orderId, body }) => ({
        url: `delivery/orders/${orderId}/status`,
        method: "PUT",
        body,
      }),
      invalidatesTags: (_result, _error, { orderId }) => [
        { type: "Orders", id: orderId },
        { type: "Orders", id: "LIST" },
        { type: "DeliveryOrders", id: "LIST" },
      ],
    }),
  }),
});

export const {
  useGetDeliveryPersonsQuery,
  useGetDeliveryPersonByIdQuery,
  useGetActiveDeliveryPersonsQuery,
  useGetDeliveryOrdersQuery,
  useCreateDeliveryPersonMutation,
  useUpdateDeliveryPersonMutation,
  useDeleteDeliveryPersonMutation,
  useAssignDeliveryPersonMutation,
  useUpdateDeliveryStatusMutation,
} = deliveryApi;
