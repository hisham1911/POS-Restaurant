import { useState } from "react";
import { Product } from "@/types/product.types";
import { Category } from "@/types/category.types";
import { ProductCard } from "./ProductCard";
import { StockAdjustmentModal } from "./StockAdjustmentModal";
import { Package } from "lucide-react";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentUser } from "@/store/slices/authSlice";
import { BranchInventoryStockMap } from "@/utils/productStock";

interface ProductGridProps {
  products: Product[];
  categories?: Category[];
  stockByProductId?: BranchInventoryStockMap;
  hasInventorySnapshot?: boolean;
  isInventoryLoading?: boolean;
}

export const ProductGrid = ({
  products,
  categories,
  stockByProductId,
  hasInventorySnapshot = false,
  isInventoryLoading = false,
}: ProductGridProps) => {
  const [adjustingProduct, setAdjustingProduct] = useState<Product | null>(
    null,
  );
  const user = useAppSelector(selectCurrentUser);

  // Only Admin or SystemOwner can adjust stock
  const canAdjustStock = user?.role === "Admin" || user?.role === "SystemOwner";

  const handleStockAdjust = (product: Product) => {
    if (canAdjustStock && product.trackInventory) {
      setAdjustingProduct(product);
    }
  };

  if (products.length === 0) {
    return (
      <div className="text-center py-12 text-gray-400">
        <Package className="w-16 h-16 mx-auto mb-4" />
        <p className="text-lg">لا توجد منتجات في هذا التصنيف</p>
      </div>
    );
  }

  return (
    <>
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-3">
        {products.map((product) => (
          <ProductCard
            key={product.id}
            product={product}
            category={categories?.find((c) => c.id === product.categoryId)}
            onStockAdjust={handleStockAdjust}
            showStockAdjust={canAdjustStock && product.trackInventory}
            stockByProductId={stockByProductId}
            hasInventorySnapshot={hasInventorySnapshot}
            isInventoryLoading={isInventoryLoading}
          />
        ))}
      </div>

      {/* Stock Adjustment Modal */}
      {adjustingProduct && (
        <StockAdjustmentModal
          product={adjustingProduct}
          onClose={() => setAdjustingProduct(null)}
        />
      )}
    </>
  );
};
