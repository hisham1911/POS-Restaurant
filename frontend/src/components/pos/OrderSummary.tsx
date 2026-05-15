import { useCart } from "@/hooks/useCart";
import { formatCurrency } from "@/utils/formatters";
import { Percent, DollarSign, Tag, Truck } from "lucide-react";

interface OrderSummaryProps {
  isDeliveryOrder?: boolean;
  deliveryFee?: number;
}

export const OrderSummary = ({
  isDeliveryOrder = false,
  deliveryFee = 0,
}: OrderSummaryProps) => {
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
    serviceChargeAmount,
    serviceChargeRate,
  } = useCart();

  const grandTotal = total + (isDeliveryOrder ? deliveryFee : 0);

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between">
        <span className="text-xs text-gray-600">المجموع الفرعي</span>
        <span className="text-xs font-semibold text-gray-900">
          {formatCurrency(subtotal)}
        </span>
      </div>

      {itemDiscountsTotal > 0 && (
        <div className="flex items-center justify-between rounded-md border border-emerald-100 bg-emerald-50 px-2.5 py-1.5">
          <span className="flex items-center gap-1.5 text-xs font-medium text-emerald-700">
            <Tag className="h-3.5 w-3.5" strokeWidth={2} />
            <span>خصومات المنتجات</span>
          </span>
          <span className="text-xs font-bold text-emerald-700">
            - {formatCurrency(itemDiscountsTotal)}
          </span>
        </div>
      )}

      {discountAmount > 0 && (
        <div className="flex items-center justify-between rounded-md border border-emerald-100 bg-emerald-50 px-2.5 py-1.5">
          <span className="flex items-center gap-1.5 text-xs font-medium text-emerald-700">
            {discountType === "Percentage" ? (
              <Percent className="h-3.5 w-3.5" strokeWidth={2} />
            ) : (
              <DollarSign className="h-3.5 w-3.5" strokeWidth={2} />
            )}
            <span>
              خصم الطلب
              {discountType === "Percentage" && discountValue
                ? ` (${discountValue}%)`
                : ""}
            </span>
          </span>
          <span className="text-xs font-bold text-emerald-700">
            - {formatCurrency(discountAmount)}
          </span>
        </div>
      )}

      {isTaxEnabled && (
        <div className="flex items-center justify-between">
          <span className="text-xs text-gray-600">الضريبة ({taxRate}%)</span>
          <span className="text-xs font-semibold text-gray-900">
            {formatCurrency(taxAmount)}
          </span>
        </div>
      )}

      {serviceChargeAmount > 0 && (
        <div className="flex items-center justify-between">
          <span className="text-xs text-gray-600">
            رسوم الخدمة ({serviceChargeRate}%)
          </span>
          <span className="text-xs font-semibold text-gray-900">
            {formatCurrency(serviceChargeAmount)}
          </span>
        </div>
      )}

      {isDeliveryOrder && (
        <div className="flex items-center justify-between">
          <span className="flex items-center gap-1.5 text-xs text-gray-600">
            <Truck className="h-3.5 w-3.5 text-primary-500" strokeWidth={2} />
            <span>رسوم التوصيل</span>
          </span>
          <span className="text-xs font-semibold text-gray-900">
            {formatCurrency(deliveryFee)}
          </span>
        </div>
      )}

      <div className="flex items-center justify-between border-t-2 border-gray-200 pt-2">
        <span className="text-base font-bold text-gray-900">الإجمالي</span>
        <span className="text-xl font-bold text-blue-600">
          {formatCurrency(grandTotal)}
        </span>
      </div>

      <p className="text-[11px] text-gray-400">
        * الإجمالي تقديري، ويتم تأكيده نهائيًا عند إنشاء الطلب.
      </p>
    </div>
  );
};
