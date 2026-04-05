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
      <div className="flex items-center justify-center py-16 text-gray-400">
        <div className="text-center">
          <div className="w-20 h-20 mx-auto mb-4 rounded-2xl bg-gray-100 flex items-center justify-center">
            <Package className="w-10 h-10 text-gray-300" />
          </div>
          <p className="text-lg font-bold text-gray-600">لا توجد منتجات</p>
          <p className="text-sm text-gray-400 mt-1">جرب تغيير الفلتر أو البحث</p>
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-4 xl:grid-cols-5 gap-4">
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
