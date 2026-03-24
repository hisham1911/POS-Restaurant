import { useState } from "react";
import { X, Plus } from "lucide-react";
import { useAddCustomItemMutation } from "@/api/ordersApi";
import { toast } from "sonner";
import { AddCustomItemRequest } from "@/types/order.types";
import { Portal } from "@/components/common/Portal";
import { numberToDisplay, displayToNumber } from "@/hooks/useNumberInput";

interface CustomItemModalProps {
  orderId?: number; // Optional now
  onClose: () => void;
  onSuccess?: (item: AddCustomItemRequest) => void;
}

export const CustomItemModal = ({
  orderId,
  onClose,
  onSuccess,
}: CustomItemModalProps) => {
  const [formData, setFormData] = useState<AddCustomItemRequest>({
    name: "",
    unitPrice: 0,
    quantity: 1,
    taxRate: 14, // الضريبة الافتراضية 14%
    notes: "",
  });

  const [addCustomItem, { isLoading }] = useAddCustomItemMutation();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!formData.name.trim()) {
      toast.error("الرجاء إدخال اسم المنتج");
      return;
    }

    if (formData.unitPrice <= 0) {
      toast.error("الرجاء إدخال سعر صحيح");
      return;
    }

    if (!formData.quantity || formData.quantity <= 0) {
      toast.error("الرجاء إدخال كمية صحيحة");
      return;
    }

    // If orderId is provided, add to existing order via API
    if (orderId) {
      try {
        const result = await addCustomItem({
          orderId,
          item: formData,
        }).unwrap();

        if (result.success) {
          toast.success(`تم إضافة: ${formData.name}`);
          onSuccess?.(formData);
          onClose();
        }
      } catch (error: any) {
        toast.error(error?.data?.message || "فشل في إضافة المنتج المخصص");
      }
    } else {
      // Otherwise, just return the data to parent component
      onSuccess?.(formData);
      onClose();
    }
  };

  return (
    <Portal>
      <div 
        className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/50"
        onClick={onClose}
      >
        <div 
          className="bg-white rounded-2xl shadow-2xl w-full max-w-md max-h-[90vh] flex flex-col overflow-hidden"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b border-gray-200 flex-shrink-0">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-orange-100 rounded-xl flex items-center justify-center">
                <Plus className="w-5 h-5 text-orange-600" />
              </div>
              <div>
                <h2 className="text-xl font-bold text-gray-800">منتج مخصص</h2>
                <p className="text-sm text-gray-500">للطلب الحالي فقط</p>
              </div>
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            >
              <X className="w-5 h-5 text-gray-500" />
            </button>
          </div>

          {/* Form - Scrollable */}
          <form
            onSubmit={handleSubmit}
            className="p-6 space-y-4 overflow-y-auto flex-1"
          >
            {/* Name */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                الاسم *
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) =>
                  setFormData({ ...formData, name: e.target.value })
                }
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
                placeholder="مثال: رسوم توصيل، خدمة تغليف"
                required
                autoFocus
              />
            </div>

            {/* Price & Quantity */}
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  السعر *
                </label>
                <div className="relative">
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={numberToDisplay(formData.unitPrice)}
                    onChange={(e) =>
                      setFormData({
                        ...formData,
                        unitPrice: displayToNumber(e.target.value),
                      })
                    }
                    className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
                    placeholder="0.00"
                    required
                  />
                </div>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  الكمية *
                </label>
                <input
                  type="number"
                  min="1"
                  value={formData.quantity}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      quantity: parseInt(e.target.value) || 1,
                    })
                  }
                  className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
                  placeholder="1"
                  required
                />
              </div>
            </div>

            {/* Tax Rate */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                نسبة الضريبة (%)
              </label>
              <input
                type="number"
                step="0.01"
                min="0"
                max="100"
                value={formData.taxRate}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    taxRate: parseFloat(e.target.value) || 14,
                  })
                }
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
                placeholder="14"
              />
              <p className="mt-1 text-xs text-gray-500">
                الافتراضي: 14% (ضريبة القيمة المضافة)
              </p>
            </div>

            {/* Notes */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                ملاحظات (اختياري)
              </label>
              <textarea
                value={formData.notes || ""}
                onChange={(e) =>
                  setFormData({ ...formData, notes: e.target.value })
                }
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-orange-500 focus:border-orange-500 resize-none"
                placeholder="أي ملاحظات إضافية..."
                rows={2}
              />
            </div>

            {/* Preview */}
            <div className="p-4 bg-orange-50 rounded-xl border border-orange-200">
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-600">الإجمالي المتوقع:</span>
                <span className="text-lg font-bold text-orange-600">
                  {(
                    formData.unitPrice *
                    (formData.quantity || 1) *
                    (1 + (formData.taxRate || 0) / 100)
                  ).toFixed(2)}{" "}
                  ج.م
                </span>
              </div>
              <p className="text-xs text-gray-500 mt-1">
                {formData.unitPrice.toFixed(2)} × {formData.quantity || 1} +
                ضريبة {formData.taxRate || 0}%
              </p>
            </div>
          </form>

          {/* Actions - Fixed at bottom */}
          <div className="flex gap-3 p-6 border-t border-gray-200 flex-shrink-0">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 px-4 py-2.5 border border-gray-300 text-gray-700 rounded-xl hover:bg-gray-50 transition-colors font-medium"
              disabled={orderId ? isLoading : false}
            >
              إلغاء
            </button>
            <button
              type="submit"
              onClick={handleSubmit}
              className="flex-1 px-4 py-2.5 bg-orange-500 text-white rounded-xl hover:bg-orange-600 transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed"
              disabled={orderId ? isLoading : false}
            >
              {orderId && isLoading ? "جاري الإضافة..." : "إضافة للطلب"}
            </button>
          </div>
        </div>
      </div>
    </Portal>
  );
};
