import { UnitOfMeasure } from "./product.types";

export interface Recipe {
  id: number;
  productId: number;
  productName: string;
  yieldQuantity: number;
  preparationTimeMinutes?: number;
  cookingTimeMinutes?: number;
  instructions?: string;
  totalCost: number;
  autoDeductIngredients: boolean;
  isActive: boolean;
  profitMargin: number;
  ingredients: RecipeIngredient[];
}

export interface RecipeIngredient {
  id: number;
  rawMaterialProductId: number;
  rawMaterialName: string;
  quantity: number;
  unit: UnitOfMeasure;
  unitName: string;
  cost: number;
  notes?: string;
}

export interface RecipeListItem {
  id: number;
  productId: number;
  productName: string;
  yieldQuantity: number;
  totalCost: number;
  profitMargin: number;
  isActive: boolean;
  ingredientCount: number;
}

export interface CreateRecipeRequest {
  productId: number;
  yieldQuantity: number;
  preparationTimeMinutes?: number;
  cookingTimeMinutes?: number;
  instructions?: string;
  autoDeductIngredients?: boolean;
  ingredients: CreateRecipeIngredientRequest[];
}

export interface CreateRecipeIngredientRequest {
  rawMaterialProductId: number;
  quantity: number;
  unit: UnitOfMeasure;
  notes?: string;
}

export interface UpdateRecipeRequest {
  yieldQuantity: number;
  preparationTimeMinutes?: number;
  cookingTimeMinutes?: number;
  instructions?: string;
  autoDeductIngredients?: boolean;
  isActive: boolean;
  ingredients: CreateRecipeIngredientRequest[];
}
