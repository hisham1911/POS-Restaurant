import { ShoppingCart, Trash2, Tag, X } from "lucide-react";
import { useCart } from "@/hooks/useCart";
import { CartItemComponent } from "./CartItem";
import { OrderSummary } from "./OrderSummary";
import { Button } from "@/components/common/Button";
import { formatCurrency } from "@/utils/formatters";
import { CustomerSearch } from "./CustomerSearch";
import { Customer } from "@/types/customer.types";
import { useState } from "react";
import { DiscountModal } from "./DiscountModal";

interface CartProps {
  onCheckout: () => void;
  selectedCustomer: Customer | null;
  onCustomerSelect: (customer: Customer | null) => void;
}

export const Cart = ({
  onCheckout,
  selectedCustomer,
  onCustomerSelect,
}: CartProps) => {
  const { items, clearCart, total, itemsCount, discountAmount } = useCart();
  const [showDiscountModal, setShowDiscountModal] = useState(false);

  if (items.length === 0) {
    return (
      <div className="h-full flex flex-col bg-white">
        {/* Customer Search */}
        <div className="p-3 lg:p-4 border-b border-gray-100">
          <CustomerSearch
            selectedCustomer={selectedCustomer}
            onCustomerSelect={onCustomerSelect}
          />
        </div>

        <div className="flex-1 flex flex-col items-center justify-center px-4">
          <div className="w-24 h-24 rounded-full bg-gray-50 flex items-center justify-center mb-4">
            <ShoppingCart className="w-12 h-12 text-gray-300" strokeWidth={1.5} />
          </div>
          <p className="text-lg font-semibold text-gray-900 mb-1">السلة فارغة</p>
          <p className="text-sm text-gray-500">ابدأ بإضافة المنتجات</p>
        </div>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col bg-white">
      {/* Customer Search */}
      <div className="p-3 lg:p-4 border-b border-gray-100">
        <CustomerSearch
          selectedCustomer={selectedCustomer}
          onCustomerSelect={onCustomerSelect}
        />
      </div>

      {/* Header */}
      <div className="flex items-center justify-between px-3 lg:px-4 py-3 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <span className="text-base lg:text-lg font-bold text-gray-900">الطلب</span>
          <span className="min-w-[24px] h-6 px-2 bg-blue-600 text-white text-xs font-bold rounded-md flex items-center justify-center">
            {itemsCount}
          </span>
        </div>
        <button
          onClick={clearCart}
          className="flex items-center gap-1.5 text-red-600 text-sm font-medium hover:bg-red-50 px-2.5 py-1.5 rounded-md transition-colors"
        >
          <Trash2 className="w-4 h-4" strokeWidth={2} />
          <span className="hidden sm:inline">إفراغ</span>
        </button>
      </div>

      {/* Items */}
      <div className="flex-1 overflow-y-auto px-3 lg:px-4 py-3 space-y-2">
        {items.map((item) => (
          <CartItemComponent key={item.product.id} item={item} />
        ))}
      </div>

      {/* Summary */}
      <div className="border-t border-gray-100 px-3 lg:px-4 py-3 bg-gray-50">
        <OrderSummary />
      </div>

      {/* Actions */}
      <div className="p-3 lg:p-4 space-y-2 border-t border-gray-100">
        <button
          onClick={() => setShowDiscountModal(true)}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 border-2 border-gray-200 rounded-lg text-sm font-medium text-gray-700 hover:border-gray-300 hover:bg-gray-50 transition-colors"
        >
          <Tag className="w-4 h-4" strokeWidth={2} />
          {discountAmount > 0 ? "تعديل الخصم" : "إضافة خصم"}
        </button>

        <button
          onClick={onCheckout}
          className="w-full flex items-center justify-center gap-2 px-4 py-3.5 bg-green-600 text-white rounded-lg text-base font-bold hover:bg-green-700 active:scale-[0.98] transition-all shadow-sm"
        >
          <span>الدفع</span>
          <span className="text-lg">{formatCurrency(total)}</span>
        </button>
      </div>

      {/* Discount Modal */}
      {showDiscountModal && (
        <DiscountModal onClose={() => setShowDiscountModal(false)} />
      )}
    </div>
  );
};
