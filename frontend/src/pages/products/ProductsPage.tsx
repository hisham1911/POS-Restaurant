import { useMemo, useState } from "react";
import clsx from "clsx";
import {
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Edit2,
  Package,
  Plus,
  Search,
  Trash2,
} from "lucide-react";
import { toast } from "react-hot-toast";
import {
  useDeleteProductMutation,
  useGetProductsQuery,
} from "@/api/productsApi";
import { useGetBranchInventoryQuery } from "@/api/inventoryApi";
import { Button, ConfirmDialog } from "@/components/common";
import { Card } from "@/components/common/Card";
import { Input } from "@/components/common/Input";
import { Loading } from "@/components/common/Loading";
import { ProductFormModal } from "@/components/products/ProductFormModal";
import { usePermission } from "@/hooks/usePermission";
import { useCategories } from "@/hooks/useProducts";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentBranch } from "@/store/slices/branchSlice";
import type { Product } from "@/types/product.types";
import { formatCurrency } from "@/utils/formatters";
import {
  buildBranchInventoryStockMap,
  getProductCurrentStock,
} from "@/utils/productStock";

const PAGE_SIZE = 20;

export const ProductsPage = () => {
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [showActiveOnly, setShowActiveOnly] = useState(false);
  const [showLowStockOnly, setShowLowStockOnly] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [editingProduct, setEditingProduct] = useState<Product | null>(null);
  const [deletingProductId, setDeletingProductId] = useState<number | null>(
    null,
  );
  const [currentPage, setCurrentPage] = useState(1);

  const { categories } = useCategories();
  const { hasPermission } = usePermission();
  const currentBranch = useAppSelector(selectCurrentBranch);
  const canManageProducts = hasPermission("ProductsManage");
  const canAdjustStock = hasPermission("InventoryManage");

  const { data: branchInventory, isLoading: isInventoryLoading } =
    useGetBranchInventoryQuery(currentBranch?.id ?? 0, {
      skip: !currentBranch?.id,
    });

  const stockByProductId = useMemo(
    () => buildBranchInventoryStockMap(branchInventory),
    [branchInventory],
  );
  const hasInventorySnapshot = Array.isArray(branchInventory);

  const getBranchStock = (product: Product) =>
    hasInventorySnapshot
      ? getProductCurrentStock(product, stockByProductId)
      : 0;

  const queryParams = useMemo(
    () => ({
      categoryId: selectedCategory ?? undefined,
      search: searchQuery.trim() || undefined,
      isActive: showActiveOnly ? true : undefined,
      lowStock: showLowStockOnly ? true : undefined,
      page: currentPage,
      pageSize: PAGE_SIZE,
    }),
    [
      currentPage,
      searchQuery,
      selectedCategory,
      showActiveOnly,
      showLowStockOnly,
    ],
  );

  const { data: productsData, isLoading } = useGetProductsQuery(queryParams);
  const [deleteMutation, { isLoading: isDeleting }] =
    useDeleteProductMutation();

  const products = productsData?.data?.items ?? [];
  const totalCount = productsData?.data?.totalCount ?? products.length;
  const totalPages = productsData?.data?.totalPages ?? 1;

  const filteredProducts = useMemo(() => products, [products]);

  const handleEdit = (product: Product) => {
    if (!canManageProducts) {
      toast.error("ليس لديك صلاحية إدارة المنتجات");
      return;
    }

    setEditingProduct(product);
    setShowForm(true);
  };

  const handleDeleteClick = (id: number) => {
    if (!canManageProducts) {
      toast.error("ليس لديك صلاحية إدارة المنتجات");
      return;
    }

    setDeletingProductId(id);
  };

  const handleConfirmDelete = async () => {
    if (deletingProductId === null) return;

    try {
      await deleteMutation(deletingProductId).unwrap();
      toast.success("تم حذف المنتج بنجاح");
      setDeletingProductId(null);
    } catch {
      setDeletingProductId(null);
    }
  };

  const handleCloseForm = () => {
    setShowForm(false);
    setEditingProduct(null);
  };

  if (isLoading) return <Loading />;

  const activeProducts = filteredProducts.filter(
    (product) => product.isActive,
  ).length;
  const lowStockProducts = filteredProducts.filter(
    (product) =>
      hasInventorySnapshot &&
      product.trackInventory &&
      getBranchStock(product) < (product.lowStockThreshold ?? 5),
  ).length;

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="mx-auto max-w-7xl space-y-4 px-4 py-5 sm:space-y-6 sm:px-6 sm:py-6 lg:px-8 lg:py-8">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="mb-2 flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-full bg-blue-100">
                <Package className="h-5 w-5 text-blue-600" />
              </div>
              <h1 className="text-3xl font-bold text-gray-900">
                إدارة المنتجات
              </h1>
            </div>
            <p className="text-gray-600">إضافة وتعديل وحذف المنتجات</p>
          </div>

          <Button
            variant="primary"
            onClick={() => setShowForm(true)}
            rightIcon={<Plus className="h-5 w-5" />}
            disabled={!canManageProducts}
            className="w-full sm:w-auto"
          >
            إضافة منتج
          </Button>
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          <Card className="border-blue-100">
            <p className="text-sm text-gray-600">إجمالي المنتجات</p>
            <p className="mt-1 text-2xl font-bold text-gray-900">
              {totalCount}
            </p>
          </Card>
          <Card className="border-green-100">
            <p className="text-sm text-gray-600">المنتجات النشطة</p>
            <p className="mt-1 text-2xl font-bold text-green-700">
              {activeProducts}
            </p>
          </Card>
          <Card className="border-amber-100">
            <p className="text-sm text-gray-600">مخزون منخفض</p>
            <p className="mt-1 text-2xl font-bold text-amber-700">
              {lowStockProducts}
            </p>
          </Card>
        </div>

        <Card className="shrink-0">
          <div className="space-y-4">
            <div className="flex flex-col gap-4 lg:flex-row">
              <div className="flex-1">
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
                  <Input
                    placeholder="بحث عن منتج..."
                    value={searchQuery}
                    onChange={(event) => {
                      setSearchQuery(event.target.value);
                      setCurrentPage(1);
                    }}
                    className="pl-10"
                  />
                </div>
              </div>

              <div className="relative w-full lg:w-auto">
                <select
                  value={selectedCategory || ""}
                  onChange={(event) => {
                    setSelectedCategory(
                      event.target.value ? Number(event.target.value) : null,
                    );
                    setCurrentPage(1);
                  }}
                  className="w-full appearance-none rounded-xl border border-gray-300 py-2.5 pl-10 pr-4 shadow-sm transition-all duration-200 hover:border-gray-400 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500 lg:min-w-[220px]"
                >
                  <option value="">كل التصنيفات</option>
                  {categories.map((category) => (
                    <option key={category.id} value={category.id}>
                      {category.name}
                    </option>
                  ))}
                </select>
                <ChevronDown className="pointer-events-none absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
              </div>
            </div>

            <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center">
              <label className="flex cursor-pointer items-center gap-2">
                <input
                  type="checkbox"
                  checked={showActiveOnly}
                  onChange={(event) => {
                    setShowActiveOnly(event.target.checked);
                    setCurrentPage(1);
                  }}
                  className="h-4 w-4 rounded text-primary-600 focus:ring-2 focus:ring-primary-500"
                />
                <span className="text-sm text-gray-700">نشط فقط</span>
              </label>
              <label className="flex cursor-pointer items-center gap-2">
                <input
                  type="checkbox"
                  checked={showLowStockOnly}
                  onChange={(event) => {
                    setShowLowStockOnly(event.target.checked);
                    setCurrentPage(1);
                  }}
                  className="h-4 w-4 rounded text-primary-600 focus:ring-2 focus:ring-primary-500"
                />
                <span className="text-sm text-gray-700">مخزون منخفض فقط</span>
              </label>
            </div>
          </div>
        </Card>

        <Card padding="none">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px]">
              <thead>
                <tr className="border-b bg-gray-50">
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    #
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    المنتج
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    التصنيف
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    السعر
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    الكمية
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    نقطة إعادة الطلب
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
                {filteredProducts.map((product, index) => {
                  const category = categories.find(
                    (currentCategory) =>
                      currentCategory.id === product.categoryId,
                  );
                  const branchStock = getBranchStock(product);
                  const shouldShowStockLoading =
                    isInventoryLoading && !hasInventorySnapshot;

                  return (
                    <tr
                      key={product.id}
                      className="border-b transition-colors hover:bg-gray-50"
                    >
                      <td className="px-4 py-3 text-gray-500">
                        {(currentPage - 1) * PAGE_SIZE + index + 1}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-3">
                          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-gray-100">
                            <Package className="h-5 w-5 text-gray-400" />
                          </div>
                          <span className="font-medium">{product.name}</span>
                        </div>
                      </td>
                      <td className="px-4 py-3 text-gray-600">
                        {category?.name || "-"}
                      </td>
                      <td className="px-4 py-3 font-semibold text-primary-600">
                        {formatCurrency(product.price)}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={clsx(
                            "rounded-full px-2.5 py-1 text-xs font-medium",
                            shouldShowStockLoading
                              ? "bg-gray-100 text-gray-500"
                              : branchStock <= 0
                                ? "bg-danger-50 text-danger-600"
                                : branchStock <=
                                    (product.lowStockThreshold ?? 5)
                                  ? "bg-warning-50 text-warning-600"
                                  : "bg-gray-100 text-gray-700",
                          )}
                        >
                          {shouldShowStockLoading ? "-" : branchStock}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600">
                        {product.reorderPoint ?? "-"}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={clsx(
                            "rounded-full px-2.5 py-0.5 text-xs font-medium",
                            product.isActive
                              ? "bg-success-50 text-success-500"
                              : "bg-gray-100 text-gray-500",
                          )}
                        >
                          {product.isActive ? "نشط" : "غير نشط"}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          {canManageProducts && (
                            <>
                              <button
                                onClick={() => handleEdit(product)}
                                className="rounded-lg p-2 transition-colors hover:bg-gray-100"
                                title="تعديل المنتج"
                              >
                                <Edit2 className="h-4 w-4 text-gray-500" />
                              </button>
                              <button
                                onClick={() => handleDeleteClick(product.id)}
                                disabled={isDeleting}
                                className="rounded-lg p-2 transition-colors hover:bg-danger-50"
                                title="حذف المنتج"
                              >
                                <Trash2 className="h-4 w-4 text-danger-500" />
                              </button>
                            </>
                          )}
                          {!canManageProducts && (
                            <span className="text-xs text-gray-400">
                              عرض فقط
                            </span>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            {filteredProducts.length === 0 && (
              <div className="py-12 text-center text-gray-400">
                <Package className="mx-auto mb-3 h-12 w-12" />
                <p>لا توجد منتجات</p>
              </div>
            )}
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-between border-t border-gray-200 px-4 py-3">
              <p className="text-sm text-gray-700">
                عرض {(currentPage - 1) * PAGE_SIZE + 1} -{" "}
                {Math.min(currentPage * PAGE_SIZE, totalCount)} من {totalCount}{" "}
                منتج
              </p>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setCurrentPage((pageValue) => Math.max(1, pageValue - 1))
                  }
                  disabled={currentPage === 1}
                >
                  السابق
                </Button>
                <span className="px-3 py-1 text-sm">
                  {currentPage} / {totalPages}
                </span>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setCurrentPage((pageValue) =>
                      Math.min(totalPages, pageValue + 1),
                    )
                  }
                  disabled={currentPage === totalPages}
                >
                  التالي
                </Button>
              </div>
            </div>
          )}
        </Card>

        {showForm && (
          <ProductFormModal
            product={editingProduct}
            onClose={handleCloseForm}
          />
        )}

        <ConfirmDialog
          open={deletingProductId !== null}
          onOpenChange={(open) => !open && setDeletingProductId(null)}
          onConfirm={handleConfirmDelete}
          title="حذف المنتج"
          description="هل أنت متأكد من حذف هذا المنتج؟"
          isLoading={isDeleting}
        />
      </div>
    </div>
  );
};

export default ProductsPage;
