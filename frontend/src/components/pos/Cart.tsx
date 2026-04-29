import { Pencil, ShoppingCart, Trash2, Tag, Truck } from "lucide-react";
import { useState } from "react";
import { useCart } from "@/hooks/useCart";
import { CartItemComponent } from "./CartItem";
import { OrderSummary } from "./OrderSummary";
import { formatCurrency } from "@/utils/formatters";
import { CustomerSearch } from "./CustomerSearch";
import { Customer } from "@/types/customer.types";
import { DiscountModal } from "./DiscountModal";
import { DeliveryDetailsModal } from "./DeliveryDetailsModal";
import clsx from "clsx";

interface CartProps {
  onCheckout: () => void;
  selectedCustomer: Customer | null;
  onCustomerSelect: (customer: Customer | null) => void;
  orderType: "Standard" | "Delivery";
  onOrderTypeChange: (orderType: "Standard" | "Delivery") => void;
  deliveryAddress: string;
  onDeliveryAddressChange: (value: string) => void;
  deliveryFee: string;
  onDeliveryFeeChange: (value: string) => void;
  deliveryNotes: string;
  onDeliveryNotesChange: (value: string) => void;
}

export const Cart = ({
  onCheckout,
  selectedCustomer,
  onCustomerSelect,
  orderType,
  onOrderTypeChange,
  deliveryAddress,
  onDeliveryAddressChange,
  deliveryFee,
  onDeliveryFeeChange,
  deliveryNotes,
  onDeliveryNotesChange,
}: CartProps) => {
  const { items, clearCart, total, itemsCount, discountAmount, canManageDiscounts } = useCart();
  const [showDiscountModal, setShowDiscountModal] = useState(false);
  const [showDeliveryModal, setShowDeliveryModal] = useState(false);
  const parsedDeliveryFee =
    orderType === "Delivery" ? Number.parseFloat(deliveryFee || "0") || 0 : 0;
  const checkoutTotal = total + parsedDeliveryFee;
  const hasDeliveryDetails =
    deliveryAddress.trim().length > 0 ||
    deliveryFee.trim().length > 0 ||
    deliveryNotes.trim().length > 0;

  if (items.length === 0) {
    return (
      <div className="h-full flex flex-col bg-white">
        <div className="border-b border-gray-100 p-3 lg:p-4">
          <CustomerSearch
            selectedCustomer={selectedCustomer}
            onCustomerSelect={onCustomerSelect}
          />
        </div>

        <div className="flex-1 flex flex-col items-center justify-center px-4">
          <div className="mb-4 flex h-24 w-24 items-center justify-center rounded-full bg-gray-50">
            <ShoppingCart className="h-12 w-12 text-gray-300" strokeWidth={1.5} />
          </div>
          <p className="mb-1 text-lg font-semibold text-gray-900">
            السلة فارغة
          </p>
          <p className="text-sm text-gray-500">ابدأ بإضافة المنتجات</p>
        </div>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col bg-white">
      <div className="border-b border-gray-100 p-3 lg:p-4">
        <CustomerSearch
          selectedCustomer={selectedCustomer}
          onCustomerSelect={onCustomerSelect}
        />
      </div>

      <div className="flex items-center justify-between border-b border-gray-100 px-3 py-3 lg:px-4">
        <div className="flex items-center gap-2">
          <span className="text-base font-bold text-gray-900 lg:text-lg">
            الطلب
          </span>
          <span className="flex h-6 min-w-[24px] items-center justify-center rounded-md bg-blue-600 px-2 text-xs font-bold text-white">
            {itemsCount}
          </span>
        </div>
        <button
          onClick={clearCart}
          className="flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-sm font-medium text-red-600 transition-colors hover:bg-red-50"
        >
          <Trash2 className="h-4 w-4" strokeWidth={2} />
          <span className="hidden sm:inline">إفراغ</span>
        </button>
      </div>

      <div className="flex-1 space-y-2 overflow-y-auto px-3 py-3 lg:px-4">
        {items.map((item) => (
          <CartItemComponent key={item.product.id} item={item} />
        ))}
      </div>

      <div className="border-t border-gray-100 bg-gray-50 px-3 py-3 lg:px-4">
        <OrderSummary
          isDeliveryOrder={orderType === "Delivery"}
          deliveryFee={parsedDeliveryFee}
        />

        <div className="mt-3 rounded-xl border border-gray-200 bg-white p-3">
          <p className="mb-2 text-xs font-bold uppercase tracking-[0.12em] text-gray-400">
            نوع الطلب
          </p>

          <div className="grid grid-cols-2 gap-2">
            {[
              { value: "Standard" as const, label: "طلب عادي" },
              { value: "Delivery" as const, label: "توصيل" },
            ].map((type) => (
              <button
                key={type.value}
                type="button"
                onClick={() => {
                  onOrderTypeChange(type.value);
                  if (type.value === "Delivery") {
                    setShowDeliveryModal(true);
                  }
                }}
                className={clsx(
                  "rounded-lg px-3 py-2 text-sm font-semibold transition-all",
                  orderType === type.value
                    ? "bg-primary-600 text-white"
                    : "border border-gray-200 bg-white text-gray-700 hover:bg-gray-50",
                )}
              >
                {type.label}
              </button>
            ))}
          </div>

          {orderType === "Delivery" && (
            <div className="mt-3 rounded-lg border border-primary-100 bg-primary-50 p-3">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="flex items-center gap-2 text-sm font-bold text-primary-700">
                    <Truck className="h-4 w-4 shrink-0" strokeWidth={2} />
                    <span>بيانات التوصيل</span>
                  </div>
                  <p className="mt-1 truncate text-xs text-gray-600">
                    {hasDeliveryDetails
                      ? deliveryAddress || "تمت إضافة بيانات التوصيل"
                      : "أضف العنوان والرسوم من نافذة التوصيل"}
                  </p>
                  <p className="mt-1 text-xs font-semibold text-gray-900">
                    الرسوم: {formatCurrency(parsedDeliveryFee)}
                  </p>
                </div>

                <button
                  type="button"
                  onClick={() => setShowDeliveryModal(true)}
                  className="flex shrink-0 items-center gap-1.5 rounded-md bg-white px-2.5 py-1.5 text-xs font-bold text-primary-700 shadow-sm transition-colors hover:bg-primary-100"
                >
                  <Pencil className="h-3.5 w-3.5" strokeWidth={2} />
                  <span>{hasDeliveryDetails ? "تعديل" : "إضافة"}</span>
                </button>
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="space-y-2 border-t border-gray-100 p-3 lg:p-4">
        {canManageDiscounts && (
          <button
            onClick={() => setShowDiscountModal(true)}
            className="flex w-full items-center justify-center gap-2 rounded-lg border-2 border-gray-200 px-4 py-2.5 text-sm font-medium text-gray-700 transition-colors hover:border-gray-300 hover:bg-gray-50"
          >
            <Tag className="h-4 w-4" strokeWidth={2} />
            {discountAmount > 0 ? "تعديل الخصم" : "إضافة خصم"}
          </button>
        )}

        <button
          onClick={onCheckout}
          className="flex w-full items-center justify-center gap-2 rounded-lg bg-green-600 px-4 py-3.5 text-base font-bold text-white shadow-sm transition-all hover:bg-green-700 active:scale-[0.98]"
        >
          <span>الدفع</span>
          <span className="text-lg">{formatCurrency(checkoutTotal)}</span>
        </button>
      </div>

      {canManageDiscounts && showDiscountModal && (
        <DiscountModal onClose={() => setShowDiscountModal(false)} />
      )}

      <DeliveryDetailsModal
        isOpen={showDeliveryModal}
        onClose={() => setShowDeliveryModal(false)}
        deliveryAddress={deliveryAddress}
        onDeliveryAddressChange={onDeliveryAddressChange}
        deliveryFee={deliveryFee}
        onDeliveryFeeChange={onDeliveryFeeChange}
        deliveryNotes={deliveryNotes}
        onDeliveryNotesChange={onDeliveryNotesChange}
        orderTotal={total}
      />
    </div>
  );
};
