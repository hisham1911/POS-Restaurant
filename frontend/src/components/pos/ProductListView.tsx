import { Product } from "@/types/product.types";
import { Category } from "@/types/category.types";
import { useCart } from "@/hooks/useCart";
import { formatCurrency } from "@/utils/formatters";
import { useAppSelector } from "@/store/hooks";
import { selectAllowNegativeStock } from "@/store/slices/cartSlice";
import clsx from "clsx";
import { Package, AlertCircle, CheckCircle2, Minus } from "lucide-react";
import {
  getProductAvailableStock,
  getProductCurrentStock,
} from "@/utils/productStock";

interface ProductListViewProps {
  products: Product[];
  categories: Category[];
}

export const ProductListView = ({ products, categories }: ProductListViewProps) => {
  const { items, addItem } = useCart();
  const allowNegativeStock = useAppSelector(selectAllowNegativeStock);

  const handleProductClick = (product: Product) => {
    const cartItem = items.find((item) => item.product.id === product.id);
    const quantityInCart = cartItem?.quantity ?? 0;
    const totalStock = getProductCurrentStock(product);
    const availableStock = getProductAvailableStock(product, quantityInCart);
    const canAddMore = allowNegativeStock || !product.trackInventory || availableStock > 0;
    const isOutOfStock = !allowNegativeStock && product.trackInventory && totalStock <= 0;

    if (product.isActive && canAddMore && !isOutOfStock) {
      addItem(product);
    }
  };

  // Group products by category
  const groupedProducts = products.reduce((acc, product) => {
    const categoryId = product.categoryId || 0;
    if (!acc[categoryId]) {
      acc[categoryId] = [];
    }
    acc[categoryId].push(product);
    return acc;
  }, {} as Record<number, Product[]>);

  return (
    <div className="space-y-6">
      {Object.entries(groupedProducts).map(([categoryId, categoryProducts]) => {
        const category = categories.find((c) => c.id === Number(categoryId));
        const categoryName = category?.name || "غير مصنف";

        return (
          <div key={categoryId} className="space-y-3">
            {/* Category Header */}
            <div className="flex items-center gap-3 pb-2 border-b-2 border-gray-200">
              <div className="w-1 h-6 bg-primary-500 rounded-full" />
              <h3 className="text-lg font-bold text-gray-800">{categoryName}</h3>
              <span className="text-sm text-gray-500">
                ({categoryProducts.length})
              </span>
            </div>

            {/* Products List */}
            <div className="space-y-2">
              {categoryProducts.map((product) => {
                const cartItem = items.find((item) => item.product.id === product.id);
                const quantityInCart = cartItem?.quantity ?? 0;
                const totalStock = getProductCurrentStock(product);
                const availableStock = getProductAvailableStock(
                  product,
                  quantityInCart,
                );
                const canAddMore =
                  allowNegativeStock || !product.trackInventory || availableStock > 0;
                const isOutOfStock =
                  !allowNegativeStock && product.trackInventory && totalStock <= 0;
                const isLowStock =
                  product.trackInventory &&
                  totalStock > 0 &&
                  totalStock <= (product.lowStockThreshold ?? 10);

                return (
                  <button
                    key={product.id}
                    onClick={() => handleProductClick(product)}
                    disabled={!product.isActive || isOutOfStock || !canAddMore}
                    className={clsx(
                      "w-full flex items-center justify-between p-4 rounded-xl transition-all duration-200",
                      "border-2 text-right",
                      quantityInCart > 0
                        ? "border-primary-400 bg-primary-50 shadow-md"
                        : "border-gray-200 bg-white hover:border-primary-300 hover:shadow-sm",
                      (!product.isActive || isOutOfStock || !canAddMore) &&
                        "opacity-50 cursor-not-allowed",
                      product.isActive && canAddMore && !isOutOfStock && "active:scale-[0.98]"
                    )}
                  >
                    {/* Left: Name & Stock */}
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1">
                        <h4 className="font-semibold text-gray-800 truncate">
                          {product.name}
                        </h4>
                        {quantityInCart > 0 && (
                          <span className="px-2 py-0.5 bg-primary-600 text-white text-xs font-bold rounded-full">
                            {quantityInCart}
                          </span>
                        )}
                      </div>

                      {/* Stock Status */}
                      {product.trackInventory && (
                        <div className="flex items-center gap-2 text-sm">
                          {isOutOfStock ? (
                            <>
                              <AlertCircle className="w-4 h-4 text-red-500" />
                              <span className="text-red-600 font-medium">نفد المخزون</span>
                            </>
                          ) : isLowStock ? (
                            <>
                              <Minus className="w-4 h-4 text-amber-500" />
                              <span className="text-amber-600 font-medium">
                                متبقي {availableStock}
                              </span>
                            </>
                          ) : (
                            <>
                              <CheckCircle2 className="w-4 h-4 text-success-500" />
                              <span className="text-success-600 font-medium">
                                متاح {availableStock}
                              </span>
                            </>
                          )}
                        </div>
                      )}

                      {!product.isActive && (
                        <div className="flex items-center gap-2 text-sm">
                          <Package className="w-4 h-4 text-gray-400" />
                          <span className="text-gray-500">غير متوفر</span>
                        </div>
                      )}
                    </div>

                    {/* Right: Price */}
                    <div className="text-left shrink-0 ml-4">
                      <div className="text-xl font-bold text-primary-600">
                        {formatCurrency(product.price)}
                      </div>
                      {product.sku && (
                        <div className="text-xs text-gray-400 font-mono">
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
          <Package className="w-16 h-16 mx-auto mb-4 opacity-50" />
          <p className="text-lg font-medium">لا توجد منتجات</p>
          <p className="text-sm">جرب تغيير البحث أو الفلتر</p>
        </div>
      )}
    </div>
  );
};
