import { useMemo, useState } from "react";
import clsx from "clsx";
import {
  AlertCircle,
  ChefHat,
  Edit2,
  Plus,
  Scale,
  Search,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";
import {
  useDeleteRecipeMutation,
  useGetRecipesQuery,
  useLazyGetRecipeQuery,
} from "@/api/recipesApi";
import { Button, ConfirmDialog } from "@/components/common";
import { Card } from "@/components/common/Card";
import { Input } from "@/components/common/Input";
import { Loading } from "@/components/common/Loading";
import { RecipeFormModal } from "@/components/recipes/RecipeFormModal";
import { usePermission } from "@/hooks/usePermission";
import { useProducts } from "@/hooks/useProducts";
import { ProductType } from "@/types/product.types";
import { Recipe, RecipeListItem } from "@/types/recipe.types";
import { formatCurrency } from "@/utils/formatters";

type RecipeTableRow = {
  rowId: string;
  recipeId: number | null;
  productId: number;
  productName: string;
  yieldQuantity: number | null;
  totalCost: number | null;
  profitMargin: number | null;
  isActive: boolean;
  ingredientCount: number;
  hasRecipe: boolean;
};

const RecipesPage = () => {
  const [searchQuery, setSearchQuery] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [editingRecipe, setEditingRecipe] = useState<Recipe | null>(null);
  const [initialProductId, setInitialProductId] = useState<number | undefined>();
  const [deletingRecipeId, setDeletingRecipeId] = useState<number | null>(null);
  const [loadingRecipeId, setLoadingRecipeId] = useState<number | null>(null);
  const [showActiveOnly, setShowActiveOnly] = useState(false);

  const { hasPermission } = usePermission();
  const canManageRecipes = hasPermission("RecipesManage");
  const { products, isLoading: isLoadingProducts } = useProducts();

  const { data: recipesData, isLoading: isLoadingRecipes } = useGetRecipesQuery();
  const [loadRecipe] = useLazyGetRecipeQuery();
  const [deleteMutation, { isLoading: isDeleting }] = useDeleteRecipeMutation();

  const recipes = recipesData?.data ?? [];
  const usedProductIds = useMemo(
    () => recipes.map((recipe: RecipeListItem) => recipe.productId),
    [recipes],
  );

  const manufacturedProducts = useMemo(
    () => products.filter((product) => product.type === ProductType.Manufactured),
    [products],
  );

  const recipeRows = useMemo<RecipeTableRow[]>(() => {
    const rowsFromRecipes = recipes.map((recipe: RecipeListItem) => ({
      rowId: `recipe-${recipe.id}`,
      recipeId: recipe.id,
      productId: recipe.productId,
      productName: recipe.productName,
      yieldQuantity: recipe.yieldQuantity,
      totalCost: recipe.totalCost,
      profitMargin: recipe.profitMargin,
      isActive: recipe.isActive,
      ingredientCount: recipe.ingredientCount,
      hasRecipe: true,
    }));

    const productIdsWithRecipes = new Set(rowsFromRecipes.map((row) => row.productId));
    const rowsWithoutRecipes = manufacturedProducts
      .filter((product) => !productIdsWithRecipes.has(product.id))
      .map((product) => ({
        rowId: `missing-${product.id}`,
        recipeId: null,
        productId: product.id,
        productName: product.name,
        yieldQuantity: null,
        totalCost: null,
        profitMargin: null,
        isActive: product.isActive,
        ingredientCount: 0,
        hasRecipe: false,
      }));

    return [...rowsFromRecipes, ...rowsWithoutRecipes].sort((a, b) =>
      a.productName.localeCompare(b.productName, "ar"),
    );
  }, [manufacturedProducts, recipes]);

  const filteredRecipes = recipeRows.filter((recipe) => {
    const normalizedSearch = searchQuery.trim().toLowerCase();
    const matchesSearch =
      normalizedSearch === "" ||
      recipe.productName.toLowerCase().includes(normalizedSearch);
    const matchesActive = !showActiveOnly || recipe.isActive;
    return matchesSearch && matchesActive;
  });

  const handleEdit = async (recipe: RecipeTableRow) => {
    if (!canManageRecipes) {
      toast.error("ليس لديك صلاحية إدارة الوصفات");
      return;
    }

    if (recipe.recipeId === null) {
      return;
    }

    try {
      setLoadingRecipeId(recipe.recipeId);
      const response = await loadRecipe(recipe.recipeId).unwrap();
      if (!response.data) {
        toast.error("لم يتم العثور على الوصفة");
        return;
      }

      setEditingRecipe(response.data);
      setInitialProductId(undefined);
      setShowForm(true);
    } catch {
      toast.error("تعذر تحميل تفاصيل الوصفة");
    } finally {
      setLoadingRecipeId(null);
    }
  };

  const handleCreateForProduct = (productId?: number) => {
    if (!canManageRecipes) {
      toast.error("ليس لديك صلاحية إدارة الوصفات");
      return;
    }

    setEditingRecipe(null);
    setInitialProductId(productId);
    setShowForm(true);
  };

  const handleDeleteClick = (id: number) => {
    if (!canManageRecipes) {
      toast.error("ليس لديك صلاحية إدارة الوصفات");
      return;
    }

    setDeletingRecipeId(id);
  };

  const handleConfirmDelete = async () => {
    if (deletingRecipeId === null) {
      return;
    }

    try {
      await deleteMutation(deletingRecipeId).unwrap();
      toast.success("تم حذف الوصفة بنجاح");
    } finally {
      setDeletingRecipeId(null);
    }
  };

  const handleCloseForm = () => {
    setShowForm(false);
    setEditingRecipe(null);
    setInitialProductId(undefined);
  };

  if (isLoadingRecipes || isLoadingProducts) {
    return <Loading />;
  }

  const activeCount = recipes.filter((recipe: RecipeListItem) => recipe.isActive).length;
  const totalIngredients = recipes.reduce(
    (sum: number, recipe: RecipeListItem) => sum + recipe.ingredientCount,
    0,
  );
  const missingRecipeCount = recipeRows.filter((row) => !row.hasRecipe).length;

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="mx-auto max-w-7xl space-y-4 px-4 py-5 sm:space-y-6 sm:px-6 sm:py-6 lg:px-8 lg:py-8">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="mb-2 flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary-100">
                <ChefHat className="h-5 w-5 text-primary-600" />
              </div>
              <h1 className="text-3xl font-bold text-gray-900">إدارة الوصفات</h1>
            </div>
            <p className="text-gray-600">
              إنشاء وتعديل وصفات المنتجات المصنعة وربطها بالمواد الخام.
            </p>
          </div>

          <Button
            variant="primary"
            onClick={() => handleCreateForProduct()}
            rightIcon={<Plus className="h-5 w-5" />}
            disabled={!canManageRecipes}
            className="w-full sm:w-auto"
          >
            إضافة وصفة
          </Button>
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Card className="border-blue-100">
            <p className="text-sm text-gray-600">إجمالي الوصفات</p>
            <p className="mt-1 text-2xl font-bold text-gray-900">{recipes.length}</p>
          </Card>
          <Card className="border-green-100">
            <p className="text-sm text-gray-600">الوصفات النشطة</p>
            <p className="mt-1 text-2xl font-bold text-green-700">{activeCount}</p>
          </Card>
          <Card className="border-amber-100">
            <p className="text-sm text-gray-600">إجمالي المكونات</p>
            <p className="mt-1 text-2xl font-bold text-amber-700">{totalIngredients}</p>
          </Card>
          <Card className="border-purple-100">
            <p className="text-sm text-gray-600">منتجات بدون وصفة</p>
            <p className="mt-1 text-2xl font-bold text-purple-700">{missingRecipeCount}</p>
          </Card>
        </div>

        <Card className="shrink-0">
          <div className="space-y-4">
            <div className="flex flex-col gap-4 lg:flex-row">
              <div className="flex-1">
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
                  <Input
                    placeholder="بحث عن وصفة..."
                    value={searchQuery}
                    onChange={(event) => setSearchQuery(event.target.value)}
                    className="pl-10"
                  />
                </div>
              </div>
            </div>

            <label className="flex cursor-pointer items-center gap-2">
              <input
                type="checkbox"
                checked={showActiveOnly}
                onChange={(event) => setShowActiveOnly(event.target.checked)}
                className="h-4 w-4 rounded text-primary-600 focus:ring-2 focus:ring-primary-500"
              />
              <span className="text-sm text-gray-700">نشط فقط</span>
            </label>
          </div>
        </Card>

        <Card padding="none">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px]">
              <thead>
                <tr className="border-b bg-gray-50">
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">#</th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    المنتج المصنع
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    الإنتاج
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    التكلفة
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    هامش الربح
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    المكونات
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    الحالة
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    الإجراءات
                  </th>
                </tr>
              </thead>
              <tbody>
                {filteredRecipes.map((recipe, index) => (
                  <tr
                    key={recipe.rowId}
                    className="border-b transition-colors hover:bg-gray-50"
                  >
                    <td className="px-4 py-3 text-gray-500">{index + 1}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary-100">
                          <ChefHat className="h-5 w-5 text-primary-600" />
                        </div>
                        <span className="font-medium">{recipe.productName}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      <span className="inline-flex items-center gap-1">
                        <Scale className="h-4 w-4 text-gray-400" />
                        {recipe.yieldQuantity ?? "-"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span className="font-semibold text-gray-900">
                        {recipe.totalCost !== null ? formatCurrency(recipe.totalCost) : "-"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      {recipe.profitMargin !== null ? (
                        <span
                          className={clsx(
                            "rounded-full px-2.5 py-1 text-xs font-medium",
                            recipe.profitMargin >= 0
                              ? "bg-success-50 text-success-600"
                              : "bg-danger-50 text-danger-600",
                          )}
                        >
                          {recipe.profitMargin >= 0 ? "+" : ""}
                          {formatCurrency(recipe.profitMargin)}
                        </span>
                      ) : (
                        <span className="text-sm text-gray-400">-</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      <span className="inline-flex items-center gap-1">
                        <AlertCircle className="h-4 w-4 text-gray-400" />
                        {recipe.hasRecipe ? `${recipe.ingredientCount} مكون` : "بدون وصفة"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={clsx(
                          "rounded-full px-2.5 py-0.5 text-xs font-medium",
                          !recipe.hasRecipe
                            ? "bg-amber-50 text-amber-700"
                            : recipe.isActive
                              ? "bg-success-50 text-success-500"
                              : "bg-gray-100 text-gray-500",
                        )}
                      >
                        {!recipe.hasRecipe
                          ? "بدون وصفة"
                          : recipe.isActive
                            ? "نشط"
                            : "غير نشط"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        {canManageRecipes && recipe.hasRecipe && recipe.recipeId !== null && (
                          <>
                            <button
                              onClick={() => handleEdit(recipe)}
                              disabled={loadingRecipeId === recipe.recipeId}
                              className="rounded-lg p-2 transition-colors hover:bg-gray-100 disabled:cursor-wait disabled:opacity-50"
                              title="تعديل الوصفة"
                            >
                              <Edit2 className="h-4 w-4 text-gray-500" />
                            </button>
                            <button
                              onClick={() => handleDeleteClick(recipe.recipeId!)}
                              disabled={isDeleting}
                              className="rounded-lg p-2 transition-colors hover:bg-danger-50 disabled:opacity-50"
                              title="حذف الوصفة"
                            >
                              <Trash2 className="h-4 w-4 text-danger-500" />
                            </button>
                          </>
                        )}
                        {canManageRecipes && !recipe.hasRecipe && (
                          <button
                            onClick={() => handleCreateForProduct(recipe.productId)}
                            className="inline-flex items-center gap-1 rounded-lg bg-primary-50 px-3 py-2 text-sm font-medium text-primary-700 transition-colors hover:bg-primary-100"
                          >
                            <Plus className="h-4 w-4" />
                            إضافة وصفة
                          </button>
                        )}
                        {!canManageRecipes && (
                          <span className="text-xs text-gray-400">عرض فقط</span>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {filteredRecipes.length === 0 && (
              <div className="py-12 text-center text-gray-400">
                <ChefHat className="mx-auto mb-3 h-12 w-12" />
                <p>لا توجد وصفات</p>
              </div>
            )}
          </div>
        </Card>

        {showForm && (
          <RecipeFormModal
            recipe={editingRecipe}
            initialProductId={initialProductId}
            usedProductIds={usedProductIds}
            onClose={handleCloseForm}
          />
        )}

        <ConfirmDialog
          open={deletingRecipeId !== null}
          onOpenChange={(open) => !open && setDeletingRecipeId(null)}
          onConfirm={handleConfirmDelete}
          title="حذف الوصفة"
          description="هل أنت متأكد من حذف هذه الوصفة؟"
          isLoading={isDeleting}
        />
      </div>
    </div>
  );
};

export default RecipesPage;
