import { useState } from "react";
import { X, Package, DollarSign, Tag, Hash, Barcode, ChevronDown } from "lucide-react";
import { useQuickCreateProductMutation } from "@/api/productsApi";
import { useCategories } from "@/hooks/useProducts";
import { toast } from "sonner";
import { QuickCreateProductRequest, ProductType } from "@/types/product.types";
import { Portal } from "@/components/common/Portal";

interface ProductQuickCreateModalProps {
  onClose: () => void;
  onSuccess?: (productId: number) => void;
  initialName?: string;
}

export const ProductQuickCreateModal = ({
  onClose,
  onSuccess,
  initialName = "",
}: ProductQuickCreateModalProps) => {
  const [formData, setFormData] = useState<Omit<QuickCreateProductRequest, 'price' | 'initialStock'> & { price: string | number; initialStock: string | number }>({
    name: initialName,
    price: "" as string | number,
    categoryId: 0,
    type: ProductType.Service, // افتراضياً خدمة للإنشاء السريع
    initialStock: "" as string | number,
    sku: "",
    barcode: "",
  });

  const [quickCreate, { isLoading }] = useQuickCreateProductMutation();
  const { categories } = useCategories();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!formData.name.trim()) {
      toast.error("الرجاء إدخال اسم المنتج");
      return;
    }

    const numPrice = Number(formData.price) || 0;
    if (numPrice <= 0) {
      toast.error("الرجاء إدخال سعر صحيح");
      return;
    }

    if (!formData.categoryId) {
      toast.error("الرجاء اختيار التصنيف");
      return;
    }

    try {
      const result = await quickCreate({
        ...formData,
        price: numPrice,
        initialStock: Number(formData.initialStock) || 0,
      }).unwrap();

      if (result.success && result.data) {
        toast.success(`تم إضافة المنتج: ${result.data.name}`);
        onSuccess?.(result.data.id);
        onClose();
      }
    } catch (error: any) {
      toast.error(error?.data?.message || "فشل في إضافة المنتج");
    }
  };

  return (
    <Portal>
      <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/50">
        <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md max-h-[90vh] overflow-y-auto">
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b border-gray-200">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-primary-100 rounded-xl flex items-center justify-center">
                <Package className="w-5 h-5 text-primary-600" />
              </div>
              <h2 className="text-xl font-bold text-gray-800">
                إضافة منتج سريع
              </h2>
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            >
              <X className="w-5 h-5 text-gray-500" />
            </button>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} className="p-6 space-y-4">
            {/* Name */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                اسم المنتج *
              </label>
              <div className="relative">
                <Tag className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <input
                  type="text"
                  value={formData.name}
                  onChange={(e) =>
                    setFormData({ ...formData, name: e.target.value })
                  }
                  className="w-full pr-10 pl-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                  placeholder="مثال: قهوة تركي"
                  required
                  autoFocus
                />
              </div>
            </div>

            {/* Price */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                السعر *
              </label>
              <div className="relative">
                <DollarSign className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={formData.price || ""}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      price: e.target.value,
                    })
                  }
                  className="w-full pr-10 pl-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                  placeholder="0.00"
                  required
                />
              </div>
            </div>

            {/* Category */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                التصنيف *
              </label>
              <div className="relative">
                <select
                  value={formData.categoryId}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      categoryId: parseInt(e.target.value),
                    })
                  }
                  className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
                  required
                >
                  <option value="">اختر التصنيف</option>
                  {categories.map((cat) => (
                    <option key={cat.id} value={cat.id}>
                      {cat.name}
                    </option>
                  ))}
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>

            {/* Product Type */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                نوع المنتج *
              </label>
              <div className="relative">
                <select
                  value={formData.type}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      type: parseInt(e.target.value) as ProductType,
                    })
                  }
                  className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
                  required
                >
                  <option value={ProductType.Service}>
                    خدمة (لا يتتبع المخزون)
                  </option>
                  <option value={ProductType.Physical}>
                    منتج مادي (يتتبع المخزون)
                  </option>
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
              <p className="mt-1 text-xs text-gray-500">
                {formData.type === ProductType.Service
                  ? "الخدمات لا تحتاج تتبع مخزون (مثل: التوصيل، الصيانة)"
                  : "المنتجات المادية تحتاج تتبع مخزون (مثل: القهوة، الطعام)"}
              </p>
            </div>

            {/* Initial Stock (only for Physical products) */}
            {formData.type === ProductType.Physical && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  الكمية الأولية
                </label>
                <div className="relative">
                  <Hash className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                  <input
                    type="number"
                    min="0"
                    value={formData.initialStock || ""}
                    onChange={(e) =>
                      setFormData({
                        ...formData,
                        initialStock: e.target.value,
                      })
                    }
                    className="w-full pr-10 pl-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                    placeholder="0"
                  />
                </div>
              </div>
            )}

            {/* SKU (optional) */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                SKU (اختياري)
              </label>
              <input
                type="text"
                value={formData.sku || ""}
                onChange={(e) =>
                  setFormData({ ...formData, sku: e.target.value })
                }
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                placeholder="مثال: COFFEE-001"
              />
            </div>

            {/* Barcode (optional) */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                الباركود (اختياري)
              </label>
              <div className="relative">
                <Barcode className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <input
                  type="text"
                  value={formData.barcode || ""}
                  onChange={(e) =>
                    setFormData({ ...formData, barcode: e.target.value })
                  }
                  className="w-full pr-10 pl-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                  placeholder="مثال: 1234567890123"
                />
              </div>
            </div>

            {/* Actions */}
            <div className="flex gap-3 pt-4">
              <button
                type="button"
                onClick={onClose}
                className="flex-1 px-4 py-2.5 border border-gray-300 text-gray-700 rounded-xl hover:bg-gray-50 transition-colors font-medium"
                disabled={isLoading}
              >
                إلغاء
              </button>
              <button
                type="submit"
                className="flex-1 px-4 py-2.5 bg-primary-600 text-white rounded-xl hover:bg-primary-700 transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                disabled={isLoading}
              >
                {isLoading ? "جاري الإضافة..." : "إضافة المنتج"}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Portal>
  );
};
