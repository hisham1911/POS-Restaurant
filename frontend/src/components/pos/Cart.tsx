import {
  Armchair,
  MessageSquare,
  Pencil,
  ShoppingCart,
  Tag,
  Trash2,
  Truck,
} from "lucide-react";
import { useState } from "react";
import { useCart } from "@/hooks/useCart";
import { CartItemComponent } from "./CartItem";
import { OrderSummary } from "./OrderSummary";
import { formatCurrency } from "@/utils/formatters";
import { CustomerSearch } from "./CustomerSearch";
import { Customer } from "@/types/customer.types";
import { DiscountModal } from "./DiscountModal";
import { DeliveryDetailsModal } from "./DeliveryDetailsModal";
import type { OrderSource, OrderType } from "@/types/order.types";
import type { RestaurantTable } from "@/types/restaurant.types";
import clsx from "clsx";

interface CartProps {
  onCheckout: () => void;
  selectedCustomer: Customer | null;
  onCustomerSelect: (customer: Customer | null) => void;
  orderType: OrderType;
  onOrderTypeChange: (orderType: OrderType) => void;
  selectedTable?: RestaurantTable | null;
  onTableSelectClick?: () => void;
  deliveryAddress: string;
  onDeliveryAddressChange: (value: string) => void;
  deliveryFee: string;
  onDeliveryFeeChange: (value: string) => void;
  deliveryNotes: string;
  onDeliveryNotesChange: (value: string) => void;
  orderSource: OrderSource;
  onOrderSourceChange: (value: OrderSource) => void;
  externalOrderNumber: string;
  onExternalOrderNumberChange: (value: string) => void;
  orderNotes: string;
  onOrderNotesChange: (value: string) => void;
  onSavedNotesClick: () => void;
}

const orderTypes: Array<{ value: OrderType; label: string }> = [
  { value: "DineIn", label: "ØµØ§Ù„Ø©" },
  { value: "Takeaway", label: "ØªÙŠÙƒ Ø£ÙˆØ§ÙŠ" },
  { value: "Delivery", label: "Ø¯Ù„ÙŠÙØ±ÙŠ" },
];

const orderSources: Array<{ value: OrderSource; label: string }> = [
  { value: "POS", label: "POS" },
  { value: "Talabat", label: "Ø·Ù„Ø¨Ø§Øª" },
  { value: "Marsool", label: "Ù…Ø±Ø³ÙˆÙ„" },
  { value: "Jahez", label: "Ø¬Ø§Ù‡Ø²" },
  { value: "Other", label: "Ø£Ø®Ø±Ù‰" },
];

