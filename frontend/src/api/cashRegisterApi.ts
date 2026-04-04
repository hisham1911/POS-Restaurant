import { baseApi } from './baseApi';
import type {
  CashRegisterTransaction,
  CashRegisterBalance,
  CashRegisterSummary,
  CreateCashRegisterTransactionRequest,
  ReconcileCashRegisterRequest,
  TransferCashRequest,
  CashRegisterFilters,
} from '../types/cashRegister.types';
import type { ApiResponse } from '../types/api.types';

// Define PagedResult locally if not exported from api.types
interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export const cashRegisterApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // Get current balance
    getCurrentBalance: builder.query<ApiResponse<CashRegisterBalance>, number | void>({
      query: (branchId) => ({
        url: '/cash-register/balance',
        params: branchId ? { branchId } : undefined,
      }),
      providesTags: ['CashRegisterBalance'],
    }),

    // Get transactions with filters and pagination
    getTransactions: builder.query<ApiResponse<PagedResult<CashRegisterTransaction>>, CashRegisterFilters>({
      query: (filters) => ({
        url: '/cash-register/transactions',
        params: {
          branchId: filters.branchId,
          type: filters.type,
          fromDate: filters.fromDate,
          toDate: filters.toDate,
          shiftId: filters.shiftId,
          pageNumber: filters.pageNumber || 1,
          pageSize: filters.pageSize || 20,
        },
      }),
      providesTags: ['CashRegisterTransactions'],
    }),

    // Deposit
    deposit: builder.mutation<ApiResponse<CashRegisterTransaction>, Omit<CreateCashRegisterTransactionRequest, 'type'>>({
      query: (request) => ({
        url: '/cash-register/deposit',
        method: 'POST',
        body: request,
      }),
      invalidatesTags: ['CashRegisterBalance', 'CashRegisterTransactions'],
    }),

    // Withdrawal
    withdraw: builder.mutation<ApiResponse<CashRegisterTransaction>, Omit<CreateCashRegisterTransactionRequest, 'type'>>({
      query: (request) => ({
        url: '/cash-register/withdraw',
        method: 'POST',
        body: request,
      }),
      invalidatesTags: ['CashRegisterBalance', 'CashRegisterTransactions'],
    }),

    // Reconcile
    reconcile: builder.mutation<ApiResponse<boolean>, { shiftId: number; request: ReconcileCashRegisterRequest }>({
      query: ({ shiftId, request }) => ({
        url: '/cash-register/reconcile',
        method: 'POST',
        params: { shiftId },
        body: request,
      }),
      invalidatesTags: ['CashRegisterBalance', 'CashRegisterTransactions'],
    }),

    // Transfer cash between branches
    transferCash: builder.mutation<ApiResponse<boolean>, TransferCashRequest>({
      query: (request) => ({
        url: '/cash-register/transfer',
        method: 'POST',
        body: request,
      }),
      invalidatesTags: ['CashRegisterBalance', 'CashRegisterTransactions'],
    }),

    // Get summary
    getSummary: builder.query<
      ApiResponse<CashRegisterSummary>,
      { branchId: number; fromDate: string; toDate: string }
    >({
      query: ({ branchId, fromDate, toDate }) => ({
        url: '/cash-register/summary',
        params: { branchId, fromDate, toDate },
      }),
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetCurrentBalanceQuery,
  useGetTransactionsQuery,
  useDepositMutation,
  useWithdrawMutation,
  useReconcileMutation,
  useTransferCashMutation,
  useGetSummaryQuery,
} = cashRegisterApi;
