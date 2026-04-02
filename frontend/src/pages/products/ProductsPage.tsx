import { useState, useMemo } from "react";
import { Plus, Search, Edit2, Trash2, Package, ChevronDown } from "lucide-react";
import {
  useGetProductsQuery,
  useDeleteProductMutation,
} from "@/api/productsApi";
import { useCategories } from "@/hooks/useProducts";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { Card } from "@/components/common/Card";
import { Loading } from "@/components/common/Loading";
import { ProductFormModal } from "@/components/products/ProductFormModal";
import { formatCurrency } from "@/utils/formatters";
import { Product } from "@/types/product.types";
import { toast } from "react-hot-toast";
import clsx from "clsx";
import { getProductCurrentStock } from "@/utils/productStock";

export const ProductsPage = () => {
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [showActiveOnly, setShowActiveOnly] = useState(false);
  const [showLowStockOnly, setShowLowStockOnly] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [editingProduct, setEditingProduct] = useState<Product | null>(null);

  const { categories } = useCategories();

  // Determine if we should use server-side filtering
  // For now, always use server-side filtering for better performance
  const useServerSideFiltering = true;

  // Build query params for server-side filtering
  const queryParams = useMemo(() => {
    if (!useServerSideFiltering) return undefined;

    return {
      categoryId: selectedCategory ?? undefined,
      search: searchQuery.trim() || undefined,
      isActive: showActiveOnly ? true : undefined,
      lowStock: showLowStockOnly ? true : undefined,
    };
  }, [
    selectedCategory,
    searchQuery,
    showActiveOnly,
    showLowStockOnly,
    useServerSideFiltering,
  ]);

  const { data: productsData, isLoading } = useGetProductsQuery(queryParams);
  const [deleteMutation, { isLoading: isDeleting }] =
    useDeleteProductMutation();

  const products = productsData?.data || [];

  // Client-side filtering fallback (if server-side is disabled)
  const filteredProducts = useMemo(() => {
    if (useServerSideFiltering) return products;

    return products.filter((product) => {
      const matchesSearch = product.name
        .toLowerCase()
        .includes(searchQuery.toLowerCase());
      const matchesCategory =
        !selectedCategory || product.categoryId === selectedCategory;
      const matchesActive = !showActiveOnly || product.isActive;
      const matchesLowStock =
        !showLowStockOnly ||
        (product.trackInventory &&
          getProductCurrentStock(product) < (product.lowStockThreshold ?? 5));
      return (
        matchesSearch && matchesCategory && matchesActive && matchesLowStock
      );
    });
  }, [
    products,
    searchQuery,
    selectedCategory,
    showActiveOnly,
    showLowStockOnly,
    useServerSideFiltering,
  ]);

  const handleEdit = (product: Product) => {
    setEditingProduct(product);
    setShowForm(true);
  };

  const handleDelete = async (id: number) => {
    if (confirm("هل أنت متأكد من حذف هذا المنتج؟")) {
      try {
        await deleteMutation(id).unwrap();
        toast.success("تم حذف المنتج بنجاح");
      } catch (error) {
        toast.error("فشل في حذف المنتج");
      }
    }
  };

  const handleCloseForm = () => {
    setShowForm(false);
    setEditingProduct(null);
  };

  if (isLoading) return <Loading />;

  const totalProducts = filteredProducts.length;
  const activeProducts = filteredProducts.filter((p) => p.isActive).length;
  const lowStockProducts = filteredProducts.filter(
    (p) =>
      p.trackInventory &&
      getProductCurrentStock(p) < (p.lowStockThreshold ?? 5),
  ).length;

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="flex items-center gap-3 mb-2">
              <div className="w-10 h-10 rounded-full bg-blue-100 flex items-center justify-center">
                <Package className="w-5 h-5 text-blue-600" />
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
            rightIcon={<Plus className="w-5 h-5" />}
          >
            إضافة منتج
          </Button>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <Card className="border-blue-100">
            <p className="text-sm text-gray-600">إجمالي المنتجات</p>
            <p className="text-2xl font-bold text-gray-900 mt-1">
              {totalProducts}
            </p>
          </Card>
          <Card className="border-green-100">
            <p className="text-sm text-gray-600">المنتجات النشطة</p>
            <p className="text-2xl font-bold text-green-700 mt-1">
              {activeProducts}
            </p>
          </Card>
          <Card className="border-amber-100">
            <p className="text-sm text-gray-600">مخزون منخفض</p>
            <p className="text-2xl font-bold text-amber-700 mt-1">
              {lowStockProducts}
            </p>
          </Card>
        </div>

        <Card className="shrink-0">
          <div className="space-y-4">
            <div className="flex flex-col md:flex-row gap-4">
              <div className="flex-1">
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                  <Input
                    placeholder="بحث عن منتج..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    className="pl-10"
                  />
                </div>
              </div>
              <div className="relative">
                <select
                  value={selectedCategory || ""}
                  onChange={(e) =>
                    setSelectedCategory(
                      e.target.value ? Number(e.target.value) : null,
                    )
                  }
                  className="appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
                >
                  <option value="">كل التصنيفات</option>
                  {categories.map((cat) => (
                    <option key={cat.id} value={cat.id}>
                      {cat.name}
                    </option>
                  ))}
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>

            {/* Additional Filters */}
            <div className="flex flex-wrap gap-4">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={showActiveOnly}
                  onChange={(e) => setShowActiveOnly(e.target.checked)}
                  className="w-4 h-4 text-primary-600 rounded focus:ring-2 focus:ring-primary-500"
                />
                <span className="text-sm text-gray-700">نشط فقط</span>
              </label>
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={showLowStockOnly}
                  onChange={(e) => setShowLowStockOnly(e.target.checked)}
                  className="w-4 h-4 text-primary-600 rounded focus:ring-2 focus:ring-primary-500"
                />
                <span className="text-sm text-gray-700">مخزون منخفض فقط</span>
              </label>
            </div>
          </div>
        </Card>

        <Card padding="none">
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="bg-gray-50 border-b">
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
                    (c) => c.id === product.categoryId,
                  );
                  return (
                    <tr key={product.id} className="border-b hover:bg-gray-50">
                      <td className="px-4 py-3 text-gray-500">{index + 1}</td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-3">
                          <div className="w-10 h-10 bg-gray-100 rounded-lg flex items-center justify-center">
                            <Package className="w-5 h-5 text-gray-400" />
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
                            "px-2.5 py-1 rounded-full text-xs font-medium",
                            getProductCurrentStock(product) <= 0
                              ? "bg-danger-50 text-danger-600"
                              : getProductCurrentStock(product) <=
                                  (product.lowStockThreshold ?? 5)
                                ? "bg-warning-50 text-warning-600"
                                : "bg-gray-100 text-gray-700",
                          )}
                        >
                          {getProductCurrentStock(product)}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-gray-600 text-sm">
                        {product.reorderPoint ?? "—"}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={clsx(
                            "px-2.5 py-0.5 rounded-full text-xs font-medium",
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
                          <button
                            onClick={() => handleEdit(product)}
                            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
                          >
                            <Edit2 className="w-4 h-4 text-gray-500" />
                          </button>
                          <button
                            onClick={() => handleDelete(product.id)}
                            disabled={isDeleting}
                            className="p-2 hover:bg-danger-50 rounded-lg transition-colors"
                          >
                            <Trash2 className="w-4 h-4 text-danger-500" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            {filteredProducts.length === 0 && (
              <div className="text-center py-12 text-gray-400">
                <Package className="w-12 h-12 mx-auto mb-3" />
                <p>لا توجد منتجات</p>
              </div>
            )}
          </div>
        </Card>

        {showForm && (
          <ProductFormModal
            product={editingProduct}
            onClose={handleCloseForm}
          />
        )}
      </div>
    </div>
  );
};

export default ProductsPage;
