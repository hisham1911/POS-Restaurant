import { useState } from "react";
import { Product } from "@/types/product.types";
import { Category } from "@/types/category.types";
import { useCart } from "@/hooks/useCart";
import { formatCurrency } from "@/utils/formatters";
import { useAppSelector } from "@/store/hooks";
import { selectAllowNegativeStock } from "@/store/slices/cartSlice";
import clsx from "clsx";
import {
  getProductAvailableStock,
  getProductCurrentStock,
  type BranchInventoryStockMap,
} from "@/utils/productStock";
import { Loader2 } from "lucide-react";

// Default low stock threshold if not provided
const DEFAULT_LOW_STOCK_THRESHOLD = 10;

const isImageSource = (value?: string): boolean => {
  if (!value) return false;
  const normalized = value.trim();
  if (!normalized) return false;
  return /^(https?:\/\/|\/|data:image\/|blob:)/i.test(normalized);
};

interface ProductCardProps {
  product: Product;
  category?: Category;
  onStockAdjust?: (product: Product) => void;
  showStockAdjust?: boolean;
  stockByProductId?: BranchInventoryStockMap;
  hasInventorySnapshot?: boolean;
  isInventoryLoading?: boolean;
}

export const ProductCard = ({
  product,
  category,
  onStockAdjust,
  showStockAdjust,
  stockByProductId,
  hasInventorySnapshot = false,
  isInventoryLoading = false,
}: ProductCardProps) => {
  const { items, addItem } = useCart();
  const [imageError, setImageError] = useState(false);
  const [categoryImageError, setCategoryImageError] = useState(false);
  const allowNegativeStock = useAppSelector(selectAllowNegativeStock);

  const productIcon = product.imageUrl?.trim() || "";
  const categoryIcon = category?.imageUrl?.trim() || "";
  const hasProductImage = isImageSource(productIcon) && !imageError;
  const hasCategoryImage = isImageSource(categoryIcon) && !categoryImageError;

  // Get quantity in cart for this product
  const cartItem = items.find((item) => item.product.id === product.id);
  const quantityInCart = cartItem?.quantity ?? 0;

  // Calculate available stock (stock - what's in cart)
  const totalStock = getProductCurrentStock(product, stockByProductId);
  const availableStock = hasInventorySnapshot
    ? getProductAvailableStock(product, quantityInCart, stockByProductId)
    : Number.POSITIVE_INFINITY;

  // If allowNegativeStock is enabled, always allow adding
  const canAddMore =
    allowNegativeStock ||
    !product.trackInventory ||
    !hasInventorySnapshot ||
    availableStock > 0;

  const handleClick = () => {
    if (product.isActive && canAddMore) {
      const productForCart = hasInventorySnapshot
        ? ({
            ...product,
            branchInventoryQuantity: totalStock,
          } as Product)
        : product;

      addItem(productForCart);
    }
  };

  const handleContextMenu = (e: React.MouseEvent) => {
    if (showStockAdjust && onStockAdjust) {
      e.preventDefault();
      onStockAdjust(product);
    }
  };

  // Stock badge logic - shows available stock (accounting for cart)
  const getStockBadge = () => {
    if (!product.trackInventory) return null;

    if (isInventoryLoading && !hasInventorySnapshot) {
      return (
        <span className="absolute top-2 left-2 px-2 py-1 text-xs font-bold rounded-lg bg-gray-800/90 text-white inline-flex items-center gap-1">
          <Loader2 className="w-3 h-3 animate-spin" />
        </span>
      );
    }

    if (!hasInventorySnapshot) {
      return null;
    }

    const threshold = product.lowStockThreshold ?? DEFAULT_LOW_STOCK_THRESHOLD;

    // Out of stock - danger red
    if (totalStock <= 0) {
      return (
        <span className="absolute top-2 left-2 px-2.5 py-1 text-xs font-bold rounded-lg bg-red-500 text-white">
          نفد
        </span>
      );
    }

    // All in cart - warning orange
    if (availableStock <= 0) {
      return (
        <span className="absolute top-2 left-2 px-2.5 py-1 text-xs font-bold rounded-lg bg-orange-500 text-white">
          في السلة
        </span>
      );
    }

    // Low stock - warning amber
    if (totalStock <= threshold) {
      return (
        <span className="absolute top-2 left-2 px-2.5 py-1 text-xs font-bold rounded-lg bg-amber-500 text-white">
          {availableStock}
        </span>
      );
    }

    // Normal stock - neutral with subtle warmth
    return (
      <span className="absolute top-2 left-2 px-2.5 py-1 text-xs font-bold rounded-lg bg-gray-800/80 text-white">
        {availableStock}
      </span>
    );
  };

  // Determine if product is out of stock (only if allowNegativeStock is disabled)
  const isOutOfStock =
    !allowNegativeStock &&
    product.trackInventory &&
    hasInventorySnapshot &&
    totalStock <= 0;
  const isDisabled = !product.isActive || isOutOfStock;
  const isInCart = quantityInCart > 0;

  return (
    <button
      onClick={handleClick}
      onContextMenu={handleContextMenu}
      disabled={isDisabled || !canAddMore}
      className={clsx(
        "relative rounded-xl overflow-hidden w-full text-right p-3",
        "bg-white border-2 transition-colors",
        isDisabled && "opacity-40 cursor-not-allowed",
        !isDisabled &&
          canAddMore &&
          "hover:border-primary-300 hover:bg-primary-50/50",
        !canAddMore && !isDisabled && "cursor-not-allowed opacity-60",
        isInCart && "border-primary-500 bg-primary-50",
      )}
      aria-label={`إضافة ${product.name} - ${formatCurrency(product.price)}`}
      aria-disabled={isDisabled}
    >
      {/* Image */}
      <div className="aspect-square bg-gray-100 rounded-lg mb-2 flex items-center justify-center overflow-hidden relative">
        {hasProductImage ? (
          <img
            src={productIcon}
            alt={product.name}
            className="w-full h-full object-cover"
            onError={() => setImageError(true)}
          />
        ) : productIcon ? (
          <span className="text-4xl">{productIcon}</span>
        ) : hasCategoryImage ? (
          <img
            src={categoryIcon}
            alt={category?.name || "category"}
            className="w-full h-full object-cover"
            onError={() => setCategoryImageError(true)}
          />
        ) : categoryIcon ? (
          <span className="text-4xl">{categoryIcon}</span>
        ) : (
          <span className="text-4xl">{"📦"}</span>
        )}

        {/* Stock Badge */}
        {getStockBadge()}
      </div>

      {/* Name */}
      <h3 className="font-bold text-gray-900 truncate text-base mb-1">
        {product.name}
      </h3>

      {/* Price */}
      <p className="text-lg font-bold text-primary-600">
        {formatCurrency(product.price)}
      </p>

      {/* In cart indicator */}
      {isInCart && !isDisabled && (
        <div className="absolute top-2 right-2 bg-primary-600 text-white text-xs font-bold px-2.5 py-1 rounded-lg">
          في السلة
        </div>
      )}

      {/* Out of stock overlay */}
      {isOutOfStock && (
        <div className="absolute inset-0 bg-white/95 flex items-center justify-center rounded-xl">
          <span className="bg-red-500 text-white px-4 py-2 rounded-lg text-sm font-bold">
            نفد المخزون
          </span>
        </div>
      )}

      {/* Inactive overlay */}
      {!product.isActive && !isOutOfStock && (
        <div className="absolute inset-0 bg-white/95 flex items-center justify-center rounded-xl">
          <span className="bg-gray-500 text-white px-4 py-2 rounded-lg text-sm font-bold">
            غير متوفر
          </span>
        </div>
      )}
    </button>
  );
};
