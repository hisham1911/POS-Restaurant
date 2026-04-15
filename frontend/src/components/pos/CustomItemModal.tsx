import { useEffect, useState } from "react";
import { X, Plus } from "lucide-react";
import { toast } from "sonner";
import { useAddCustomItemMutation } from "@/api/ordersApi";
import { Portal } from "@/components/common/Portal";
import { numberToDisplay, displayToNumber } from "@/hooks/useNumberInput";
import { useAppSelector } from "@/store/hooks";
import { selectIsTaxEnabled, selectTaxRate } from "@/store/slices/cartSlice";
import { AddCustomItemRequest } from "@/types/order.types";
import { Product, ProductType } from "@/types/product.types";
import { handleApiError } from "@/utils/errorHandler";
import {
  getCartItemSubtotal,
  getCartItemTaxAmount,
  getCartItemTotal,
} from "@/utils/cartPricing";

interface CustomItemModalProps {
  orderId?: number;
  onClose: () => void;
  onSuccess?: (item: AddCustomItemRequest) => void;
}

export const CustomItemModal = ({
  orderId,
  onClose,
  onSuccess,
}: CustomItemModalProps) => {
  const currentTaxRate = useAppSelector(selectTaxRate);
  const isTaxEnabled = useAppSelector(selectIsTaxEnabled);
  const [formData, setFormData] = useState<AddCustomItemRequest>({
    name: "",
    unitPrice: 0,
    quantity: 1,
    taxRate: currentTaxRate,
    taxInclusive: false,
    notes: "",
  });

  const [addCustomItem, { isLoading }] = useAddCustomItemMutation();

  useEffect(() => {
    setFormData((current) => ({ ...current, taxRate: currentTaxRate }));
  }, [currentTaxRate]);

  const previewTaxRate = formData.taxRate ?? currentTaxRate;
  const previewProduct: Product = {
    id: 0,
    name: formData.name || "منتج مخصص",
    price: formData.unitPrice,
    taxRate: previewTaxRate,
    taxInclusive: formData.taxInclusive ?? false,
    categoryId: 0,
    isActive: true,
    type: ProductType.Service,
    trackInventory: false,
    createdAt: "",
  };
  const previewItem = {
    product: previewProduct,
    quantity: formData.quantity || 1,
  };
  const previewSubtotal = getCartItemSubtotal(
    previewItem,
    currentTaxRate,
    isTaxEnabled,
  );
  const previewTaxAmount = getCartItemTaxAmount(
    previewItem,
    currentTaxRate,
    isTaxEnabled,
  );
  const previewTotal = getCartItemTotal(
    previewItem,
    currentTaxRate,
    isTaxEnabled,
  );

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

    if (orderId) {
      try {
        await addCustomItem({
          orderId,
          item: formData,
        }).unwrap();

        toast.success(`تم إضافة: ${formData.name}`);
        onSuccess?.(formData);
        onClose();
      } catch (error) {
        toast.error(handleApiError(error));
      }
      return;
    }

    onSuccess?.(formData);
    onClose();
  };

  return (
    <Portal>
      <div
        className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 p-4"
        onClick={onClose}
      >
        <div
          className="flex max-h-[90vh] w-full max-w-md flex-col overflow-hidden rounded-2xl bg-white shadow-2xl"
          onClick={(e) => e.stopPropagation()}
        >
          <div className="flex flex-shrink-0 items-center justify-between border-b border-gray-200 p-6">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-orange-100">
                <Plus className="h-5 w-5 text-orange-600" />
              </div>
              <div>
                <h2 className="text-xl font-bold text-gray-800">منتج مخصص</h2>
                <p className="text-sm text-gray-500">للطلب الحالي فقط</p>
              </div>
            </div>
            <button
              onClick={onClose}
              className="rounded-lg p-2 transition-colors hover:bg-gray-100"
            >
              <X className="h-5 w-5 text-gray-500" />
            </button>
          </div>

          <form
            onSubmit={handleSubmit}
            className="flex-1 space-y-4 overflow-y-auto p-6"
          >
            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700">
                الاسم *
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) =>
                  setFormData({ ...formData, name: e.target.value })
                }
                className="w-full rounded-xl border border-gray-300 px-4 py-2.5 focus:border-orange-500 focus:ring-2 focus:ring-orange-500"
                placeholder="مثال: رسوم توصيل، خدمة تغليف"
                required
                autoFocus
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="mb-2 block text-sm font-medium text-gray-700">
                  السعر *
                </label>
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
                  className="w-full rounded-xl border border-gray-300 px-4 py-2.5 focus:border-orange-500 focus:ring-2 focus:ring-orange-500"
                  placeholder="0.00"
                  required
                />
              </div>

              <div>
                <label className="mb-2 block text-sm font-medium text-gray-700">
                  الكمية *
                </label>
                <input
                  type="number"
                  min="1"
                  value={formData.quantity}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      quantity: parseInt(e.target.value, 10) || 1,
                    })
                  }
                  className="w-full rounded-xl border border-gray-300 px-4 py-2.5 focus:border-orange-500 focus:ring-2 focus:ring-orange-500"
                  placeholder="1"
                  required
                />
              </div>
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700">
                نسبة الضريبة (%)
              </label>
              <input
                type="number"
                step="0.01"
                min="0"
                max="100"
                value={formData.taxRate}
                onChange={(e) => {
                  const nextTaxRate = parseFloat(e.target.value);
                  setFormData({
                    ...formData,
                    taxRate: Number.isNaN(nextTaxRate)
                      ? currentTaxRate
                      : nextTaxRate,
                  });
                }}
                className="w-full rounded-xl border border-gray-300 px-4 py-2.5 focus:border-orange-500 focus:ring-2 focus:ring-orange-500"
                placeholder={currentTaxRate.toString()}
              />
              <p className="mt-1 text-xs text-gray-500">
                الافتراضي: {currentTaxRate}% (من إعدادات المؤسسة)
              </p>
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700">
                السعر شامل الضريبة؟
              </label>
              <div className="grid grid-cols-2 gap-3">
                <label className="flex cursor-pointer items-center gap-2 rounded-xl border border-gray-300 px-4 py-3">
                  <input
                    type="radio"
                    name="custom-item-tax-mode"
                    checked={Boolean(formData.taxInclusive)}
                    onChange={() =>
                      setFormData({ ...formData, taxInclusive: true })
                    }
                    className="h-4 w-4 text-orange-600"
                  />
                  <span className="text-sm text-gray-700">نعم (شامل)</span>
                </label>
                <label className="flex cursor-pointer items-center gap-2 rounded-xl border border-gray-300 px-4 py-3">
                  <input
                    type="radio"
                    name="custom-item-tax-mode"
                    checked={!formData.taxInclusive}
                    onChange={() =>
                      setFormData({ ...formData, taxInclusive: false })
                    }
                    className="h-4 w-4 text-orange-600"
                  />
                  <span className="text-sm text-gray-700">لا (تضاف)</span>
                </label>
              </div>
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700">
                ملاحظات (اختياري)
              </label>
              <textarea
                value={formData.notes || ""}
                onChange={(e) =>
                  setFormData({ ...formData, notes: e.target.value })
                }
                className="w-full resize-none rounded-xl border border-gray-300 px-4 py-2.5 focus:border-orange-500 focus:ring-2 focus:ring-orange-500"
                placeholder="أي ملاحظات إضافية..."
                rows={2}
              />
            </div>

            <div className="rounded-xl border border-orange-200 bg-orange-50 p-4">
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-sm text-gray-600">المجموع الفرعي:</span>
                  <span className="text-sm font-semibold text-gray-700">
                    {previewSubtotal.toFixed(2)} ج.م
                  </span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-sm text-gray-600">
                    الضريبة ({isTaxEnabled ? previewTaxRate : 0}%):
                  </span>
                  <span className="text-sm font-semibold text-gray-700">
                    {previewTaxAmount.toFixed(2)} ج.م
                  </span>
                </div>
                <div className="flex items-center justify-between border-t border-orange-200 pt-2">
                  <span className="text-sm text-gray-600">الإجمالي المتوقع:</span>
                  <span className="text-lg font-bold text-orange-600">
                    {previewTotal.toFixed(2)} ج.م
                  </span>
                </div>
              </div>
              <p className="mt-2 text-xs text-gray-500">
                {formData.taxInclusive
                  ? "السعر المُدخل شامل الضريبة وسيتم استخراج صافي السعر تلقائيًا."
                  : "السعر المُدخل قبل الضريبة وسيتم إضافة الضريبة عليه."}
              </p>
            </div>
          </form>

          <div className="flex flex-shrink-0 gap-3 border-t border-gray-200 p-6">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 rounded-xl border border-gray-300 px-4 py-2.5 font-medium text-gray-700 transition-colors hover:bg-gray-50"
              disabled={orderId ? isLoading : false}
            >
              إلغاء
            </button>
            <button
              type="submit"
              onClick={handleSubmit}
              className="flex-1 rounded-xl bg-orange-500 px-4 py-2.5 font-medium text-white transition-colors hover:bg-orange-600 disabled:cursor-not-allowed disabled:opacity-50"
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
