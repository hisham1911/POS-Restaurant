import { useEffect, useState } from "react";
import { Plus, Minus, Trash2, Tag, Layers } from "lucide-react";
import { CartItem } from "@/store/slices/cartSlice";
import { useCart } from "@/hooks/useCart";
import { formatCurrency } from "@/utils/formatters";
import {
  getCartItemDiscountAmount,
  getCartItemTotal,
  getProductNetUnitPrice,
} from "@/utils/cartPricing";
import { ItemDiscountModal } from "./ItemDiscountModal";
import { BatchSelectionModal } from "./BatchSelectionModal";
import { useGetAvailableBatchesQuery } from "@/api/productBatchApi";
import { usePermission } from "@/hooks/usePermission";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentBranch } from "@/store/slices/branchSlice";

interface CartItemProps {
  item: CartItem;
}

const isImageSource = (value?: string): boolean => {
  if (!value) return false;
  const normalized = value.trim();
  if (!normalized) return false;
  return /^(https?:\/\/|\/|data:image\/|blob:)/i.test(normalized);
};

export const CartItemComponent = ({ item }: CartItemProps) => {
  const {
    updateQuantity,
    applyItemDiscount,
    removeItemDiscount,
    updateItemBatch,
    taxRate,
    isTaxEnabled,
    canManageDiscounts,
  } = useCart();
  const { hasPermission } = usePermission();
  const currentBranch = useAppSelector(selectCurrentBranch);
  const [showDiscountModal, setShowDiscountModal] = useState(false);
  const [showBatchModal, setShowBatchModal] = useState(false);
  const [imageError, setImageError] = useState(false);
  const { product, quantity, discount } = item;
  const canChangeBatch = hasPermission("PosChangeBatch");
  const { data: batchesResponse } = useGetAvailableBatchesQuery(
    { productId: product.id, branchId: currentBranch?.id ?? 0 },
    {
      skip:
        (!showBatchModal && Boolean(item.batchId)) ||
        !currentBranch?.id ||
        !product.isBatchTracked,
    },
  );
  const batches = batchesResponse?.data ?? [];

  useEffect(() => {
    if (!product.isBatchTracked || item.batchId || batches.length === 0) {
      return;
    }

    const batch = batches[0];
    updateItemBatch(
      product.id,
      item.batchId,
      batch.id,
      batch.batchNumber,
      batch.expiryDate,
      batch.sellingPrice,
      batch.quantity,
    );
  }, [
    batches,
    item.batchId,
    product.id,
    product.isBatchTracked,
    updateItemBatch,
  ]);

  const unitPrice = getProductNetUnitPrice(product, taxRate, isTaxEnabled);
  const discountAmount = getCartItemDiscountAmount(item, taxRate, isTaxEnabled);
  const total = getCartItemTotal(item, taxRate, isTaxEnabled);
  const originalTotal =
    discountAmount > 0
      ? getCartItemTotal({ ...item, discount: undefined }, taxRate, isTaxEnabled)
      : total;
  const productIcon = product.imageUrl?.trim() || "";
  const hasProductImage = isImageSource(productIcon) && !imageError;

  return (
    <>
      <div className="rounded-xl border border-gray-200 bg-white p-3 transition-colors hover:border-gray-300">
        <div className="flex items-start gap-3">
          <div className="flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-gray-200 bg-gray-100">
            {hasProductImage ? (
              <img
                src={productIcon}
                alt={product.name}
                className="h-full w-full object-cover"
                onError={() => setImageError(true)}
              />
            ) : (
              <span className="text-2xl">{productIcon || "📦"}</span>
            )}
          </div>

          <div className="min-w-0 flex-1">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <h4 className="break-words text-sm font-bold leading-5 text-gray-900">
                  {product.name}
                </h4>
                <div className="mt-1 flex flex-wrap items-center gap-1.5 text-xs font-medium text-gray-500">
                  <span>
                    {formatCurrency(unitPrice)} × {quantity}
                  </span>
                  {item.batchId && (
                    <span className="rounded-md bg-gray-100 px-1.5 py-0.5 text-[11px] text-gray-600">
                      {item.batchNumber || "بدون رقم دفعة"}
                      {item.expiryDate
                        ? ` • ${new Date(item.expiryDate).toLocaleDateString(
                            "ar-EG",
                            {
                              year: "numeric",
                              month: "2-digit",
                              day: "2-digit",
                            },
                          )}`
                        : ""}
                    </span>
                  )}
                </div>
              </div>

              <div className="shrink-0 text-end">
                {discountAmount > 0 ? (
                  <>
                    <p className="text-xs text-gray-400 line-through">
                      {formatCurrency(originalTotal)}
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
            </div>

            {discount && (
              <span className="mt-2 inline-flex items-center gap-1 rounded-md border border-green-200 bg-green-50 px-2 py-0.5 text-xs font-medium text-green-700">
                <Tag className="h-3 w-3" />
                {discount.type === "percentage"
                  ? `${discount.value}%`
                  : formatCurrency(discount.value)}{" "}
                خصم
              </span>
            )}
          </div>
        </div>

        <div className="mt-3 flex items-center justify-between gap-2 border-t border-gray-100 pt-2.5">
          <div className="inline-flex items-center rounded-lg border border-gray-200 bg-gray-50 p-1">
            <button
              onClick={() => updateQuantity(product.id, quantity - 1, item.batchId)}
              className="flex h-8 w-8 items-center justify-center rounded-md text-gray-600 transition-colors hover:bg-red-50 hover:text-red-500"
              aria-label={
                quantity === 1
                  ? `حذف ${product.name}`
                  : `تقليل كمية ${product.name}`
              }
            >
              {quantity === 1 ? (
                <Trash2 className="h-4 w-4 text-red-500" />
              ) : (
                <Minus className="h-4 w-4" />
              )}
            </button>

            <span className="min-w-[34px] text-center text-base font-bold text-gray-900">
              {quantity}
            </span>

            <button
              onClick={() => updateQuantity(product.id, quantity + 1, item.batchId)}
              className="flex h-8 w-8 items-center justify-center rounded-md bg-primary-600 text-white transition-colors hover:bg-primary-700"
              aria-label={`زيادة كمية ${product.name}`}
            >
              <Plus className="h-4 w-4" />
            </button>
          </div>

          <div className="flex items-center gap-1.5">
            {canManageDiscounts && (
              <button
                onClick={() => setShowDiscountModal(true)}
                className={`flex h-9 w-9 items-center justify-center rounded-lg border transition-colors ${
                  discount
                    ? "bg-green-100 border-green-400 text-green-600"
                    : "bg-gray-50 border-gray-300 hover:border-gray-400 hover:bg-gray-100 text-gray-600"
                }`}
                aria-label={`خصم على ${product.name}`}
              >
                <Tag className="h-4 w-4" />
              </button>
            )}

            {product.isBatchTracked && canChangeBatch && (
              <button
                onClick={() => setShowBatchModal(true)}
                className="flex h-9 w-9 items-center justify-center rounded-lg border border-gray-300 bg-gray-50 text-gray-600 transition-colors hover:border-primary-300 hover:bg-primary-50 hover:text-primary-600"
                aria-label={`تغيير دفعة ${product.name}`}
              >
                <Layers className="h-4 w-4" />
              </button>
            )}
          </div>
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

      {product.isBatchTracked && canChangeBatch && showBatchModal && (
        <BatchSelectionModal
          isOpen={true}
          onClose={() => setShowBatchModal(false)}
          productName={product.name}
          batches={batches}
          selectedBatchId={item.batchId}
          onSelectBatch={(batch) => {
            updateItemBatch(
              product.id,
              item.batchId,
              batch.id,
              batch.batchNumber,
              batch.expiryDate,
              batch.sellingPrice,
              batch.quantity,
            );
            setShowBatchModal(false);
          }}
        />
      )}
    </>
  );
};
