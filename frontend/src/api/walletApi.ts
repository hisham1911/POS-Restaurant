import { baseApi } from "./baseApi";
import type {
  Wallet,
  WalletTransaction,
  CreateWalletRequest,
  UpdateWalletRequest,
  WalletDepositWithdrawRequest,
  WalletTransactionFilters,
  PagedWalletTransactions,
} from "../types/wallet.types";
import type { ApiResponse } from "../types/api.types";

export const walletApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getWallets: builder.query<ApiResponse<Wallet[]>, void>({
      query: () => "/wallets",
      providesTags: ["Wallets"],
    }),

    getActiveWallets: builder.query<ApiResponse<Wallet[]>, void>({
      query: () => "/wallets/active",
      providesTags: ["Wallets"],
    }),

    getWalletById: builder.query<ApiResponse<Wallet>, number>({
      query: (id) => `/wallets/${id}`,
      providesTags: (_result, _error, id) => [{ type: "Wallets", id }],
    }),

    createWallet: builder.mutation<ApiResponse<Wallet>, CreateWalletRequest>({
      query: (body) => ({
        url: "/wallets",
        method: "POST",
        body,
      }),
      invalidatesTags: ["Wallets"],
    }),

    updateWallet: builder.mutation<ApiResponse<Wallet>, { id: number; body: UpdateWalletRequest }>({
      query: ({ id, body }) => ({
        url: `/wallets/${id}`,
        method: "PUT",
        body,
      }),
      invalidatesTags: (_result, _error, { id }) => [{ type: "Wallets", id }, "Wallets"],
    }),

    deleteWallet: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/wallets/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: ["Wallets"],
    }),

    depositWallet: builder.mutation<ApiResponse<WalletTransaction>, { id: number; body: WalletDepositWithdrawRequest }>({
      query: ({ id, body }) => ({
        url: `/wallets/${id}/deposit`,
        method: "POST",
        body,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: "Wallets", id },
        "Wallets",
        "WalletTransactions",
      ],
    }),

    withdrawWallet: builder.mutation<ApiResponse<WalletTransaction>, { id: number; body: WalletDepositWithdrawRequest }>({
      query: ({ id, body }) => ({
        url: `/wallets/${id}/withdraw`,
        method: "POST",
        body,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: "Wallets", id },
        "Wallets",
        "WalletTransactions",
      ],
    }),

    getWalletTransactions: builder.query<ApiResponse<PagedWalletTransactions>, { id: number; filters?: WalletTransactionFilters }>({
      query: ({ id, filters }) => ({
        url: `/wallets/${id}/transactions`,
        params: filters,
      }),
      providesTags: (_result, _error, { id }) => [
        { type: "WalletTransactions", id },
      ],
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetWalletsQuery,
  useGetActiveWalletsQuery,
  useGetWalletByIdQuery,
  useCreateWalletMutation,
  useUpdateWalletMutation,
  useDeleteWalletMutation,
  useDepositWalletMutation,
  useWithdrawWalletMutation,
  useGetWalletTransactionsQuery,
} = walletApi;
