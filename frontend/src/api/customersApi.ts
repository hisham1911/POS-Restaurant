import { baseApi } from "./baseApi";
import { ApiResponse } from "../types/api.types";
import {
  Customer,
  CustomersPagedResult,
  CustomersQueryParams,
  CreateCustomerRequest,
  GetOrCreateCustomerRequest,
  GetOrCreateCustomerResponse,
  LoyaltyPointsRequest,
  PayDebtRequest,
  PayDebtResponse,
  DebtPaymentDto,
} from "../types/customer.types";

export const customersApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // جلب قائمة العملاء مع الترقيم
    getCustomers: builder.query<
      ApiResponse<CustomersPagedResult>,
      CustomersQueryParams | void
    >({
      query: (params) => ({
        url: "/customers",
        params: params || {},
      }),
      providesTags: (result) =>
        result?.data?.items
          ? [
              ...result.data.items.map(({ id }) => ({
                type: "Customers" as const,
                id,
              })),
              { type: "Customers", id: "LIST" },
            ]
          : [{ type: "Customers", id: "LIST" }],
    }),

    // جلب عميل بالمعرف
    getCustomer: builder.query<ApiResponse<Customer>, number>({
      query: (id) => `/customers/${id}`,
      providesTags: (_result, _error, id) => [{ type: "Customers", id }],
    }),

    // جلب عميل برقم الهاتف (للبحث في السلة)
    getCustomerByPhone: builder.query<ApiResponse<Customer>, string>({
      query: (phone) => `/customers/by-phone/${encodeURIComponent(phone)}`,
      providesTags: (result) =>
        result?.data ? [{ type: "Customers", id: result.data.id }] : [],
    }),

    // إنشاء عميل جديد
    createCustomer: builder.mutation<
      ApiResponse<Customer>,
      CreateCustomerRequest
    >({
      query: (customer) => ({
        url: "/customers",
        method: "POST",
        body: customer,
      }),
      invalidatesTags: [{ type: "Customers", id: "LIST" }],
    }),

    // الحصول على عميل أو إنشاء جديد (للدفع)
    getOrCreateCustomer: builder.mutation<
      GetOrCreateCustomerResponse,
      GetOrCreateCustomerRequest
    >({
      query: (data) => ({
        url: "/customers/get-or-create",
        method: "POST",
        body: data,
      }),
      invalidatesTags: (result) =>
        result?.wasCreated ? [{ type: "Customers", id: "LIST" }] : [],
    }),

    // تحديث بيانات عميل
    updateCustomer: builder.mutation<
      ApiResponse<Customer>,
      { id: number; data: Partial<Customer> }
    >({
      query: ({ id, data }) => ({
        url: `/customers/${id}`,
        method: "PUT",
        body: data,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: "Customers", id },
        { type: "Customers", id: "LIST" },
      ],
    }),

    // إضافة نقاط ولاء
    addLoyaltyPoints: builder.mutation<
      ApiResponse<{ message: string }>,
      { customerId: number; points: number }
    >({
      query: ({ customerId, points }) => ({
        url: `/customers/${customerId}/loyalty/add`,
        method: "POST",
        body: { points } as LoyaltyPointsRequest,
      }),
      invalidatesTags: (_result, _error, { customerId }) => [
        { type: "Customers", id: customerId },
      ],
    }),

    // استبدال نقاط ولاء
    redeemLoyaltyPoints: builder.mutation<
      ApiResponse<{ message: string }>,
      { customerId: number; points: number }
    >({
      query: ({ customerId, points }) => ({
        url: `/customers/${customerId}/loyalty/redeem`,
        method: "POST",
        body: { points } as LoyaltyPointsRequest,
      }),
      invalidatesTags: (_result, _error, { customerId }) => [
        { type: "Customers", id: customerId },
      ],
    }),

    // حذف عميل
    deleteCustomer: builder.mutation<ApiResponse<{ message: string }>, number>({
      query: (id) => ({
        url: `/customers/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: (_result, _error, id) => [
        { type: "Customers", id },
        { type: "Customers", id: "LIST" },
      ],
    }),

    // تسديد دين عميل
    payDebt: builder.mutation<
      ApiResponse<PayDebtResponse>,
      { customerId: number; data: PayDebtRequest }
    >({
      query: ({ customerId, data }) => ({
        url: `/customers/${customerId}/pay-debt`,
        method: "POST",
        body: data,
      }),
      invalidatesTags: (_result, _error, { customerId }) => [
        { type: "Customers", id: customerId },
        { type: "Customers", id: "LIST" },
        { type: "Customers", id: "DEBT-LIST" },
        { type: "Customers", id: `debt-${customerId}` },
      ],
    }),

    // جلب سجل تسديدات الديون لعميل
    getDebtHistory: builder.query<
      ApiResponse<DebtPaymentDto[]>,
      number
    >({
      query: (customerId) => `/customers/${customerId}/debt-history`,
      providesTags: (_result, _error, customerId) => [
        { type: "Customers", id: `debt-${customerId}` },
      ],
    }),

    // جلب العملاء الذين لديهم ديون
    getCustomersWithDebt: builder.query<ApiResponse<Customer[]>, void>({
      query: () => "/customers/with-debt",
      providesTags: (result) =>
        result?.data
          ? [
              ...result.data.map(({ id }) => ({
                type: "Customers" as const,
                id,
              })),
              { type: "Customers", id: "DEBT-LIST" },
            ]
          : [{ type: "Customers", id: "DEBT-LIST" }],
    }),

    // طباعة إيصال تسديد دين
    printDebtPaymentReceipt: builder.mutation<
      ApiResponse<{ message: string }>,
      number
    >({
      query: (paymentId) => ({
        url: `/customers/debt-payments/${paymentId}/print`,
        method: "POST",
      }),
    }),
  }),
});

export const {
  useGetCustomersQuery,
  useGetCustomerQuery,
  useGetCustomerByPhoneQuery,
  useLazyGetCustomerByPhoneQuery,
  useCreateCustomerMutation,
  useGetOrCreateCustomerMutation,
  useUpdateCustomerMutation,
  useAddLoyaltyPointsMutation,
  useRedeemLoyaltyPointsMutation,
  useDeleteCustomerMutation,
  usePayDebtMutation,
  useGetDebtHistoryQuery,
  useLazyGetDebtHistoryQuery,
  useGetCustomersWithDebtQuery,
  usePrintDebtPaymentReceiptMutation,
} = customersApi;
