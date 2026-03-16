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
      <div className="flex gap-3 p-3 bg-gray-50 rounded-xl">
        {/* Image */}
        <div className="w-16 h-16 bg-white rounded-lg flex items-center justify-center shrink-0">
          <span className="text-2xl">{product.imageUrl || "📦"}</span>
        </div>

        {/* Details */}
        <div className="flex-1 min-w-0">
          <h4 className="font-medium text-gray-800 truncate">{product.name}</h4>
          <p className="text-sm text-gray-500">
            {formatCurrency(product.price)}
          </p>

          {/* Item Discount Badge */}
          {discount && (
            <span className="inline-flex items-center gap-1 text-xs bg-success-100 text-success-700 px-2 py-0.5 rounded-full mt-1">
              <Tag className="w-3 h-3" />
              {discount.type === "percentage"
                ? `${discount.value}%`
                : formatCurrency(discount.value)}{" "}
              خصم
            </span>
          )}

          {/* Quantity Controls */}
          <div className="flex items-center gap-2 mt-2">
            <button
              onClick={() => updateQuantity(product.id, quantity - 1)}
              className="w-11 h-11 flex items-center justify-center bg-white rounded-lg border hover:bg-gray-100 active:scale-95 transition-transform"
              aria-label={
                quantity === 1
                  ? `حذف ${product.name}`
                  : `تقليل كمية ${product.name}`
              }
            >
              {quantity === 1 ? (
                <Trash2 className="w-5 h-5 text-danger-500" />
              ) : (
                <Minus className="w-5 h-5" />
              )}
            </button>

            <span className="w-8 text-center font-bold">{quantity}</span>

            <button
              onClick={() => updateQuantity(product.id, quantity + 1)}
              className="w-11 h-11 flex items-center justify-center bg-primary-600 text-white rounded-lg hover:bg-primary-700 active:scale-95 transition-transform"
              aria-label={`زيادة كمية ${product.name}`}
            >
              <Plus className="w-5 h-5" />
            </button>

            {/* Item Discount Button */}
            <button
              onClick={() => setShowDiscountModal(true)}
              className={`w-11 h-11 flex items-center justify-center rounded-lg border active:scale-95 transition-all ${
                discount
                  ? "bg-success-100 border-success-300 text-success-600 hover:bg-success-200"
                  : "bg-white hover:bg-gray-100"
              }`}
              aria-label={`خصم على ${product.name}`}
            >
              <Tag className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Total */}
        <div className="text-start shrink-0">
          {discountAmount > 0 ? (
            <>
              <p className="text-sm text-gray-400 line-through">
                {formatCurrency(total)}
              </p>
              <p className="font-bold text-success-600">
                {formatCurrency(total - discountAmount)}
              </p>
            </>
          ) : (
            <p className="font-bold text-primary-600">
              {formatCurrency(total)}
            </p>
          )}
          <button
            onClick={() => removeItem(product.id)}
            className="text-danger-500 text-sm hover:underline mt-1"
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
