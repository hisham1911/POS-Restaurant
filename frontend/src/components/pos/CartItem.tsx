import { useState } from "react";
import { Plus, Minus, Trash2, Tag } from "lucide-react";
import { CartItem } from "@/store/slices/cartSlice";
import { useCart } from "@/hooks/useCart";
import { formatCurrency } from "@/utils/formatters";
import { ItemDiscountModal } from "./ItemDiscountModal";

interface CartItemProps {
  item: CartItem;
}

export const CartItemComponent = ({ item }: CartItemProps) => {
  const { updateQuantity, removeItem, applyItemDiscount, removeItemDiscount } =
    useCart();
  const [showDiscountModal, setShowDiscountModal] = useState(false);
  const { product, quantity, discount } = item;
  const total = product.price * quantity;

  let discountAmount = 0;
  if (discount) {
    discountAmount =
      discount.type === "percentage"
        ? total * (discount.value / 100)
        : Math.min(discount.value, total);
  }

  return (
    <>
      <div className="flex gap-3 p-4 bg-gray-50 rounded-xl border-2 border-gray-200 hover:border-primary-200 transition-colors">
        {/* Image - with subtle border */}
        <div className="w-14 h-14 bg-gray-100 rounded-lg flex items-center justify-center shrink-0 border border-gray-200">
          <span className="text-2xl">{product.imageUrl || "📦"}</span>
        </div>

        {/* Details */}
        <div className="flex-1 min-w-0">
          <h4 className="font-bold text-gray-900 truncate text-base">
            {product.name}
          </h4>
          <p className="text-sm text-gray-500 font-medium">
            {formatCurrency(product.price)} × {quantity}
          </p>

          {/* Item Discount Badge - with success color */}
          {discount && (
            <span className="inline-flex items-center gap-1 text-xs bg-green-100 text-green-700 px-2.5 py-1 rounded-lg font-medium mt-1 border border-green-200">
              <Tag className="w-3 h-3" />
              {discount.type === "percentage"
                ? `${discount.value}%`
                : formatCurrency(discount.value)}{" "}
              خصم
            </span>
          )}

          {/* Quantity Controls - with brand colors */}
          <div className="flex items-center gap-2 mt-3">
            <button
              onClick={() => updateQuantity(product.id, quantity - 1)}
              className="w-10 h-10 flex items-center justify-center bg-white rounded-lg border-2 border-gray-300 hover:border-red-400 hover:bg-red-50 hover:text-red-500"
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

            <span className="w-10 text-center font-bold text-lg text-gray-900">{quantity}</span>

            <button
              onClick={() => updateQuantity(product.id, quantity + 1)}
              className="w-10 h-10 flex items-center justify-center bg-primary-600 text-white rounded-lg hover:bg-primary-700"
              aria-label={`زيادة كمية ${product.name}`}
            >
              <Plus className="w-5 h-5" />
            </button>

            {/* Item Discount Button - with secondary accent */}
            <button
              onClick={() => setShowDiscountModal(true)}
              className={`w-10 h-10 flex items-center justify-center rounded-lg border-2 ${
                discount
                  ? "bg-green-100 border-green-400 text-green-600"
                  : "bg-white border-gray-300 hover:border-secondary-400 hover:bg-secondary-50 text-gray-600"
              }`}
              aria-label={`خصم على ${product.name}`}
            >
              <Tag className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Total - with brand color */}
        <div className="text-start shrink-0 flex flex-col justify-between">
          <div>
            {discountAmount > 0 ? (
              <>
                <p className="text-sm text-gray-400 line-through">
                  {formatCurrency(total)}
                </p>
                <p className="font-bold text-green-600 text-lg">
                  {formatCurrency(total - discountAmount)}
                </p>
              </>
            ) : (
              <p className="font-bold text-primary-600 text-lg">
                {formatCurrency(total)}
              </p>
            )}
          </div>
          <button
            onClick={() => removeItem(product.id)}
            className="text-red-500 text-sm font-medium hover:text-red-600"
          >
            حذف
          </button>
        </div>
      </div>

      {showDiscountModal && (
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
