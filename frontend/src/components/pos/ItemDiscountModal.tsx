import { useState } from "react";
import { X, Percent, DollarSign, Check } from "lucide-react";
import { formatCurrency } from "@/utils/formatters";
import { Button } from "@/components/common/Button";
import { toast } from "sonner";
import clsx from "clsx";
import { Portal } from "@/components/common/Portal";
import { CartItem, ItemDiscount } from "@/store/slices/cartSlice";

interface ItemDiscountModalProps {
  item: CartItem;
  onApply: (discount: ItemDiscount) => void;
  onRemove: () => void;
  onClose: () => void;
}

export const ItemDiscountModal = ({
  item,
  onApply,
  onRemove,
  onClose,
}: ItemDiscountModalProps) => {
  const itemTotal = item.product.price * item.quantity;
  const currentDiscount = item.discount;

  const [discountType, setDiscountType] = useState<"percentage" | "fixed">(
    currentDiscount?.type || "percentage",
  );
  const [discountValue, setDiscountValue] = useState<string>(
    currentDiscount?.value?.toString() || "",
  );
  const [reason, setReason] = useState<string>(currentDiscount?.reason || "");

  const numericValue = parseFloat(discountValue) || 0;

  let previewDiscount = 0;
  if (numericValue > 0) {
    if (discountType === "percentage") {
      previewDiscount = itemTotal * (numericValue / 100);
    } else {
      previewDiscount = numericValue;
    }
    previewDiscount = Math.min(previewDiscount, itemTotal);
  }

  const previewTotal = itemTotal - previewDiscount;

  const handleNumpadClick = (value: string) => {
    if (value === "C") {
      setDiscountValue("");
    } else if (value === "←") {
      setDiscountValue((prev) => prev.slice(0, -1));
    } else if (value === ".") {
      if (!discountValue.includes(".")) {
        setDiscountValue((prev) => prev + ".");
      }
    } else {
      setDiscountValue((prev) => prev + value);
    }
  };

  const handleApply = () => {
    if (numericValue <= 0) {
      toast.error("قيمة الخصم يجب أن تكون أكبر من صفر");
      return;
    }

    if (discountType === "percentage" && numericValue > 100) {
      toast.error("نسبة الخصم لا يمكن أن تتجاوز 100%");
      return;
    }

    if (discountType === "fixed" && numericValue > itemTotal) {
      toast.error("قيمة الخصم لا يمكن أن تتجاوز سعر المنتج");
      return;
    }

    onApply({
      type: discountType,
      value: numericValue,
      ...(reason ? { reason } : {}),
    });
    toast.success(`تم تطبيق الخصم على ${item.product.name}`);
    onClose();
  };

  const handleRemove = () => {
    onRemove();
    toast.success(`تم إلغاء الخصم عن ${item.product.name}`);
    onClose();
  };

  const quickPercentages = [5, 10, 15, 20];

  return (
    <Portal>
      <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-[100] p-4">
        <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-hidden animate-scale-in flex flex-col">
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b shrink-0">
            <h2 className="text-xl font-bold">خصم على: {item.product.name}</h2>
            <button
              onClick={onClose}
              className="w-10 h-10 rounded-lg bg-gray-100 flex items-center justify-center hover:bg-danger-50 hover:text-danger-500 transition-colors"
            >
              <X className="w-5 h-5" />
            </button>
          </div>

          <div className="flex-1 overflow-y-auto p-6 space-y-6">
            {/* Item Total */}
            <div className="text-center p-4 bg-gray-50 rounded-xl">
              <p className="text-gray-500 text-sm mb-1">
                سعر المنتج ({item.quantity} ×{" "}
                {formatCurrency(item.product.price)})
              </p>
              <p className="text-2xl font-bold text-gray-800">
                {formatCurrency(itemTotal)}
              </p>
            </div>

            {/* Discount Type */}
            <div>
              <p className="text-sm font-medium text-gray-500 mb-3">
                نوع الخصم
              </p>
              <div className="grid grid-cols-2 gap-3">
                <button
                  onClick={() => setDiscountType("percentage")}
                  className={clsx(
                    "flex flex-col items-center gap-2 p-4 rounded-xl border-2 transition-all",
                    discountType === "percentage"
                      ? "border-primary-600 bg-primary-50 text-primary-600"
                      : "border-gray-200 hover:border-gray-300",
                  )}
                >
                  <Percent className="w-8 h-8" />
                  <span className="font-medium">نسبة مئوية</span>
                </button>
                <button
                  onClick={() => setDiscountType("fixed")}
                  className={clsx(
                    "flex flex-col items-center gap-2 p-4 rounded-xl border-2 transition-all",
                    discountType === "fixed"
                      ? "border-primary-600 bg-primary-50 text-primary-600"
                      : "border-gray-200 hover:border-gray-300",
                  )}
                >
                  <DollarSign className="w-8 h-8" />
                  <span className="font-medium">مبلغ ثابت</span>
                </button>
              </div>
            </div>

            {/* Discount Value Input */}
            <div>
              <p className="text-sm font-medium text-gray-500 mb-3">
                {discountType === "percentage"
                  ? "نسبة الخصم (%)"
                  : "قيمة الخصم (ج.م)"}
              </p>
              <div className="text-center p-4 bg-gray-50 rounded-xl">
                <p className="text-3xl font-bold">
                  {discountValue || "0"}{" "}
                  <span className="text-lg text-gray-400">
                    {discountType === "percentage" ? "%" : "ج.م"}
                  </span>
                </p>
              </div>
            </div>

            {/* Quick Percentages */}
            {discountType === "percentage" && (
              <div className="flex gap-2">
                {quickPercentages.map((percent) => (
                  <button
                    key={percent}
                    onClick={() => setDiscountValue(percent.toString())}
                    className="flex-1 py-2 rounded-lg bg-gray-100 font-medium hover:bg-primary-100 hover:text-primary-600 transition-colors"
                  >
                    {percent}%
                  </button>
                ))}
              </div>
            )}

            {/* Numpad */}
            <div className="grid grid-cols-4 gap-2">
              {[
                "7",
                "8",
                "9",
                "←",
                "4",
                "5",
                "6",
                "C",
                "1",
                "2",
                "3",
                ".",
                "0",
                "00",
              ].map((key) => (
                <button
                  key={key}
                  onClick={() => handleNumpadClick(key)}
                  className={clsx(
                    "h-14 rounded-lg bg-gray-100 font-semibold text-xl hover:bg-gray-200 active:bg-gray-300 transition-colors",
                    key === "0" && "col-span-2",
                  )}
                >
                  {key}
                </button>
              ))}
            </div>

            {/* Reason */}
            <div>
              <p className="text-sm font-medium text-gray-500 mb-2">
                سبب الخصم (اختياري)
              </p>
              <input
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="مثال: عرض خاص، عميل مميز..."
                className="w-full px-4 py-3 border rounded-xl focus:outline-none focus:ring-2 focus:ring-primary-500"
              />
            </div>

            {/* Preview */}
            {previewDiscount > 0 && (
              <div className="space-y-2 p-4 bg-success-50 rounded-xl border border-success-200">
                <div className="flex justify-between text-sm text-gray-600">
                  <span>سعر المنتج</span>
                  <span>{formatCurrency(itemTotal)}</span>
                </div>
                <div className="flex justify-between text-sm text-success-600 font-medium">
                  <span>الخصم</span>
                  <span>- {formatCurrency(previewDiscount)}</span>
                </div>
                <div className="flex justify-between text-lg font-bold text-gray-800 pt-2 border-t border-success-200">
                  <span>بعد الخصم</span>
                  <span>{formatCurrency(previewTotal)}</span>
                </div>
              </div>
            )}

            {/* Action Buttons */}
            <div className="flex gap-3">
              {currentDiscount && (
                <Button
                  variant="danger"
                  size="lg"
                  className="flex-1"
                  onClick={handleRemove}
                >
                  إلغاء الخصم
                </Button>
              )}
              <Button
                variant="success"
                size="lg"
                className="flex-1"
                onClick={handleApply}
                disabled={numericValue <= 0}
                rightIcon={<Check className="w-5 h-5" />}
              >
                تطبيق الخصم
              </Button>
            </div>
          </div>
        </div>
      </div>
    </Portal>
  );
};
