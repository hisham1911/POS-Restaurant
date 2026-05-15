import { baseApi } from "./baseApi";
import {
  Recipe,
  RecipeListItem,
  CreateRecipeRequest,
  UpdateRecipeRequest,
} from "../types/recipe.types";
import { ApiResponse } from "../types/api.types";

export const recipesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getRecipes: builder.query<ApiResponse<RecipeListItem[]>, void>({
      query: () => "/recipes",
      providesTags: (result) => {
        const items = result?.data ?? [];
        return [
          ...items.map(({ id }: { id: number }) => ({
            type: "Recipes" as const,
            id,
          })),
          { type: "Recipes" as const, id: "LIST" },
        ];
      },
    }),

    getRecipe: builder.query<ApiResponse<Recipe | null>, number>({
      query: (id) => `/recipes/${id}`,
      providesTags: (_result, _error, id) => [{ type: "Recipes", id }],
    }),

    getRecipeByProductId: builder.query<ApiResponse<Recipe | null>, number>({
      query: (productId) => `/recipes/product/${productId}`,
      providesTags: (_result, _error, productId) => [
        { type: "Recipes", id: `product-${productId}` },
      ],
    }),

    getRecipeCost: builder.query<ApiResponse<number>, number>({
      query: (id) => `/recipes/${id}/cost`,
      providesTags: (_result, _error, id) => [
        { type: "Recipes", id: `cost-${id}` },
      ],
    }),

    createRecipe: builder.mutation<ApiResponse<Recipe>, CreateRecipeRequest>({
      query: (recipe) => ({
        url: "/recipes",
        method: "POST",
        body: recipe,
      }),
      invalidatesTags: [{ type: "Recipes", id: "LIST" }],
    }),

    updateRecipe: builder.mutation<
      ApiResponse<Recipe>,
      { id: number; data: UpdateRecipeRequest }
    >({
      query: ({ id, data }) => ({
        url: `/recipes/${id}`,
        method: "PUT",
        body: data,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: "Recipes", id },
        { type: "Recipes", id: "LIST" },
      ],
    }),

    deleteRecipe: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/recipes/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: [{ type: "Recipes", id: "LIST" }],
    }),
  }),
});

export const {
  useGetRecipesQuery,
  useGetRecipeQuery,
  useLazyGetRecipeQuery,
  useGetRecipeByProductIdQuery,
  useGetRecipeCostQuery,
  useCreateRecipeMutation,
  useUpdateRecipeMutation,
  useDeleteRecipeMutation,
} = recipesApi;
