import type { Product } from "@/types/product.types";
import type { BranchInventory } from "@/types/inventory.types";

export type BranchInventoryStockMap = Record<number, number>;

export const buildBranchInventoryStockMap = (
  branchInventory: BranchInventory[] | undefined,
): BranchInventoryStockMap => {
  if (!branchInventory || branchInventory.length === 0) {
    return {};
  }

  return branchInventory.reduce<BranchInventoryStockMap>((acc, item) => {
    acc[item.productId] = item.quantity;
    return acc;
  }, {});
};

export const getProductCurrentStock = (
  product: Product,
  stockByProductId?: BranchInventoryStockMap,
): number => {
  if (!stockByProductId) {
    return 0;
  }

  return stockByProductId[product.id] ?? 0;
};

export const getProductAvailableStock = (
  product: Product,
  quantityInCart = 0,
  stockByProductId?: BranchInventoryStockMap,
): number =>
  product.trackInventory
    ? getProductCurrentStock(product, stockByProductId) - quantityInCart
    : Number.POSITIVE_INFINITY;

export const isProductOutOfStock = (
  product: Product,
  stockByProductId?: BranchInventoryStockMap,
): boolean =>
  product.trackInventory &&
  getProductCurrentStock(product, stockByProductId) <= 0;
