import { Product } from "@/types/product.types";

type CartDiscount = {
  type: "percentage" | "fixed";
  value: number;
};

export interface CartPricingItem {
  product: Product;
  quantity: number;
  discount?: CartDiscount;
}

const round2 = (value: number): number => Math.round(value * 100) / 100;

export const getProductEffectiveTaxRate = (
  product: Product,
  fallbackTaxRate: number,
  isTaxEnabled: boolean,
): number => {
  if (!isTaxEnabled) {
    return 0;
  }

  return product.taxRate ?? fallbackTaxRate;
};

export const getProductNetUnitPrice = (
  product: Product,
  fallbackTaxRate: number,
  isTaxEnabled: boolean,
): number => {
  // Use suggestedPrice (batch price if available, otherwise base price)
  return product.suggestedPrice;
};

export const getCartItemSubtotal = (
  item: CartPricingItem,
  fallbackTaxRate: number,
  isTaxEnabled: boolean,
): number =>
  round2(
    getProductNetUnitPrice(item.product, fallbackTaxRate, isTaxEnabled) *
      item.quantity,
  );

export const getCartItemDiscountAmount = (
  item: CartPricingItem,
  fallbackTaxRate: number,
  isTaxEnabled: boolean,
): number => {
  if (!item.discount) {
    return 0;
  }

  const lineSubtotal = getCartItemSubtotal(item, fallbackTaxRate, isTaxEnabled);

  if (item.discount.type === "percentage") {
    return round2(
      Math.min(lineSubtotal * (item.discount.value / 100), lineSubtotal),
    );
  }

  return round2(Math.min(item.discount.value, lineSubtotal));
};

export const getCartItemNetAfterDiscount = (
  item: CartPricingItem,
  fallbackTaxRate: number,
  isTaxEnabled: boolean,
): number =>
  round2(
    getCartItemSubtotal(item, fallbackTaxRate, isTaxEnabled) -
      getCartItemDiscountAmount(item, fallbackTaxRate, isTaxEnabled),
  );

export const getCartItemTaxAmount = (
  item: CartPricingItem,
  fallbackTaxRate: number,
  isTaxEnabled: boolean,
): number => {
  const taxRate = getProductEffectiveTaxRate(
    item.product,
    fallbackTaxRate,
    isTaxEnabled,
  );

  return round2(
    getCartItemNetAfterDiscount(item, fallbackTaxRate, isTaxEnabled) *
      (taxRate / 100),
  );
};

export const getCartItemTotal = (
  item: CartPricingItem,
  fallbackTaxRate: number,
  isTaxEnabled: boolean,
): number =>
  round2(
    getCartItemNetAfterDiscount(item, fallbackTaxRate, isTaxEnabled) +
      getCartItemTaxAmount(item, fallbackTaxRate, isTaxEnabled),
  );
