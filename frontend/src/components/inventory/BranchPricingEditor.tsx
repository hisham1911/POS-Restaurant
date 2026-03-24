import { useState } from "react";
import {
  useGetBranchPricesQuery,
  useSetBranchPriceMutation,
  useRemoveBranchPriceMutation,
} from "../../api/inventoryApi";
import { useGetProductsQuery } from "../../api/productsApi";
import { useAppSelector } from "../../store/hooks";
import {
  selectCurrentBranch,
  selectBranches,
} from "../../store/slices/branchSlice";
import { selectIsAdmin } from "../../store/slices/authSlice";
import {
  DollarSign,
  Edit,
  Trash2,
  Plus,
  AlertTriangle,
  X,
  ChevronDown,
} from "lucide-react";
import { toast } from "sonner";
import { formatDateOnly } from "../../utils/formatters";

export default function BranchPricingEditor() {
  const isAdmin = useAppSelector(selectIsAdmin);
  const currentBranch = useAppSelector(selectCurrentBranch);
  const branches = useAppSelector(selectBranches);

  const [selectedBranchId, setSelectedBranchId] = useState(
    currentBranch?.id || 0,
  );
  const [isAddingPrice, setIsAddingPrice] = useState(false);
  const [editingPriceId, setEditingPriceId] = useState<number | null>(null);

  const [formData, setFormData] = useState({
    productId: 0,
    price: "" as string | number,
    effectiveFrom: new Date().toISOString().split("T")[0],
  });

  const { data: branchPrices, isLoading } = useGetBranchPricesQuery(
    selectedBranchId,
    {
      skip: !selectedBranchId,
    },
  );
  const { data: productsResponse } = useGetProductsQuery({});
  const products = productsResponse?.data ?? [];

  const [setBranchPrice, { isLoading: isSaving }] = useSetBranchPriceMutation();
  const [removeBranchPrice] = useRemoveBranchPriceMutation();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!formData.productId) {
      toast.error("الرجاء اختيار المنتج");
      return;
    }

    const numPrice = Number(formData.price) || 0;
    if (numPrice <= 0) {
      toast.error("السعر يجب أن يكون أكبر من صفر");
      return;
    }

    try {
      await setBranchPrice({
        branchId: selectedBranchId,
        productId: formData.productId,
        price: numPrice,
        effectiveFrom: formData.effectiveFrom,
      }).unwrap();

      toast.success("تم تحديث السعر بنجاح");
      setIsAddingPrice(false);
      setEditingPriceId(null);
      setFormData({
        productId: 0,
        price: "" as string | number,
        effectiveFrom: new Date().toISOString().split("T")[0],
      });
    } catch (error: any) {
      toast.error(error?.data?.message || "حدث خطأ في تحديث السعر");
    }
  };

  const handleRemove = async (productId: number) => {
    if (!confirm("هل تريد حذف السعر المخصص؟ سيتم استخدام السعر الافتراضي"))
      return;

    try {
      await removeBranchPrice({
        branchId: selectedBranchId,
        productId,
      }).unwrap();
      toast.success("تم حذف السعر المخصص");
    } catch (error: any) {
      toast.error(error?.data?.message || "حدث خطأ في حذف السعر");
    }
  };

  const handleEdit = (price: any) => {
    setFormData({
      productId: price.productId,
      price: price.price,
      effectiveFrom: price.effectiveFrom.split("T")[0],
    });
    setEditingPriceId(price.id);
    setIsAddingPrice(true);
  };

  if (!isAdmin) {
    return (
      <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-6">
        <div className="flex items-center gap-3">
          <AlertTriangle className="w-8 h-8 text-yellow-600" />
          <div>
            <h3 className="text-lg font-semibold text-yellow-900">
              صلاحيات غير كافية
            </h3>
            <p className="text-sm text-yellow-700">
              تعديل الأسعار متاح للمديرين فقط
            </p>
          </div>
        </div>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">أسعار الفروع</h2>
          <p className="text-sm text-gray-600 mt-1">
            تخصيص أسعار مختلفة لكل فرع
          </p>
        </div>

        <button
          onClick={() => setIsAddingPrice(true)}
          className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
        >
          <Plus className="w-4 h-4" />
          إضافة سعر مخصص
        </button>
      </div>

      {/* Branch Selector */}
      <div className="bg-white rounded-lg border border-gray-200 p-4">
        <label className="block text-sm font-medium text-gray-700 mb-2">
          اختر الفرع
        </label>
        <div className="relative w-full md:w-64">
          <select
            value={selectedBranchId}
            onChange={(e) => setSelectedBranchId(Number(e.target.value))}
            className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
          >
            <option value={0}>اختر فرع</option>
            {branches.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </select>
          <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
        </div>
      </div>

      {/* Add/Edit Form */}
      {isAddingPrice && (
        <div className="bg-white rounded-lg border border-gray-200 p-6">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-lg font-semibold text-gray-900">
              {editingPriceId ? "تعديل السعر" : "إضافة سعر مخصص"}
            </h3>
            <button
              onClick={() => {
                setIsAddingPrice(false);
                setEditingPriceId(null);
                setFormData({
                  productId: 0,
                  price: 0,
                  effectiveFrom: new Date().toISOString().split("T")[0],
                });
              }}
              className="p-2 hover:bg-gray-100 rounded-lg"
            >
              <X className="w-5 h-5 text-gray-500" />
            </button>
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                المنتج
              </label>
              <div className="relative">
                <select
                  value={formData.productId}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      productId: Number(e.target.value),
                    })
                  }
                  className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                  required
                  disabled={!!editingPriceId}
                >
                  <option value={0}>اختر المنتج</option>
                  {products.map((product) => (
                    <option key={product.id} value={product.id}>
                      {product.name} - السعر الافتراضي: {product.price} ج.م
                    </option>
                  ))}
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                السعر المخصص (ج.م)
              </label>
              <input
                type="number"
                step="0.01"
                min="0"
                value={formData.price}
                onChange={(e) =>
                  setFormData({ ...formData, price: e.target.value })
                }
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="0.00"
                required
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                تاريخ السريان
              </label>
              <input
                type="date"
                value={formData.effectiveFrom}
                onChange={(e) =>
                  setFormData({ ...formData, effectiveFrom: e.target.value })
                }
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                required
              />
            </div>

            <div className="flex gap-3">
              <button
                type="submit"
                disabled={isSaving}
                className="flex-1 px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 font-semibold"
              >
                {isSaving ? "جاري الحفظ..." : "حفظ"}
              </button>
              <button
                type="button"
                onClick={() => {
                  setIsAddingPrice(false);
                  setEditingPriceId(null);
                }}
                className="px-6 py-3 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300"
              >
                إلغاء
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Prices Table */}
      {selectedBranchId > 0 && (
        <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    المنتج
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    السعر الافتراضي
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    السعر المخصص
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    الفرق
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    تاريخ السريان
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    الحالة
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    إجراءات
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {branchPrices && branchPrices.length > 0 ? (
                  branchPrices.map((price) => {
                    const difference = price.price - price.defaultPrice;
                    const percentDiff = (
                      (difference / price.defaultPrice) *
                      100
                    ).toFixed(1);

                    return (
                      <tr key={price.id} className="hover:bg-gray-50">
                        <td className="px-6 py-4">
                          <div className="text-sm font-medium text-gray-900">
                            {price.productName}
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          <div className="text-sm text-gray-900">
                            {price.defaultPrice.toFixed(2)} ج.م
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          <div className="text-sm font-semibold text-blue-600">
                            {price.price.toFixed(2)} ج.م
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          <div
                            className={`text-sm font-semibold ${
                              difference > 0
                                ? "text-green-600"
                                : difference < 0
                                  ? "text-red-600"
                                  : "text-gray-600"
                            }`}
                          >
                            {difference > 0 ? "+" : ""}
                            {difference.toFixed(2)} ({percentDiff}%)
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          <div className="text-sm text-gray-900">
                            {formatDateOnly(price.effectiveFrom)}
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          {price.isActive ? (
                            <span className="inline-flex px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">
                              نشط
                            </span>
                          ) : (
                            <span className="inline-flex px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                              غير نشط
                            </span>
                          )}
                        </td>
                        <td className="px-6 py-4">
                          <div className="flex items-center gap-2">
                            <button
                              onClick={() => handleEdit(price)}
                              className="p-2 text-blue-600 hover:bg-blue-50 rounded-lg"
                              title="تعديل"
                            >
                              <Edit className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => handleRemove(price.productId)}
                              className="p-2 text-red-600 hover:bg-red-50 rounded-lg"
                              title="حذف"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })
                ) : (
                  <tr>
                    <td colSpan={7} className="px-6 py-12 text-center">
                      <DollarSign className="w-12 h-12 text-gray-400 mx-auto mb-4" />
                      <p className="text-gray-500">
                        لا توجد أسعار مخصصة لهذا الفرع
                      </p>
                      <p className="text-sm text-gray-400 mt-1">
                        يتم استخدام الأسعار الافتراضية للمنتجات
                      </p>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
