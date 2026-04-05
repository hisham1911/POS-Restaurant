import { Product } from "@/types/product.types";
import { Category } from "@/types/category.types";
import { useCart } from "@/hooks/useCart";
import { formatCurrency } from "@/utils/formatters";
import { useAppSelector } from "@/store/hooks";
import { selectAllowNegativeStock } from "@/store/slices/cartSlice";
import clsx from "clsx";
import {
  Package,
  AlertCircle,
  CheckCircle2,
  Minus,
  Loader2,
} from "lucide-react";
import {
  getProductAvailableStock,
  getProductCurrentStock,
  type BranchInventoryStockMap,
} from "@/utils/productStock";

interface ProductListViewProps {
  products: Product[];
  categories: Category[];
  stockByProductId?: BranchInventoryStockMap;
  hasInventorySnapshot?: boolean;
  isInventoryLoading?: boolean;
}

export const ProductListView = ({
  products,
  categories,
  stockByProductId,
  hasInventorySnapshot = false,
  isInventoryLoading = false,
}: ProductListViewProps) => {
  const { items, addItem } = useCart();
  const allowNegativeStock = useAppSelector(selectAllowNegativeStock);

  const handleProductClick = (product: Product) => {
    const cartItem = items.find((item) => item.product.id === product.id);
    const quantityInCart = cartItem?.quantity ?? 0;
    const totalStock = getProductCurrentStock(product, stockByProductId);
    const availableStock = hasInventorySnapshot
      ? getProductAvailableStock(product, quantityInCart, stockByProductId)
      : Number.POSITIVE_INFINITY;
    const canAddMore =
      allowNegativeStock ||
      !product.trackInventory ||
      !hasInventorySnapshot ||
      availableStock > 0;
    const isOutOfStock =
      !allowNegativeStock &&
      product.trackInventory &&
      hasInventorySnapshot &&
      totalStock <= 0;

    if (product.isActive && canAddMore && !isOutOfStock) {
      const productForCart = hasInventorySnapshot
        ? ({
            ...product,
            branchInventoryQuantity: totalStock,
          } as Product)
        : product;

      addItem(productForCart);
    }
  };

  // Group products by category
  const groupedProducts = products.reduce(
    (acc, product) => {
      const categoryId = product.categoryId || 0;
      if (!acc[categoryId]) {
        acc[categoryId] = [];
      }
      acc[categoryId].push(product);
      return acc;
    },
    {} as Record<number, Product[]>,
  );

  return (
    <div className="space-y-5">
      {Object.entries(groupedProducts).map(([categoryId, categoryProducts]) => {
        const category = categories.find((c) => c.id === Number(categoryId));
        const categoryName = category?.name || "غير مصنف";

        return (
          <div key={categoryId} className="space-y-2.5">
            {/* Category Header */}
            <div className="flex items-center gap-2.5 pb-2">
              <div className="w-1 h-5 bg-blue-600 rounded-full" />
              <h3 className="text-base font-bold text-gray-900">
                {categoryName}
              </h3>
              <span className="text-xs font-medium text-gray-500 bg-gray-100 px-2 py-0.5 rounded-full">
                {categoryProducts.length}
              </span>
            </div>

            {/* Products List */}
            <div className="space-y-1.5">
              {categoryProducts.map((product) => {
                const cartItem = items.find(
                  (item) => item.product.id === product.id,
                );
                const quantityInCart = cartItem?.quantity ?? 0;
                const totalStock = getProductCurrentStock(
                  product,
                  stockByProductId,
                );
                const availableStock = hasInventorySnapshot
                  ? getProductAvailableStock(
                      product,
                      quantityInCart,
                      stockByProductId,
                    )
                  : Number.POSITIVE_INFINITY;
                const canAddMore =
                  allowNegativeStock ||
                  !product.trackInventory ||
                  !hasInventorySnapshot ||
                  availableStock > 0;
                const isOutOfStock =
                  !allowNegativeStock &&
                  product.trackInventory &&
                  hasInventorySnapshot &&
                  totalStock <= 0;
                const isLowStock =
                  product.trackInventory &&
                  hasInventorySnapshot &&
                  totalStock > 0 &&
                  totalStock <= (product.lowStockThreshold ?? 10);

                return (
                  <button
                    key={product.id}
                    onClick={() => handleProductClick(product)}
                    disabled={!product.isActive || isOutOfStock || !canAddMore}
                    className={clsx(
                      "w-full flex items-center justify-between min-h-[56px]",
                      "px-3 py-2.5 rounded-lg transition-all duration-150",
                      "border-2 text-start",
                      quantityInCart > 0
                        ? "border-blue-500 bg-blue-50"
                        : "border-gray-200 bg-white hover:border-gray-300 hover:bg-gray-50",
                      (!product.isActive || isOutOfStock || !canAddMore) &&
                        "opacity-50 cursor-not-allowed",
                      product.isActive &&
                        canAddMore &&
                        !isOutOfStock &&
                        "active:scale-[0.99]",
                    )}
                  >
                    {/* Left: Name & Stock */}
                    <div className="flex-1 min-w-0 pe-3">
                      <div className="flex items-center gap-2 mb-1">
                        <h4 className="text-sm font-semibold text-gray-900 truncate">
                          {product.name}
                        </h4>
                        {quantityInCart > 0 && (
                          <span className="shrink-0 min-w-[20px] h-5 px-1.5 bg-blue-600 text-white text-xs font-bold rounded flex items-center justify-center">
                            {quantityInCart}
                          </span>
                        )}
                      </div>

                      {/* Stock Status */}
                      {product.trackInventory && (
                        <div className="flex items-center gap-1.5 text-xs">
                          {isInventoryLoading && !hasInventorySnapshot ? (
                            <>
                              <Loader2 className="w-3.5 h-3.5 text-gray-400 animate-spin shrink-0" />
                              <span className="text-gray-500 font-medium">-</span>
                            </>
                          ) : !hasInventorySnapshot ? (
                            <>
                              <Loader2 className="w-3.5 h-3.5 text-gray-400 shrink-0" />
                              <span className="text-gray-500 font-medium">-</span>
                            </>
                          ) : isOutOfStock ? (
                            <>
                              <AlertCircle className="w-3.5 h-3.5 text-red-600 shrink-0" />
                              <span className="text-red-600 font-semibold">نفد المخزون</span>
                            </>
                          ) : isLowStock ? (
                            <>
                              <Minus className="w-3.5 h-3.5 text-amber-600 shrink-0" />
                              <span className="text-amber-600 font-semibold">
                                متبقي {availableStock}
                              </span>
                            </>
                          ) : (
                            <>
                              <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600 shrink-0" />
                              <span className="text-emerald-600 font-semibold">
                                متاح {availableStock}
                              </span>
                            </>
                          )}
                        </div>
                      )}

                      {!product.isActive && (
                        <div className="flex items-center gap-1.5 text-xs">
                          <Package className="w-3.5 h-3.5 text-gray-400 shrink-0" />
                          <span className="text-gray-500 font-medium">غير متوفر</span>
                        </div>
                      )}
                    </div>

                    {/* Right: Price */}
                    <div className="text-end shrink-0">
                      <div className="text-lg font-bold text-gray-900">
                        {formatCurrency(product.price)}
                      </div>
                      {product.sku && (
                        <div className="text-[10px] text-gray-400 font-mono mt-0.5">
                          {product.sku}
                        </div>
                      )}
                    </div>
                  </button>
                );
              })}
            </div>
          </div>
        );
      })}

      {products.length === 0 && (
        <div className="text-center py-12 text-gray-400">
          <Package className="w-16 h-16 mx-auto mb-4 opacity-30" strokeWidth={1.5} />
          <p className="text-base font-semibold text-gray-600 mb-1">لا توجد منتجات</p>
          <p className="text-sm text-gray-500">جرب تغيير البحث أو الفلتر</p>
        </div>
      )}
    </div>
  );
};
