import { useState } from "react";
import { Plus, Minus, Trash2, Tag } from "lucide-react";
import { CartItem } from "@/store/slices/cartSlice";
import { useCart } from "@/hooks/useCart";
import { formatCurrency } from "@/utils/formatters";
import {
  getCartItemDiscountAmount,
  getCartItemSubtotal,
  getProductNetUnitPrice,
} from "@/utils/cartPricing";
import { ItemDiscountModal } from "./ItemDiscountModal";

interface CartItemProps {
  item: CartItem;
}

export const CartItemComponent = ({ item }: CartItemProps) => {
  const {
    updateQuantity,
    removeItem,
    applyItemDiscount,
    removeItemDiscount,
    taxRate,
    isTaxEnabled,
    canManageDiscounts,
  } = useCart();
  const [showDiscountModal, setShowDiscountModal] = useState(false);
  const { product, quantity, discount } = item;
  const unitPrice = getProductNetUnitPrice(product, taxRate, isTaxEnabled);
  const subtotal = getCartItemSubtotal(item, taxRate, isTaxEnabled);
  const discountAmount = getCartItemDiscountAmount(item, taxRate, isTaxEnabled);
  const total = subtotal - discountAmount;

  return (
    <>
      <div className="flex gap-3 rounded-xl border border-gray-200 bg-white p-3 transition-colors hover:border-gray-300">
        {/* Image - with subtle border */}
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg border border-gray-200 bg-gray-100">
          <span className="text-2xl">{product.imageUrl || "📦"}</span>
        </div>

        {/* Details */}
        <div className="flex-1 min-w-0">
          <h4 className="truncate text-sm font-bold text-gray-900">
            {product.name}
          </h4>
          <p className="text-xs font-medium text-gray-500">
            {formatCurrency(unitPrice)} × {quantity}
          </p>

          {/* Item Discount Badge - with success color */}
          {discount && (
            <span className="mt-1 inline-flex items-center gap-1 rounded-md border border-green-200 bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700">
              <Tag className="w-3 h-3" />
              {discount.type === "percentage"
                ? `${discount.value}%`
                : formatCurrency(discount.value)}{" "}
              خصم
            </span>
          )}

          {/* Quantity Controls - with brand colors */}
          <div className="mt-3 flex items-center gap-2">
            <button
              onClick={() => updateQuantity(product.id, quantity - 1)}
              className="flex h-9 w-9 items-center justify-center rounded-lg border border-gray-300 bg-gray-50 hover:border-red-300 hover:bg-red-50 hover:text-red-500"
              aria-label={
                quantity === 1
                  ? `حذف ${product.name}`
                  : `تقليل كمية ${product.name}`
              }
            >
              {quantity === 1 ? (
                <Trash2 className="w-5 h-5 text-red-500" />
              ) : (
                <Minus className="w-5 h-5 text-gray-600" />
              )}
            </button>

            <span className="w-8 text-center text-base font-bold text-gray-900">{quantity}</span>

            <button
              onClick={() => updateQuantity(product.id, quantity + 1)}
              className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary-600 text-white hover:bg-primary-700"
              aria-label={`زيادة كمية ${product.name}`}
            >
              <Plus className="w-5 h-5" />
            </button>

            {/* Item Discount Button - with secondary accent */}
            {canManageDiscounts && (
              <button
                onClick={() => setShowDiscountModal(true)}
                className={`flex h-9 w-9 items-center justify-center rounded-lg border ${
                  discount
                    ? "bg-green-100 border-green-400 text-green-600"
                    : "bg-gray-50 border-gray-300 hover:border-gray-400 hover:bg-gray-100 text-gray-600"
                }`}
                aria-label={`خصم على ${product.name}`}
              >
                <Tag className="w-5 h-5" />
              </button>
            )}
          </div>
        </div>

        {/* Total - with brand color */}
        <div className="flex shrink-0 flex-col justify-between text-start">
          <div>
            {discountAmount > 0 ? (
              <>
                <p className="text-xs text-gray-400 line-through">
                  {formatCurrency(subtotal)}
                </p>
                <p className="text-base font-bold text-green-600">
                  {formatCurrency(total)}
                </p>
              </>
            ) : (
              <p className="text-base font-bold text-primary-600">
                {formatCurrency(total)}
              </p>
            )}
          </div>
          <button
            onClick={() => removeItem(product.id)}
            className="text-xs font-medium text-red-500 hover:text-red-600"
          >
            حذف
          </button>
        </div>
      </div>

      {canManageDiscounts && showDiscountModal && (
        <ItemDiscountModal
          item={item}
          onApply={(disc) => applyItemDiscount(product.id, disc)}
          onRemove={() => removeItemDiscount(product.id)}
          onClose={() => setShowDiscountModal(false)}
        />
      )}
    </>
  );
};
