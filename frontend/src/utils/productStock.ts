import type { Product } from "@/types/product.types";

// TODO: This utility should eventually use BranchInventory API, not ProductDto.
export const getProductCurrentStock = (product: Product): number =>
  product.currentBranchStock ?? 0;

export const getProductAvailableStock = (
  product: Product,
  quantityInCart = 0,
): number =>
  product.trackInventory
    ? getProductCurrentStock(product) - quantityInCart
    : Number.POSITIVE_INFINITY;

export const isProductOutOfStock = (product: Product): boolean =>
  product.trackInventory && getProductCurrentStock(product) <= 0;
