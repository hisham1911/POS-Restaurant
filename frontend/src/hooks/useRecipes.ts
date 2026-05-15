import {
  useCreateRecipeMutation,
  useDeleteRecipeMutation,
  useGetRecipesQuery,
  useUpdateRecipeMutation,
} from "@/api/recipesApi";
import type {
  CreateRecipeRequest,
  UpdateRecipeRequest,
} from "@/types/recipe.types";
import { toast } from "sonner";

export const useRecipes = () => {
  const {
    data: recipesData,
    isLoading,
    isError,
    refetch,
  } = useGetRecipesQuery();

  const [createMutation, { isLoading: isCreating }] = useCreateRecipeMutation();
  const [updateMutation, { isLoading: isUpdating }] = useUpdateRecipeMutation();
  const [deleteMutation, { isLoading: isDeleting }] = useDeleteRecipeMutation();

  const recipes = recipesData?.data ?? [];

  const createRecipe = async (data: CreateRecipeRequest) => {
    try {
      const response = await createMutation(data).unwrap();
      toast.success("تم إضافة الوصفة بنجاح");
      return response;
    } catch {
      throw new Error("Failed to create recipe");
    }
  };

  const updateRecipe = async (id: number, data: UpdateRecipeRequest) => {
    try {
      const response = await updateMutation({ id, data }).unwrap();
      toast.success("تم تحديث الوصفة بنجاح");
      return response;
    } catch {
      throw new Error("Failed to update recipe");
    }
  };

  const deleteRecipe = async (id: number) => {
    try {
      await deleteMutation(id).unwrap();
      toast.success("تم حذف الوصفة بنجاح");
    } catch {
      throw new Error("Failed to delete recipe");
    }
  };

  return {
    recipes,
    isLoading,
    isError,
    refetch,
    createRecipe,
    updateRecipe,
    deleteRecipe,
    isCreating,
    isUpdating,
    isDeleting,
  };
};
