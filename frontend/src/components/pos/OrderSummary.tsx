import { useCart } from "@/hooks/useCart";
import { formatCurrency } from "@/utils/formatters";
import { Percent, DollarSign, Tag } from "lucide-react";

export const OrderSummary = () => {
  const {
    subtotal,
    discountAmount,
    discountType,
    discountValue,
    itemDiscountsTotal,
    taxAmount,
    total,
    taxRate,
    isTaxEnabled,
  } = useCart();

  return (
    <div className="space-y-2.5">
      {/* Subtotal */}
      <div className="flex justify-between items-center">
        <span className="text-sm text-gray-600">المجموع الفرعي</span>
        <span className="text-sm font-semibold text-gray-900">{formatCurrency(subtotal)}</span>
      </div>

      {/* Item Discounts */}
      {itemDiscountsTotal > 0 && (
        <div className="flex justify-between items-center px-2.5 py-2 bg-emerald-50 rounded-md border border-emerald-100">
          <span className="flex items-center gap-1.5 text-sm font-medium text-emerald-700">
            <Tag className="w-3.5 h-3.5" strokeWidth={2} />
            <span>خصومات المنتجات</span>
          </span>
          <span className="text-sm font-bold text-emerald-700">- {formatCurrency(itemDiscountsTotal)}</span>
        </div>
      )}

      {/* Order Discount */}
      {discountAmount > 0 && (
        <div className="flex justify-between items-center px-2.5 py-2 bg-emerald-50 rounded-md border border-emerald-100">
          <span className="flex items-center gap-1.5 text-sm font-medium text-emerald-700">
            {discountType === "Percentage" ? (
              <Percent className="w-3.5 h-3.5" strokeWidth={2} />
            ) : (
              <DollarSign className="w-3.5 h-3.5" strokeWidth={2} />
            )}
            <span>
              خصم الطلب
              {discountType === "Percentage" && discountValue && ` (${discountValue}%)`}
            </span>
          </span>
          <span className="text-sm font-bold text-emerald-700">- {formatCurrency(discountAmount)}</span>
        </div>
      )}

      {/* Tax */}
      {isTaxEnabled && (
        <div className="flex justify-between items-center">
          <span className="text-sm text-gray-600">الضريبة ({taxRate}%)</span>
          <span className="text-sm font-semibold text-gray-900">{formatCurrency(taxAmount)}</span>
        </div>
      )}

      {/* Total */}
      <div className="flex justify-between items-center pt-2.5 border-t-2 border-gray-200">
        <span className="text-base font-bold text-gray-900">الإجمالي</span>
        <span className="text-2xl font-bold text-blue-600">
          {formatCurrency(total)}
        </span>
      </div>
    </div>
  );
};
