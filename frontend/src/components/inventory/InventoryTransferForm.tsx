import { useState, useEffect } from "react";
import { useCreateTransferMutation } from "../../api/inventoryApi";
import { useGetProductsQuery } from "../../api/productsApi";
import { useAppSelector } from "../../store/hooks";
import {
  selectBranches,
  selectCurrentBranch,
} from "../../store/slices/branchSlice";
import { selectIsAdmin } from "../../store/slices/authSlice";
import {
  ArrowRight,
  Package,
  AlertTriangle,
  X,
  ChevronDown,
} from "lucide-react";
import { toast } from "sonner";
import type { CreateTransferRequest } from "../../types/inventory.types";

interface InventoryTransferFormProps {
  onSuccess?: () => void;
  onCancel?: () => void;
}

export default function InventoryTransferForm({
  onSuccess,
  onCancel,
}: InventoryTransferFormProps) {
  const isAdmin = useAppSelector(selectIsAdmin);
  const branches = useAppSelector(selectBranches);
  const currentBranch = useAppSelector(selectCurrentBranch);

  const [formData, setFormData] = useState<
    Omit<CreateTransferRequest, "quantity"> & { quantity: string | number }
  >({
    fromBranchId: currentBranch?.id || 0,
    toBranchId: 0,
    productId: 0,
    quantity: "" as string | number,
    reason: "",
    notes: "",
  });

  const [createTransfer, { isLoading }] = useCreateTransferMutation();
  const { data: productsResponse } = useGetProductsQuery({});
  const products = productsResponse?.data ?? [];

  // Update fromBranchId when currentBranch changes
  useEffect(() => {
    if (currentBranch) {
      setFormData((prev) => ({ ...prev, fromBranchId: currentBranch.id }));
    }
  }, [currentBranch]);

  // Get available branches (exclude source branch)
  const availableToBranches = branches.filter(
    (b) => b.id !== formData.fromBranchId,
  );

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const numQuantity = Number(formData.quantity) || 0;

    // Validation
    if (!formData.fromBranchId) {
      toast.error("الرجاء اختيار الفرع المصدر");
      return;
    }
    if (!formData.toBranchId) {
      toast.error("الرجاء اختيار الفرع المستهدف");
      return;
    }
    if (!formData.productId) {
      toast.error("الرجاء اختيار المنتج");
      return;
    }
    if (numQuantity <= 0) {
      toast.error("الكمية يجب أن تكون أكبر من صفر");
      return;
    }
    if (!formData.reason.trim()) {
      toast.error("الرجاء إدخال سبب النقل");
      return;
    }

    try {
      await createTransfer({
        ...formData,
        quantity: numQuantity,
      }).unwrap();
      toast.success("تم إنشاء طلب النقل بنجاح");

      // Reset form
      setFormData({
        fromBranchId: currentBranch?.id || 0,
        toBranchId: 0,
        productId: 0,
        quantity: "" as string | number,
        reason: "",
        notes: "",
      });

      onSuccess?.();
    } catch (error: any) {
      toast.error(error?.data?.message || "حدث خطأ في إنشاء طلب النقل");
    }
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
              نقل المخزون متاح للمديرين فقط
            </p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-xl font-bold text-gray-900">
          نقل مخزون بين الفروع
        </h2>
        {onCancel && (
          <button
            onClick={onCancel}
            className="p-2 hover:bg-gray-100 rounded-lg"
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>
        )}
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        {/* Branch Selection */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* From Branch */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              من فرع
            </label>
            <div className="relative">
              <select
                value={formData.fromBranchId}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    fromBranchId: Number(e.target.value),
                  })
                }
                className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
                required
              >
                <option value={0}>اختر الفرع المصدر</option>
                {branches.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name}
                  </option>
                ))}
              </select>
              <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
            </div>
          </div>

          {/* To Branch */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              إلى فرع
            </label>
            <div className="relative">
              <select
                value={formData.toBranchId}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    toBranchId: Number(e.target.value),
                  })
                }
                className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                required
                disabled={!formData.fromBranchId}
              >
                <option value={0}>اختر الفرع المستهدف</option>
                {availableToBranches.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name}
                  </option>
                ))}
              </select>
              <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
            </div>
          </div>
        </div>

        {/* Transfer Direction Visual */}
        {formData.fromBranchId > 0 && formData.toBranchId > 0 && (
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
            <div className="flex items-center justify-center gap-4">
              <div className="text-center">
                <Package className="w-8 h-8 text-blue-600 mx-auto mb-2" />
                <p className="text-sm font-semibold text-blue-900">
                  {branches.find((b) => b.id === formData.fromBranchId)?.name}
                </p>
              </div>
              <ArrowRight className="w-8 h-8 text-blue-600" />
              <div className="text-center">
                <Package className="w-8 h-8 text-blue-600 mx-auto mb-2" />
                <p className="text-sm font-semibold text-blue-900">
                  {branches.find((b) => b.id === formData.toBranchId)?.name}
                </p>
              </div>
            </div>
          </div>
        )}

        {/* Product Selection */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            المنتج
          </label>
          <div className="relative">
            <select
              value={formData.productId}
              onChange={(e) =>
                setFormData({ ...formData, productId: Number(e.target.value) })
              }
              className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
              required
            >
              <option value={0}>اختر المنتج</option>
              {products.map((product) => (
                <option key={product.id} value={product.id}>
                  {product.name} {product.sku && `(${product.sku})`}
                </option>
              ))}
            </select>
            <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
          </div>
        </div>

        {/* Quantity */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            الكمية
          </label>
          <input
            type="number"
            min="1"
            value={formData.quantity}
            onChange={(e) =>
              setFormData({ ...formData, quantity: e.target.value })
            }
            className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            placeholder="1"
            required
          />
        </div>

        {/* Reason */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            سبب النقل <span className="text-red-500">*</span>
          </label>
          <input
            type="text"
            value={formData.reason}
            onChange={(e) =>
              setFormData({ ...formData, reason: e.target.value })
            }
            placeholder="مثال: تعويض نقص المخزون، إعادة توزيع..."
            className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            required
          />
        </div>

        {/* Notes */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            ملاحظات (اختياري)
          </label>
          <textarea
            value={formData.notes}
            onChange={(e) =>
              setFormData({ ...formData, notes: e.target.value })
            }
            rows={3}
            placeholder="أي ملاحظات إضافية..."
            className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          />
        </div>

        {/* Actions */}
        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={isLoading}
            className="flex-1 px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed font-semibold"
          >
            {isLoading ? "جاري الإنشاء..." : "إنشاء طلب النقل"}
          </button>
          {onCancel && (
            <button
              type="button"
              onClick={onCancel}
              className="px-6 py-3 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300"
            >
              إلغاء
            </button>
          )}
        </div>
      </form>
    </div>
  );
}
