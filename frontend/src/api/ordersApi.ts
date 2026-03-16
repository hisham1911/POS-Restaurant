import { baseApi } from "./baseApi";
import {
  Order,
  CreateOrderRequest,
  CompleteOrderRequest,
  OrdersQueryParams,
  PagedOrders,
  AddCustomItemRequest,
} from "../types/order.types";
import { ApiResponse } from "../types/api.types";

// Paged Result for customer orders
interface OrdersPagedResult {
  items: Order[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

interface CustomerOrdersParams {
  customerId: number;
  page?: number;
  pageSize?: number;
}

export const ordersApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // جلب كل الطلبات مع الفلاتر والباجنيشن
    getOrders: builder.query<ApiResponse<PagedOrders>, OrdersQueryParams | void>({
      query: (params) => {
        const queryParams: Record<string, string> = {};
        
        if (params) {
          if (params.status) queryParams.status = params.status;
          if (params.fromDate) queryParams.fromDate = params.fromDate;
          if (params.toDate) queryParams.toDate = params.toDate;
          if (params.page) queryParams.page = params.page.toString();
          if (params.pageSize) queryParams.pageSize = params.pageSize.toString();
        }

        return {
          url: "/orders",
          params: queryParams,
        };
      },
      providesTags: (result) =>
        result?.data?.items
          ? [
              ...result.data.items.map(({ id }) => ({
                type: "Orders" as const,
                id,
              })),
              { type: "Orders", id: "LIST" },
            ]
          : [{ type: "Orders", id: "LIST" }],
    }),

    // جلب طلب واحد
    getOrder: builder.query<ApiResponse<Order>, number>({
      query: (id) => `/orders/${id}`,
      providesTags: (_result, _error, id) => [{ type: "Orders", id }],
    }),

    // جلب طلبات اليوم
    getTodayOrders: builder.query<ApiResponse<Order[]>, void>({
      query: () => "/orders/today",
      providesTags: [{ type: "Orders", id: "LIST" }],
    }),

    // جلب طلبات عميل معين
    getCustomerOrders: builder.query<
      ApiResponse<OrdersPagedResult>,
      CustomerOrdersParams
    >({
      query: ({ customerId, page = 1, pageSize = 10 }) => ({
        url: `/orders/by-customer/${customerId}`,
        params: { page, pageSize },
      }),
      providesTags: (_result, _error, { customerId }) => [
        { type: "Orders", id: `CUSTOMER_${customerId}` },
      ],
    }),

    // إنشاء طلب جديد
    createOrder: builder.mutation<ApiResponse<Order>, CreateOrderRequest>({
      query: (order) => ({
        url: "/orders",
        method: "POST",
        body: order,
      }),
      invalidatesTags: [{ type: "Orders", id: "LIST" }, "Shifts"],
    }),

    // إضافة عنصر للطلب
    addOrderItem: builder.mutation<
      ApiResponse<Order>,
      {
        orderId: number;
        item: { productId: number; quantity: number; notes?: string };
      }
    >({
      query: ({ orderId, item }) => ({
        url: `/orders/${orderId}/items`,
        method: "POST",
        body: item,
      }),
      invalidatesTags: (_result, _error, { orderId }) => [
        { type: "Orders", id: orderId },
      ],
    }),

    // إضافة منتج مخصص للطلب (ليس من الكتالوج)
    addCustomItem: builder.mutation<
      ApiResponse<Order>,
      {
        orderId: number;
        item: AddCustomItemRequest;
      }
    >({
      query: ({ orderId, item }) => ({
        url: `/orders/${orderId}/items/custom`,
        method: "POST",
        body: item,
      }),
      invalidatesTags: (_result, _error, { orderId }) => [
        { type: "Orders", id: orderId },
      ],
    }),

    // حذف عنصر من الطلب
    removeOrderItem: builder.mutation<
      ApiResponse<Order>,
      { orderId: number; itemId: number }
    >({
      query: ({ orderId, itemId }) => ({
        url: `/orders/${orderId}/items/${itemId}`,
        method: "DELETE",
      }),
      invalidatesTags: (_result, _error, { orderId }) => [
        { type: "Orders", id: orderId },
      ],
    }),

    // إكمال الطلب
    completeOrder: builder.mutation<
      ApiResponse<Order>,
      { orderId: number; data: CompleteOrderRequest }
    >({
      query: ({ orderId, data }) => ({
        url: `/orders/${orderId}/complete`,
        method: "POST",
        body: data,
      }),
      invalidatesTags: (_result, _error, { orderId }) => [
        { type: "Orders", id: orderId },
        { type: "Orders", id: "LIST" },
        "Shifts",
        "Customers", // Invalidate customers in case order affects customer debt
        "Inventory", // Invalidate inventory as stock is updated
      ],
    }),

    // إلغاء الطلب
    cancelOrder: builder.mutation<
      ApiResponse<boolean>,
      { orderId: number; reason?: string }
    >({
      query: ({ orderId, reason }) => ({
        url: `/orders/${orderId}/cancel`,
        method: "POST",
        body: { reason },
      }),
      invalidatesTags: (_result, _error, { orderId }) => [
        { type: "Orders", id: orderId },
        { type: "Orders", id: "LIST" },
      ],
    }),

    // استرجاع الطلب (Full or Partial Refund)
    refundOrder: builder.mutation<
      ApiResponse<Order>,
      {
        orderId: number;
        reason?: string;
        items?: { itemId: number; quantity: number; reason?: string }[];
      }
    >({
      query: ({ orderId, reason, items }) => ({
        url: `/orders/${orderId}/refund`,
        method: "POST",
        body: { reason, items },
      }),
      // Invalidate Orders AND Inventory (stock is restored on refund)
      invalidatesTags: (_result, _error, { orderId }) => [
        { type: "Orders", id: orderId },
        { type: "Orders", id: "LIST" },
        { type: "Inventory", id: "LOW_STOCK" },
        { type: "Products", id: "LIST" },
        "Shifts",
      ],
    }),

    // طباعة فاتورة الطلب
    printReceipt: builder.mutation<ApiResponse<{ message: string }>, number>({
      query: (orderId) => ({
        url: `/orders/${orderId}/print`,
        method: "POST",
      }),
    }),
  }),
});

export const {
  useGetOrdersQuery,
  useGetOrderQuery,
  useGetTodayOrdersQuery,
  useGetCustomerOrdersQuery,
  useCreateOrderMutation,
  useAddOrderItemMutation,
  useAddCustomItemMutation,
  useRemoveOrderItemMutation,
  useCompleteOrderMutation,
  useCancelOrderMutation,
  useRefundOrderMutation,
  usePrintReceiptMutation,
} = ordersApi;
