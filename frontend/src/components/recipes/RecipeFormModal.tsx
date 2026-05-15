import { useEffect, useMemo, useState } from "react";
import clsx from "clsx";
import { ChefHat, Clock, Flame, Plus, Scale, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import { useProducts } from "@/hooks/useProducts";
import { useRecipes } from "@/hooks/useRecipes";
import { Product, ProductType, UnitOfMeasure } from "@/types/product.types";
import {
  CreateRecipeIngredientRequest,
  CreateRecipeRequest,
  Recipe,
  UpdateRecipeRequest,
} from "@/types/recipe.types";

interface RecipeFormModalProps {
  recipe: Recipe | null;
  initialProductId?: number;
  usedProductIds?: number[];
  onClose: () => void;
}

const UNIT_LABELS: Record<UnitOfMeasure, string> = {
  [UnitOfMeasure.Piece]: "قطعة",
  [UnitOfMeasure.Kilogram]: "كيلوجرام",
  [UnitOfMeasure.Gram]: "جرام",
  [UnitOfMeasure.Liter]: "لتر",
  [UnitOfMeasure.Milliliter]: "ملليلتر",
  [UnitOfMeasure.Meter]: "متر",
  [UnitOfMeasure.Box]: "صندوق",
  [UnitOfMeasure.Portion]: "حصة",
};

const EMPTY_INGREDIENT: CreateRecipeIngredientRequest = {
  rawMaterialProductId: 0,
  quantity: 0,
  unit: UnitOfMeasure.Piece,
};

const getCompatibleUnits = (unit: UnitOfMeasure): UnitOfMeasure[] => {
  if (unit === UnitOfMeasure.Kilogram || unit === UnitOfMeasure.Gram) {
    return [UnitOfMeasure.Kilogram, UnitOfMeasure.Gram];
  }

  if (unit === UnitOfMeasure.Liter || unit === UnitOfMeasure.Milliliter) {
    return [UnitOfMeasure.Liter, UnitOfMeasure.Milliliter];
  }

  return [unit];
};

const getIngredientUnitOptions = (rawMaterial?: Product): UnitOfMeasure[] =>
  rawMaterial ? getCompatibleUnits(rawMaterial.unit) : Object.keys(UNIT_LABELS).map(Number) as UnitOfMeasure[];

const toIngredientFormData = (recipe: Recipe | null): CreateRecipeIngredientRequest[] => {
  if (!recipe?.ingredients?.length) {
    return [{ ...EMPTY_INGREDIENT }];
  }

  return recipe.ingredients.map((ingredient) => ({
    rawMaterialProductId: ingredient.rawMaterialProductId,
    quantity: ingredient.quantity,
    unit: ingredient.unit,
    notes: ingredient.notes ?? "",
  }));
};

export const RecipeFormModal = ({
  recipe,
  initialProductId,
  usedProductIds = [],
  onClose,
}: RecipeFormModalProps) => {
  const { products } = useProducts();
  const { createRecipe, updateRecipe, isCreating, isUpdating } = useRecipes();
  const isEditing = !!recipe;
  const isLoading = isCreating || isUpdating;

  const usedProductIdSet = useMemo(() => new Set(usedProductIds), [usedProductIds]);

  const manufacturedProducts = useMemo(
    () =>
      products.filter(
        (product) =>
          product.type === ProductType.Manufactured &&
          (isEditing || !usedProductIdSet.has(product.id) || product.id === initialProductId),
      ),
    [initialProductId, isEditing, products, usedProductIdSet],
  );

  const rawMaterialProducts = useMemo(
    () => products.filter((product) => product.type === ProductType.RawMaterial),
    [products],
  );

  const rawMaterialsById = useMemo(
    () => new Map(rawMaterialProducts.map((product) => [product.id, product])),
    [rawMaterialProducts],
  );

  const [formData, setFormData] = useState({
    productId: recipe?.productId ?? initialProductId ?? 0,
    yieldQuantity: recipe?.yieldQuantity ?? 1,
    preparationTimeMinutes: recipe?.preparationTimeMinutes ?? undefined,
    cookingTimeMinutes: recipe?.cookingTimeMinutes ?? undefined,
    instructions: recipe?.instructions ?? "",
    autoDeductIngredients: recipe?.autoDeductIngredients ?? true,
    isActive: recipe?.isActive ?? true,
    ingredients: toIngredientFormData(recipe),
  });

  useEffect(() => {
    if (isEditing) {
      return;
    }

    const nextProductId = initialProductId ?? manufacturedProducts[0]?.id ?? 0;
    setFormData((prev) =>
      prev.productId === nextProductId ? prev : { ...prev, productId: nextProductId },
    );
  }, [initialProductId, isEditing, manufacturedProducts]);

  const addIngredient = () => {
    setFormData((prev) => ({
      ...prev,
      ingredients: [...prev.ingredients, { ...EMPTY_INGREDIENT }],
    }));
  };

  const removeIngredient = (index: number) => {
    setFormData((prev) => ({
      ...prev,
      ingredients: prev.ingredients.filter((_, currentIndex) => currentIndex !== index),
    }));
  };

  const updateIngredient = (
    index: number,
    field: keyof CreateRecipeIngredientRequest,
    value: CreateRecipeIngredientRequest[keyof CreateRecipeIngredientRequest],
  ) => {
    setFormData((prev) => {
      const ingredients = [...prev.ingredients];
      ingredients[index] = { ...ingredients[index], [field]: value };
      return { ...prev, ingredients };
    });
  };

  const handleRawMaterialChange = (index: number, rawMaterialProductId: number) => {
    const rawMaterial = rawMaterialsById.get(rawMaterialProductId);
    setFormData((prev) => {
      const ingredients = [...prev.ingredients];
      ingredients[index] = {
        ...ingredients[index],
        rawMaterialProductId,
        unit: rawMaterial?.unit ?? UnitOfMeasure.Piece,
      };
      return { ...prev, ingredients };
    });
  };

  const validateForm = () => {
    if (formData.productId <= 0) {
      toast.error("اختر المنتج المصنع أولا");
      return false;
    }

    if (formData.yieldQuantity <= 0) {
      toast.error("كمية الإنتاج يجب أن تكون أكبر من صفر");
      return false;
    }

    const validIngredients = formData.ingredients.filter(
      (ingredient) => ingredient.rawMaterialProductId > 0 && ingredient.quantity > 0,
    );

    if (validIngredients.length !== formData.ingredients.length || validIngredients.length === 0) {
      toast.error("أكمل اختيار المواد الخام والكميات قبل الحفظ");
      return false;
    }

    const uniqueIngredients = new Set(validIngredients.map((ingredient) => ingredient.rawMaterialProductId));
    if (uniqueIngredients.size !== validIngredients.length) {
      toast.error("لا يمكن تكرار نفس المادة الخام في الوصفة");
      return false;
    }

    const hasInvalidUnit = validIngredients.some((ingredient) => {
      const rawMaterial = rawMaterialsById.get(ingredient.rawMaterialProductId);
      return !rawMaterial || !getCompatibleUnits(rawMaterial.unit).includes(ingredient.unit);
    });

    if (hasInvalidUnit) {
      toast.error("راجع وحدات المكونات. كل وحدة يجب أن تكون متوافقة مع وحدة مخزون المادة الخام");
      return false;
    }

    return true;
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!validateForm()) {
      return;
    }

    const ingredients = formData.ingredients.map((ingredient) => ({
      ...ingredient,
      notes: ingredient.notes?.trim() || undefined,
    }));

    try {
      if (isEditing && recipe) {
        const updateData: UpdateRecipeRequest = {
          yieldQuantity: formData.yieldQuantity,
          preparationTimeMinutes: formData.preparationTimeMinutes,
          cookingTimeMinutes: formData.cookingTimeMinutes,
          instructions: formData.instructions.trim() || undefined,
          autoDeductIngredients: formData.autoDeductIngredients,
          isActive: formData.isActive,
          ingredients,
        };
        await updateRecipe(recipe.id, updateData);
      } else {
        const createData: CreateRecipeRequest = {
          productId: formData.productId,
          yieldQuantity: formData.yieldQuantity,
          preparationTimeMinutes: formData.preparationTimeMinutes,
          cookingTimeMinutes: formData.cookingTimeMinutes,
          instructions: formData.instructions.trim() || undefined,
          autoDeductIngredients: formData.autoDeductIngredients,
          ingredients,
        };
        await createRecipe(createData);
      }

      onClose();
    } catch {
      // baseApi handles the visible error toast.
    }
  };

  const canSubmit =
    !isLoading &&
    formData.productId > 0 &&
    rawMaterialProducts.length > 0 &&
    formData.ingredients.length > 0;

  return (
    <Modal
      isOpen={true}
      onClose={onClose}
      title={isEditing ? "تعديل وصفة" : "إضافة وصفة جديدة"}
      size="lg"
    >
      <form onSubmit={handleSubmit} className="space-y-6">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-gray-700">
            المنتج المصنع
          </label>
          <select
            value={formData.productId}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                productId: Number(event.target.value),
              }))
            }
            disabled={isEditing || isLoading}
            className="w-full rounded-lg border border-gray-300 px-4 py-2.5 outline-none transition-all focus:border-transparent focus:ring-2 focus:ring-primary-500"
          >
            <option value={0}>اختر منتج مصنع...</option>
            {manufacturedProducts.map((product) => (
              <option key={product.id} value={product.id}>
                {product.name}
              </option>
            ))}
          </select>
          {!isEditing && manufacturedProducts.length === 0 && (
            <p className="mt-1 text-sm text-warning-600">
              لا توجد منتجات مصنعة بدون وصفة. أنشئ منتجا من نوع منتج مصنع أولا.
            </p>
          )}
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">
              <span className="inline-flex items-center gap-1.5">
                <Scale className="h-4 w-4 text-gray-400" />
                كمية الإنتاج
              </span>
            </label>
            <Input
              type="number"
              step="0.01"
              min="0.01"
              required
              value={formData.yieldQuantity}
              onChange={(event) =>
                setFormData((prev) => ({
                  ...prev,
                  yieldQuantity: Number(event.target.value),
                }))
              }
              disabled={isLoading}
            />
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">
              <span className="inline-flex items-center gap-1.5">
                <Clock className="h-4 w-4 text-gray-400" />
                وقت التحضير (د)
              </span>
            </label>
            <Input
              type="number"
              min="0"
              value={formData.preparationTimeMinutes ?? ""}
              onChange={(event) =>
                setFormData((prev) => ({
                  ...prev,
                  preparationTimeMinutes: event.target.value ? Number(event.target.value) : undefined,
                }))
              }
              disabled={isLoading}
            />
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">
              <span className="inline-flex items-center gap-1.5">
                <Flame className="h-4 w-4 text-gray-400" />
                وقت الطبخ (د)
              </span>
            </label>
            <Input
              type="number"
              min="0"
              value={formData.cookingTimeMinutes ?? ""}
              onChange={(event) =>
                setFormData((prev) => ({
                  ...prev,
                  cookingTimeMinutes: event.target.value ? Number(event.target.value) : undefined,
                }))
              }
              disabled={isLoading}
            />
          </div>
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-gray-700">
            خطوات التحضير / التعليمات
          </label>
          <textarea
            value={formData.instructions}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                instructions: event.target.value,
              }))
            }
            rows={4}
            disabled={isLoading}
            className="w-full resize-none rounded-lg border border-gray-300 px-4 py-2.5 outline-none transition-all focus:border-transparent focus:ring-2 focus:ring-primary-500"
            placeholder="اكتب خطوات تحضير الوصفة..."
          />
        </div>

        <div className="flex flex-col gap-4 sm:flex-row">
          <label className="flex cursor-pointer items-center gap-2">
            <input
              type="checkbox"
              checked={formData.autoDeductIngredients}
              onChange={(event) =>
                setFormData((prev) => ({
                  ...prev,
                  autoDeductIngredients: event.target.checked,
                }))
              }
              disabled={isLoading}
              className="h-4 w-4 rounded text-primary-600 focus:ring-2 focus:ring-primary-500"
            />
            <span className="text-sm text-gray-700">خصم المكونات تلقائيا عند البيع</span>
          </label>

          {isEditing && (
            <label className="flex cursor-pointer items-center gap-2">
              <input
                type="checkbox"
                checked={formData.isActive}
                onChange={(event) =>
                  setFormData((prev) => ({
                    ...prev,
                    isActive: event.target.checked,
                  }))
                }
                disabled={isLoading}
                className="h-4 w-4 rounded text-primary-600 focus:ring-2 focus:ring-primary-500"
              />
              <span className="text-sm text-gray-700">نشط</span>
            </label>
          )}
        </div>

        <div className="border-t border-gray-200 pt-4">
          <div className="mb-3 flex items-center justify-between">
            <h3 className="flex items-center gap-2 font-semibold text-gray-800">
              <ChefHat className="h-5 w-5 text-primary-600" />
              المكونات
            </h3>
            <Button
              type="button"
              variant="outline"
              size="sm"
              leftIcon={<Plus className="h-4 w-4" />}
              onClick={addIngredient}
              disabled={isLoading || rawMaterialProducts.length === 0}
            >
              إضافة مكون
            </Button>
          </div>

          {rawMaterialProducts.length === 0 && (
            <div className="rounded-lg bg-warning-50 p-4 text-sm text-warning-700">
              لا توجد مواد خام متاحة. أنشئ منتجات من نوع مادة خام أولا.
            </div>
          )}

          <div className="space-y-3">
            {formData.ingredients.map((ingredient, index) => {
              const rawMaterial = rawMaterialsById.get(ingredient.rawMaterialProductId);
              const unitOptions = getIngredientUnitOptions(rawMaterial);
              const selectedIds = new Set(
                formData.ingredients
                  .filter((_, currentIndex) => currentIndex !== index)
                  .map((currentIngredient) => currentIngredient.rawMaterialProductId),
              );

              return (
                <div
                  key={index}
                  className="rounded-xl border border-gray-200 bg-white p-3 shadow-sm"
                >
                  <div className="mb-3 flex items-center justify-between gap-3">
                    <span className="text-sm font-semibold text-gray-700">
                      مكون {index + 1}
                    </span>
                    <button
                      type="button"
                      onClick={() => removeIngredient(index)}
                      disabled={isLoading || formData.ingredients.length <= 1}
                      className={clsx(
                        "rounded-lg border border-transparent p-2 transition-colors",
                        formData.ingredients.length <= 1
                          ? "cursor-not-allowed text-gray-300"
                          : "text-danger-500 hover:border-danger-100 hover:bg-danger-50",
                      )}
                      title="حذف المكون"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>

                  <div className="grid grid-cols-1 gap-3 md:grid-cols-12">
                    <div className="md:col-span-5">
                      <label className="mb-1 block text-xs font-medium text-gray-600">
                        المادة الخام
                      </label>
                      <select
                        value={ingredient.rawMaterialProductId}
                        onChange={(event) => handleRawMaterialChange(index, Number(event.target.value))}
                        disabled={isLoading}
                        className="h-11 w-full rounded-lg border border-gray-300 bg-white px-3 text-sm outline-none focus:border-transparent focus:ring-2 focus:ring-primary-500"
                      >
                        <option value={0}>اختر المادة الخام...</option>
                        {rawMaterialProducts.map((product) => (
                          <option
                            key={product.id}
                            value={product.id}
                            disabled={selectedIds.has(product.id)}
                          >
                            {product.name}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="md:col-span-3">
                      <label className="mb-1 block text-xs font-medium text-gray-600">
                        الكمية
                      </label>
                      <Input
                        type="number"
                        step="0.01"
                        min="0.01"
                        value={ingredient.quantity}
                        onChange={(event) =>
                          updateIngredient(index, "quantity", Number(event.target.value))
                        }
                        disabled={isLoading}
                      />
                    </div>

                    <div className="md:col-span-4">
                      <label className="mb-1 block text-xs font-medium text-gray-600">
                        الوحدة
                      </label>
                      <select
                        value={ingredient.unit}
                        onChange={(event) =>
                          updateIngredient(index, "unit", Number(event.target.value) as UnitOfMeasure)
                        }
                        disabled={isLoading || !rawMaterial}
                        className="h-11 w-full rounded-lg border border-gray-300 bg-white px-3 text-sm outline-none focus:border-transparent focus:ring-2 focus:ring-primary-500 disabled:bg-gray-50"
                      >
                        {unitOptions.map((unit) => (
                          <option key={unit} value={unit}>
                            {UNIT_LABELS[unit]}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="md:col-span-12">
                      <label className="mb-1 block text-xs font-medium text-gray-600">
                        ملاحظات
                      </label>
                      <Input
                        value={ingredient.notes ?? ""}
                        onChange={(event) => updateIngredient(index, "notes", event.target.value)}
                        disabled={isLoading}
                        placeholder="اختياري"
                      />
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        <div className="flex justify-end gap-3 border-t border-gray-200 pt-4">
          <Button type="button" variant="secondary" onClick={onClose} disabled={isLoading}>
            إلغاء
          </Button>
          <Button type="submit" variant="primary" isLoading={isLoading} disabled={!canSubmit}>
            {isEditing ? "حفظ التعديلات" : "إنشاء الوصفة"}
          </Button>
        </div>
      </form>
    </Modal>
  );
};
