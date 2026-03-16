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
    <div className="border-t pt-4 space-y-2">
      <div className="flex justify-between text-gray-600">
        <span>المجموع الفرعي</span>
        <span>{formatCurrency(subtotal)}</span>
      </div>

      {/* إظهار خصومات المنتجات */}
      {itemDiscountsTotal > 0 && (
        <div className="flex justify-between text-success-600">
          <span className="flex items-center gap-1">
            <Tag className="w-4 h-4" />
            <span>خصومات المنتجات</span>
          </span>
          <span>- {formatCurrency(itemDiscountsTotal)}</span>
        </div>
      )}

      {/* إظهار الخصم على الطلب إذا كان موجود */}
      {discountAmount > 0 && (
        <div className="flex justify-between text-success-600">
          <span className="flex items-center gap-1">
            {discountType === "Percentage" ? (
              <Percent className="w-4 h-4" />
            ) : (
              <DollarSign className="w-4 h-4" />
            )}
            <span>
              خصم الطلب
              {discountType === "Percentage" &&
                discountValue &&
                ` (${discountValue}%)`}
            </span>
          </span>
          <span>- {formatCurrency(discountAmount)}</span>
        </div>
      )}

      {/* إظهار الضريبة فقط إذا كانت مفعلة */}
      {isTaxEnabled && (
        <div className="flex justify-between text-gray-600">
          <span>الضريبة ({taxRate}%)</span>
          <span>{formatCurrency(taxAmount)}</span>
        </div>
      )}

      <div className="flex justify-between text-xl font-bold pt-2 border-t">
        <span>الإجمالي</span>
        <span className="text-primary-600">{formatCurrency(total)}</span>
      </div>
    </div>
  );
};
