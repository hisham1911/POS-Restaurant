import { baseApi } from './baseApi';
import type {
  Expense,
  CreateExpenseRequest,
  UpdateExpenseRequest,
  ApproveExpenseRequest,
  RejectExpenseRequest,
  PayExpenseRequest,
  ExpenseFilters,
  PagedExpensesResult,
} from '../types/expense.types';
import type { ApiResponse } from '../types/api.types';

export const expensesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // Get all expenses with filters and pagination
    getExpenses: builder.query<ApiResponse<PagedExpensesResult>, ExpenseFilters>({
      query: (filters) => ({
        url: '/expenses',
        params: {
          categoryId: filters.categoryId,
          status: filters.status,
          fromDate: filters.fromDate,
          toDate: filters.toDate,
          branchId: filters.branchId,
          pageNumber: filters.pageNumber || 1,
          pageSize: filters.pageSize || 20,
        },
      }),
      providesTags: ['Expenses'],
    }),

    // Get expense by ID
    getExpenseById: builder.query<ApiResponse<Expense>, number>({
      query: (id) => `/expenses/${id}`,
      providesTags: (_result, _error, id) => [{ type: 'Expense', id }],
    }),

    // Create expense
    createExpense: builder.mutation<ApiResponse<Expense>, CreateExpenseRequest>({
      query: (expense) => ({
        url: '/expenses',
        method: 'POST',
        body: expense,
      }),
      invalidatesTags: ['Expenses'],
    }),

    // Update expense
    updateExpense: builder.mutation<ApiResponse<Expense>, { id: number; expense: UpdateExpenseRequest }>({
      query: ({ id, expense }) => ({
        url: `/expenses/${id}`,
        method: 'PUT',
        body: expense,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: 'Expense', id },
        'Expenses',
      ],
    }),

    // Delete expense
    deleteExpense: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/expenses/${id}`,
        method: 'DELETE',
      }),
      invalidatesTags: ['Expenses'],
    }),

    // Approve expense
    approveExpense: builder.mutation<ApiResponse<Expense>, { id: number; request: ApproveExpenseRequest }>({
      query: ({ id, request }) => ({
        url: `/expenses/${id}/approve`,
        method: 'POST',
        body: request,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: 'Expense', id },
        'Expenses',
      ],
    }),

    // Reject expense
    rejectExpense: builder.mutation<ApiResponse<Expense>, { id: number; request: RejectExpenseRequest }>({
      query: ({ id, request }) => ({
        url: `/expenses/${id}/reject`,
        method: 'POST',
        body: request,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: 'Expense', id },
        'Expenses',
      ],
    }),

    // Pay expense
    payExpense: builder.mutation<ApiResponse<Expense>, { id: number; request: PayExpenseRequest }>({
      query: ({ id, request }) => ({
        url: `/expenses/${id}/pay`,
        method: 'POST',
        body: request,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: 'Expense', id },
        'Expenses',
      ],
    }),

    // Upload attachment
    uploadAttachment: builder.mutation<ApiResponse<Expense>, { id: number; file: File }>({
      query: ({ id, file }) => {
        const formData = new FormData();
        formData.append('file', file);
        return {
          url: `/expenses/${id}/attachments`,
          method: 'POST',
          body: formData,
        };
      },
      invalidatesTags: (_result, _error, { id }) => [{ type: 'Expense', id }],
    }),

    // Delete attachment
    deleteAttachment: builder.mutation<ApiResponse<boolean>, { expenseId: number; attachmentId: number }>({
      query: ({ expenseId, attachmentId }) => ({
        url: `/expenses/${expenseId}/attachments/${attachmentId}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { expenseId }) => [{ type: 'Expense', id: expenseId }],
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetExpensesQuery,
  useGetExpenseByIdQuery,
  useCreateExpenseMutation,
  useUpdateExpenseMutation,
  useDeleteExpenseMutation,
  useApproveExpenseMutation,
  useRejectExpenseMutation,
  usePayExpenseMutation,
  useUploadAttachmentMutation,
  useDeleteAttachmentMutation,
} = expensesApi;
