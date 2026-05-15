import { baseApi } from "./baseApi";
import type { ApiResponse } from "@/types/api.types";
import type {
  CreateSavedOrderNoteRequest,
  SavedOrderNote,
  UpdateSavedOrderNoteRequest,
} from "@/types/restaurant.types";

export const savedOrderNotesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getSavedOrderNotes: builder.query<ApiResponse<SavedOrderNote[]>, number | void>({
      query: (branchId) => ({
        url: "/saved-order-notes",
        params: branchId ? { branchId } : undefined,
      }),
      providesTags: ["SavedOrderNotes"],
    }),
    createSavedOrderNote: builder.mutation<
      ApiResponse<SavedOrderNote>,
      CreateSavedOrderNoteRequest
    >({
      query: (body) => ({
        url: "/saved-order-notes",
        method: "POST",
        body,
      }),
      invalidatesTags: ["SavedOrderNotes"],
    }),
    updateSavedOrderNote: builder.mutation<
      ApiResponse<SavedOrderNote>,
      { id: number; body: UpdateSavedOrderNoteRequest }
    >({
      query: ({ id, body }) => ({
        url: `/saved-order-notes/${id}`,
        method: "PUT",
        body,
      }),
      invalidatesTags: ["SavedOrderNotes"],
    }),
    deleteSavedOrderNote: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/saved-order-notes/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: ["SavedOrderNotes"],
    }),
  }),
});

export const {
  useGetSavedOrderNotesQuery,
  useCreateSavedOrderNoteMutation,
  useUpdateSavedOrderNoteMutation,
  useDeleteSavedOrderNoteMutation,
} = savedOrderNotesApi;