export const Cart = ({
  onCheckout,
  selectedCustomer,
  onCustomerSelect,
  orderType,
  onOrderTypeChange,
  selectedTable,
  onTableSelectClick,
  deliveryAddress,
  onDeliveryAddressChange,
  deliveryFee,
  onDeliveryFeeChange,
  deliveryNotes,
  onDeliveryNotesChange,
  orderSource,
  onOrderSourceChange,
  externalOrderNumber,
  onExternalOrderNumberChange,
  orderNotes,
  onOrderNotesChange,
  onSavedNotesClick,
}: CartProps) => {
  const {
    items,
    clearCart,
    total,
    itemsCount,
    discountAmount,
    canManageDiscounts,
  } = useCart();
  const [showDiscountModal, setShowDiscountModal] = useState(false);
  const [showDeliveryModal, setShowDeliveryModal] = useState(false);
  const [isNotesExpanded, setIsNotesExpanded] = useState(false);
  const hasDraftOrder = false;
  const draftOrderTotal = 0;
  const parsedDeliveryFee =
    orderType === "Delivery"
      ? Number.parseFloat(deliveryFee || "0") || 0
      : 0;
  const checkoutTotal = total + parsedDeliveryFee;
  const hasDeliveryDetails =
    deliveryAddress.trim().length > 0 ||
    deliveryFee.trim().length > 0 ||
    deliveryNotes.trim().length > 0;

  return (
    <div className="grid h-full min-h-0 grid-rows-[auto_auto_minmax(0,1fr)_auto] bg-white">
      <div className="border-b border-gray-100 px-3 py-3">
        <CustomerSearch
          selectedCustomer={selectedCustomer}
          onCustomerSelect={onCustomerSelect}
        />

        <div className="mt-3 space-y-2.5">
          <div className="flex items-center gap-2">
            <div className="grid flex-1 grid-cols-3 rounded-xl bg-gray-100 p-1">
              {orderTypes.map((type) => (
                <button
                  key={type.value}
                  type="button"
                  onClick={() => onOrderTypeChange(type.value)}
                  className={clsx(
                    "min-h-[38px] rounded-lg px-2 py-2 text-sm font-bold transition-all",
                    orderType === type.value
                      ? "bg-primary-600 text-white"
                      : "text-gray-600 hover:bg-white",
                  )}
                >
                  {type.label}
                </button>
              ))}
            </div>
            {hasDraftOrder && (
              <span className="shrink-0 rounded-full bg-primary-100 px-2.5 py-1.5 text-xs font-bold text-primary-700">
                Ù…ÙØªÙˆØ­
              </span>
            )}
          </div>

          {orderType === "DineIn" && (
            <button
              type="button"
              onClick={onTableSelectClick}
              disabled={hasDraftOrder}
              className={clsx(
                "flex min-h-[42px] w-full items-center justify-between gap-3 rounded-lg border px-3 py-2 text-start text-sm font-bold transition",
                selectedTable
                  ? "border-emerald-200 bg-emerald-50 text-emerald-800"
                  : "border-dashed border-primary-200 bg-primary-50 text-primary-800",
                hasDraftOrder && "cursor-not-allowed opacity-80",
              )}
            >
              <span className="flex items-center gap-2">
                <Armchair className="h-4 w-4" />
                {selectedTable ? `طاولة ${selectedTable.number}` : "اختيار طاولة"}
              </span>
              {!hasDraftOrder && <span className="text-xs">ØªØºÙŠÙŠØ±</span>}
            </button>
          )}

          {orderType === "Delivery" && (
            <div className="space-y-2">
              <div className="grid grid-cols-2 gap-2">
                <select
                  value={orderSource}
                  onChange={(event) =>
                    onOrderSourceChange(event.target.value as OrderSource)
                  }
                  disabled={hasDraftOrder}
                  className="min-h-[40px] rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm font-medium outline-none focus:border-primary-500"
                >
                  {orderSources.map((source) => (
                    <option key={source.value} value={source.value}>
                      {source.label}
                    </option>
                  ))}
                </select>
                <input
                  type="text"
                  value={externalOrderNumber}
                  onChange={(event) =>
                    onExternalOrderNumberChange(event.target.value)
                  }
                  disabled={hasDraftOrder || orderSource === "POS"}
                  placeholder="Ø±Ù‚Ù… Ø®Ø§Ø±Ø¬ÙŠ"
                  className="min-h-[40px] rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm font-medium outline-none focus:border-primary-500 disabled:bg-gray-100"
                />
              </div>

              <button
                type="button"
                onClick={() => setShowDeliveryModal(true)}
              disabled={hasDraftOrder}
              className="flex w-full items-start justify-between gap-3 rounded-lg border border-primary-100 bg-primary-50/80 px-3 py-2 text-start transition-colors hover:bg-primary-100 disabled:cursor-not-allowed disabled:opacity-70"
            >
                <div className="min-w-0">
                  <div className="flex items-center gap-2 text-sm font-bold text-primary-700">
                    <Truck className="h-4 w-4 shrink-0" />
                    <span>Ø¨ÙŠØ§Ù†Ø§Øª Ø§Ù„Ø¯Ù„ÙŠÙØ±ÙŠ</span>
                  </div>
                  <p className="mt-1 truncate text-xs text-gray-600">
                    {hasDeliveryDetails
                      ? deliveryAddress || "ØªÙ…Øª Ø¥Ø¶Ø§ÙØ© Ø¨ÙŠØ§Ù†Ø§Øª Ø§Ù„Ø¯Ù„ÙŠÙØ±ÙŠ"
                      : "Ø£Ø¶Ù Ø§Ù„Ø¹Ù†ÙˆØ§Ù† ÙˆØ§Ù„Ø±Ø³ÙˆÙ…"}
                  </p>
                  <p className="mt-1 text-xs font-semibold text-gray-900">
                    Ø§Ù„Ø±Ø³ÙˆÙ…:{" "}
                    {formatCurrency(Number.parseFloat(deliveryFee || "0") || 0)}
                  </p>
                </div>

                <span className="flex shrink-0 items-center gap-1.5 rounded-md bg-white px-2.5 py-1.5 text-xs font-bold text-primary-700 shadow-sm">
                  <Pencil className="h-3.5 w-3.5" />
                  <span>{hasDeliveryDetails ? "ØªØ¹Ø¯ÙŠÙ„" : "Ø¥Ø¶Ø§ÙØ©"}</span>
                </span>
              </button>
            </div>
          )}

          <div className="grid grid-cols-[minmax(0,1fr)_auto] gap-2">
            <textarea
              value={orderNotes}
              onChange={(event) => onOrderNotesChange(event.target.value)}
              onFocus={() => setIsNotesExpanded(true)}
              onBlur={() => {
                if (!orderNotes.trim()) setIsNotesExpanded(false);
              }}
              rows={isNotesExpanded || orderNotes ? 2 : 1}
              placeholder="Ù…Ù„Ø§Ø­Ø¸Ø§Øª Ø§Ù„Ø·Ù„Ø¨: Ø¨Ø¯ÙˆÙ† Ø¨ØµÙ„ØŒ Ø­Ø§Ø±..."
              className="min-h-[42px] w-full resize-none rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm outline-none transition-colors focus:border-primary-500"
            />
            <button
              type="button"
              onClick={onSavedNotesClick}
              className="flex h-[42px] w-[42px] shrink-0 items-center justify-center rounded-lg border border-amber-200 bg-amber-50 text-amber-600 transition-colors hover:bg-amber-100"
              title="Ù…Ù„Ø§Ø­Ø¸Ø§Øª Ø³Ø±ÙŠØ¹Ø©"
              aria-label="Ù…Ù„Ø§Ø­Ø¸Ø§Øª Ø³Ø±ÙŠØ¹Ø©"
            >
              <MessageSquare className="h-4 w-4" />
            </button>
          </div>
        </div>
      </div>

      <div className="flex items-center justify-between border-b border-gray-100 px-3 py-2.5">
        <div className="flex items-center gap-2">
          <span className="text-base font-bold text-gray-900">Ø§Ù„Ø·Ù„Ø¨</span>
          <span className="flex h-6 min-w-[24px] items-center justify-center rounded-md bg-blue-600 px-2 text-xs font-bold text-white">
            {itemsCount}
          </span>
        </div>
        {items.length > 0 && (
          <button
            onClick={clearCart}
            className="flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-sm font-medium text-red-600 transition-colors hover:bg-red-50"
          >
            <Trash2 className="h-4 w-4" />
            <span className="hidden sm:inline">Ø¥ÙØ±Ø§Øº</span>
          </button>
        )}
      </div>

      <div className="min-h-0 space-y-2.5 overflow-y-auto px-3 py-3">
        {items.length === 0 ? (
          <div className="flex h-full min-h-[180px] flex-col items-center justify-center px-4 text-center">
            <div className="mb-4 flex h-20 w-20 items-center justify-center rounded-full bg-gray-50">
              <ShoppingCart className="h-10 w-10 text-gray-300" strokeWidth={1.5} />
            </div>
            <p className="mb-1 text-lg font-semibold text-gray-900">
              {hasDraftOrder ? "Ù„Ø§ ØªÙˆØ¬Ø¯ Ø¥Ø¶Ø§ÙØ§Øª Ø¬Ø¯ÙŠØ¯Ø©" : "Ø§Ù„Ø³Ù„Ø© ÙØ§Ø±ØºØ©"}
            </p>
            <p className="text-sm text-gray-500">
              {hasDraftOrder
                ? "ÙŠÙ…ÙƒÙ†Ùƒ Ø¥Ø±Ø³Ø§Ù„ Ø¥Ø¶Ø§ÙØ§Øª Ø¬Ø¯ÙŠØ¯Ø© Ù„Ù„Ù…Ø·Ø¨Ø® Ø£Ùˆ Ø¥ØªÙ…Ø§Ù… Ø§Ù„Ø¯ÙØ¹."
                : "Ø§Ø¨Ø¯Ø£ Ø¨Ø¥Ø¶Ø§ÙØ© Ø§Ù„Ù…Ù†ØªØ¬Ø§Øª"}
            </p>
          </div>
        ) : (
          items.map((item) => (
            <CartItemComponent
              key={`${item.product.id}-${item.batchId ?? "default"}`}
              item={item}
            />
          ))
        )}
      </div>

      <div className="border-t border-gray-100 bg-white px-3 py-3">
        {(items.length > 0 || hasDraftOrder) && (
          <>
            {items.length > 0 && (
              <OrderSummary
                isDeliveryOrder={orderType === "Delivery" && !hasDraftOrder}
                deliveryFee={parsedDeliveryFee}
              />
            )}

            {hasDraftOrder && (
              <div className="mt-3 rounded-lg border border-primary-100 bg-white p-3 text-sm">
                <div className="flex items-center justify-between">
                  <span className="font-medium text-gray-600">Ø¥Ø¬Ù…Ø§Ù„ÙŠ Ø§Ù„Ø·Ù„Ø¨ Ø§Ù„Ù…ÙØªÙˆØ­</span>
                  <span className="font-bold text-primary-700">
                    {formatCurrency(draftOrderTotal)}
                  </span>
                </div>
              </div>
            )}
          </>
        )}

        <div className="mt-3 space-y-2">
          {canManageDiscounts && items.length > 0 ? (
            <div className="grid gap-2">
              <button
                onClick={() => setShowDiscountModal(true)}
                className="flex min-h-[44px] items-center justify-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm font-bold text-gray-700 transition-colors hover:border-gray-300 hover:bg-gray-50"
              >
                <Tag className="h-4 w-4" />
                {discountAmount > 0 ? "تعديل الخصم" : "خصم"}
              </button>
            </div>
          ) : null}

          <button
            onClick={onCheckout}
            disabled={checkoutTotal <= 0}
            className="flex min-h-[50px] w-full items-center justify-center gap-2 rounded-lg bg-green-600 px-4 py-3 text-base font-bold text-white shadow-sm transition-all hover:bg-green-700 active:scale-[0.98] disabled:cursor-not-allowed disabled:opacity-60"
          >
            <span>Ø§Ù„Ø¯ÙØ¹</span>
            <span className="text-lg">{formatCurrency(checkoutTotal)}</span>
          </button>
        </div>
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
