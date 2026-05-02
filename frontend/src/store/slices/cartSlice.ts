import { createSlice, PayloadAction } from "@reduxjs/toolkit";
import { Product } from "../../types/product.types";
import {
  getCartItemDiscountAmount,
  getCartItemNetAfterDiscount,
  getCartItemSubtotal,
  getCartItemTaxAmount,
  getProductEffectiveTaxRate,
} from "@/utils/cartPricing";

type ProductWithBranchInventoryQuantity = Product & {
  branchInventoryQuantity?: number;
};

const getBranchInventoryQuantity = (product: Product): number | undefined => {
  const quantity = (product as ProductWithBranchInventoryQuantity)
    .branchInventoryQuantity;

  return typeof quantity === "number" ? quantity : undefined;
};

export type DiscountType = "Percentage" | "Fixed";

export interface ItemDiscount {
  type: "percentage" | "fixed";
  value: number;
  reason?: string;
}

export interface CartItem {
  product: Product;
  quantity: number;
  notes?: string;
  discount?: ItemDiscount;
  // Batch tracking fields
  batchId?: number;
  batchNumber?: string;
  expiryDate?: string;
}

interface CartState {
  items: CartItem[];
  taxRate: number;
  isTaxEnabled: boolean;
  serviceChargeRate: number;
  allowNegativeStock: boolean;
  // Order-level discount
  discountType?: DiscountType;
  discountValue?: number;
}

const initialState: CartState = {
  items: [],
  taxRate: 0,
  isTaxEnabled: true,
  serviceChargeRate: 0,
  allowNegativeStock: false, // Default: don't allow selling when stock is 0
  discountType: undefined,
  discountValue: undefined,
};

const cartSlice = createSlice({
  name: "cart",
  initialState,
  reducers: {
    addItem: (
      state,
      action: PayloadAction<{ 
        product: Product; 
        quantity?: number;
        batchId?: number;
        batchNumber?: string;
        expiryDate?: string;
        batchSellingPrice?: number;
        batchQuantity?: number;
      }>,
    ) => {
      const { product, quantity = 1, batchId, batchNumber, expiryDate, batchSellingPrice, batchQuantity } = action.payload;
      const productForCart = {
        ...product,
        ...(typeof batchSellingPrice === "number" ? { price: batchSellingPrice } : {}),
        ...(typeof batchQuantity === "number" ? { branchInventoryQuantity: batchQuantity } : {}),
      } as ProductWithBranchInventoryQuantity;
      const existingItem = state.items.find(
        (item) => item.product.id === product.id && item.batchId === batchId,
      );

      // Check stock availability
      const currentQty = existingItem?.quantity ?? 0;
      const newQty = currentQty + quantity;
      const branchInventoryQuantity = productForCart.trackInventory
        ? getBranchInventoryQuantity(productForCart)
        : undefined;
      const hasInventoryData = typeof branchInventoryQuantity === "number";
      const availableStock = hasInventoryData
        ? branchInventoryQuantity
        : Infinity;

      // Negative stock is never allowed for batch-tracked lines without a real batch quantity.
      if (state.allowNegativeStock && !productForCart.isBatchTracked) {
        if (existingItem) {
          existingItem.quantity = newQty;
        } else {
          state.items.push({ 
            product: productForCart, 
            quantity,
            batchId,
            batchNumber,
            expiryDate,
          });
        }
        return;
      }

      // Don't allow adding if track inventory and exceeds stock
      if (
        product.trackInventory &&
        hasInventoryData &&
        newQty > availableStock
      ) {
        // Limit to available stock
        const maxAddable = Math.max(0, availableStock - currentQty);
        if (maxAddable <= 0) return; // Can't add more

        if (existingItem) {
          existingItem.quantity = availableStock;
        } else {
          state.items.push({ 
            product: productForCart, 
            quantity: maxAddable,
            batchId,
            batchNumber,
            expiryDate,
          });
        }
        return;
      }

      if (existingItem) {
        existingItem.quantity = newQty;
      } else {
        state.items.push({ 
            product: productForCart, 
            quantity,
            batchId,
            batchNumber,
          expiryDate,
        });
      }
    },

    removeItem: (state, action: PayloadAction<{ productId: number; batchId?: number }>) => {
      state.items = state.items.filter(
        (item) =>
          item.product.id !== action.payload.productId ||
          item.batchId !== action.payload.batchId,
      );
    },

    updateQuantity: (
      state,
      action: PayloadAction<{ productId: number; quantity: number; batchId?: number }>,
    ) => {
      const { productId, quantity, batchId } = action.payload;

      if (quantity <= 0) {
        state.items = state.items.filter(
          (item) => item.product.id !== productId || item.batchId !== batchId,
        );
        return;
      }

      const item = state.items.find(
        (item) => item.product.id === productId && item.batchId === batchId,
      );
      if (item) {
        if (state.allowNegativeStock && !item.product.isBatchTracked) {
          item.quantity = quantity;
          return;
        }
        // Check stock availability
        const branchInventoryQuantity = item.product.trackInventory
          ? getBranchInventoryQuantity(item.product)
          : undefined;
        const hasInventoryData = typeof branchInventoryQuantity === "number";
        const availableStock = hasInventoryData
          ? branchInventoryQuantity
          : Infinity;

        if (
          item.product.trackInventory &&
          hasInventoryData &&
          quantity > availableStock
        ) {
          item.quantity = availableStock; // Limit to available stock
        } else {
          item.quantity = quantity;
        }
      }
    },

    updateNotes: (
      state,
      action: PayloadAction<{ productId: number; notes: string }>,
    ) => {
      const { productId, notes } = action.payload;
      const item = state.items.find((item) => item.product.id === productId);
      if (item) {
        item.notes = notes;
      }
    },

    clearCart: (state) => {
      state.items = [];
      state.discountType = undefined;
      state.discountValue = undefined;
    },

    // تحديث إعدادات الضريبة والمخزون من بيانات الشركة
    setTaxSettings: (
      state,
      action: PayloadAction<{
        taxRate: number;
        isTaxEnabled: boolean;
        serviceChargeRate?: number;
        allowNegativeStock?: boolean;
      }>,
    ) => {
      state.taxRate = action.payload.taxRate;
      state.isTaxEnabled = action.payload.isTaxEnabled;
      if (action.payload.serviceChargeRate !== undefined) {
        state.serviceChargeRate = action.payload.serviceChargeRate;
      }
      if (action.payload.allowNegativeStock !== undefined) {
        state.allowNegativeStock = action.payload.allowNegativeStock;
      }
    },

    setServiceChargeRate: (state, action: PayloadAction<number>) => {
      state.serviceChargeRate = action.payload;
    },

    // تطبيق خصم على الطلب
    setDiscount: (
      state,
      action: PayloadAction<
        | {
            type: DiscountType;
            value: number;
          }
        | undefined
      >,
    ) => {
      if (!action.payload) {
        state.discountType = undefined;
        state.discountValue = undefined;
      } else {
        state.discountType = action.payload.type;
        // Clamp percentage discounts at 100%
        state.discountValue =
          action.payload.type === "Percentage"
            ? Math.min(Math.max(0, action.payload.value), 100)
            : Math.max(0, action.payload.value);
      }
    },

    // تطبيق خصم على منتج بعينه
    setItemDiscount: (
      state,
      action: PayloadAction<{
        productId: number;
        discount?: ItemDiscount;
      }>,
    ) => {
      const item = state.items.find(
        (i) => i.product.id === action.payload.productId,
      );
      if (item) {
        item.discount = action.payload.discount;
      }
    },

    // تحديث الباتش لمنتج في السلة
    updateItemBatch: (
      state,
      action: PayloadAction<{
        productId: number;
        currentBatchId?: number;
        batchId: number;
        batchNumber?: string;
        expiryDate: string;
        sellingPrice?: number;
        batchQuantity?: number;
      }>,
    ) => {
      const item = state.items.find(
        (i) =>
          i.product.id === action.payload.productId &&
          i.batchId === action.payload.currentBatchId,
      );
      if (item) {
        const duplicate = state.items.find(
          (i) =>
            i !== item &&
            i.product.id === action.payload.productId &&
            i.batchId === action.payload.batchId,
        );

        if (duplicate) {
          duplicate.quantity += item.quantity;
          state.items = state.items.filter((i) => i !== item);
          return;
        }

        item.batchId = action.payload.batchId;
        item.batchNumber = action.payload.batchNumber;
        item.expiryDate = action.payload.expiryDate;
        if (typeof action.payload.sellingPrice === "number") {
          item.product = { ...item.product, price: action.payload.sellingPrice };
        }
        if (typeof action.payload.batchQuantity === "number") {
          item.product = {
            ...item.product,
            branchInventoryQuantity: action.payload.batchQuantity,
          } as ProductWithBranchInventoryQuantity;
          if (item.quantity > action.payload.batchQuantity) {
            item.quantity = action.payload.batchQuantity;
          }
        }
      }
    },
  },
});

export const {
  addItem,
  removeItem,
  updateQuantity,
  updateNotes,
  clearCart,
  setTaxSettings,
  setServiceChargeRate,
  setDiscount,
  setItemDiscount,
  updateItemBatch,
} = cartSlice.actions;

// Selectors
export const selectCartItems = (state: { cart: CartState }) => state.cart.items;
export const selectTaxRate = (state: { cart: CartState }) => state.cart.taxRate;
export const selectIsTaxEnabled = (state: { cart: CartState }) =>
  state.cart.isTaxEnabled;

export const selectServiceChargeRate = (state: { cart: CartState }) =>
  state.cart.serviceChargeRate;

export const selectAllowNegativeStock = (state: { cart: CartState }) =>
  state.cart.allowNegativeStock;

export const selectDiscountType = (state: { cart: CartState }) =>
  state.cart.discountType;

export const selectDiscountValue = (state: { cart: CartState }) =>
  state.cart.discountValue;

export const selectItemsCount = (state: { cart: CartState }) =>
  state.cart.items.reduce((sum, item) => sum + item.quantity, 0);

/**
 * Total item-level discounts across all cart items
 */
export const selectItemDiscountsTotal = (state: { cart: CartState }) =>
  Math.round(
    state.cart.items.reduce(
      (sum, item) =>
        sum +
        getCartItemDiscountAmount(
          item,
          state.cart.taxRate,
          state.cart.isTaxEnabled,
        ),
      0,
    ) * 100,
  ) / 100;

/**
 * Subtotal = Sum of all item net prices before discounts and tax
 */
export const selectSubtotal = (state: { cart: CartState }) =>
  Math.round(
    state.cart.items.reduce(
      (sum, item) =>
        sum +
        getCartItemSubtotal(item, state.cart.taxRate, state.cart.isTaxEnabled),
      0,
    ) * 100,
  ) / 100;

/**
 * Calculate order-level discount amount based on type and value
 */
export const selectDiscountAmount = (state: { cart: CartState }) => {
  if (!state.cart.discountType || !state.cart.discountValue) return 0;

  const subtotal = state.cart.items.reduce(
    (sum, item) =>
      sum +
      getCartItemSubtotal(item, state.cart.taxRate, state.cart.isTaxEnabled),
    0,
  );

  // Subtract item-level discounts first
  const itemDiscounts = state.cart.items.reduce(
    (sum, item) =>
      sum +
      getCartItemDiscountAmount(
        item,
        state.cart.taxRate,
        state.cart.isTaxEnabled,
      ),
    0,
  );
  const afterItemDiscounts = subtotal - itemDiscounts;

  let discountAmount = 0;
  if (state.cart.discountType === "Percentage") {
    discountAmount = afterItemDiscounts * (state.cart.discountValue / 100);
  } else {
    discountAmount = state.cart.discountValue;
  }

  // Discount cannot exceed remaining amount
  return Math.round(Math.min(discountAmount, afterItemDiscounts) * 100) / 100;
};

/**
 * Tax Exclusive (Additive): Tax is calculated on (Subtotal - ItemDiscounts - OrderDiscount)
 */
export const selectTaxAmount = (state: { cart: CartState }) => {
  if (!state.cart.isTaxEnabled) return 0;

  const subtotal = state.cart.items.reduce(
    (sum, item) =>
      sum +
      getCartItemSubtotal(item, state.cart.taxRate, state.cart.isTaxEnabled),
    0,
  );

  // Item-level discounts
  const itemDiscounts = state.cart.items.reduce(
    (sum, item) =>
      sum +
      getCartItemDiscountAmount(
        item,
        state.cart.taxRate,
        state.cart.isTaxEnabled,
      ),
    0,
  );

  // Order-level discount
  const afterItemDiscounts = subtotal - itemDiscounts;
  let orderDiscount = 0;
  if (state.cart.discountType && state.cart.discountValue) {
    if (state.cart.discountType === "Percentage") {
      orderDiscount = afterItemDiscounts * (state.cart.discountValue / 100);
    } else {
      orderDiscount = state.cart.discountValue;
    }
    orderDiscount = Math.min(orderDiscount, afterItemDiscounts);
  }

  if (orderDiscount > 0 && afterItemDiscounts > 0) {
    const discountRatio = orderDiscount / afterItemDiscounts;
    return (
      Math.round(
        state.cart.items.reduce((sum, item) => {
          const itemNetAfterItemDiscount = getCartItemNetAfterDiscount(
            item,
            state.cart.taxRate,
            state.cart.isTaxEnabled,
          );
          const itemTaxRate = getProductEffectiveTaxRate(
            item.product,
            state.cart.taxRate,
            state.cart.isTaxEnabled,
          );

          return (
            sum +
            itemNetAfterItemDiscount * (1 - discountRatio) * (itemTaxRate / 100)
          );
        }, 0) * 100,
      ) / 100
    );
  }

  return (
    Math.round(
      state.cart.items.reduce(
        (sum, item) =>
          sum +
          getCartItemTaxAmount(
            item,
            state.cart.taxRate,
            state.cart.isTaxEnabled,
          ),
        0,
      ) * 100,
    ) / 100
  );
};

export const selectServiceChargeAmount = (state: { cart: CartState }) => {
  const subtotal = state.cart.items.reduce(
    (sum, item) =>
      sum +
      getCartItemSubtotal(item, state.cart.taxRate, state.cart.isTaxEnabled),
    0,
  );

  const itemDiscounts = state.cart.items.reduce(
    (sum, item) =>
      sum +
      getCartItemDiscountAmount(
        item,
        state.cart.taxRate,
        state.cart.isTaxEnabled,
      ),
    0,
  );

  const afterItemDiscounts = subtotal - itemDiscounts;
  const orderDiscount = selectDiscountAmount(state);
  const afterAllDiscounts = afterItemDiscounts - orderDiscount;

  if (state.cart.serviceChargeRate <= 0 || afterAllDiscounts <= 0) {
    return 0;
  }

  return (
    Math.round(
      afterAllDiscounts * (state.cart.serviceChargeRate / 100) * 100,
    ) / 100
  );
};

/**
 * Total = Subtotal - ItemDiscounts - OrderDiscount + Tax + ServiceCharge
 */
export const selectTotal = (state: { cart: CartState }) => {
  const subtotal = state.cart.items.reduce(
    (sum, item) =>
      sum +
      getCartItemSubtotal(item, state.cart.taxRate, state.cart.isTaxEnabled),
    0,
  );

  // Item-level discounts
  const itemDiscounts = state.cart.items.reduce(
    (sum, item) =>
      sum +
      getCartItemDiscountAmount(
        item,
        state.cart.taxRate,
        state.cart.isTaxEnabled,
      ),
    0,
  );

  // Order-level discount
  const afterItemDiscounts = subtotal - itemDiscounts;
  let orderDiscount = 0;
  if (state.cart.discountType && state.cart.discountValue) {
    if (state.cart.discountType === "Percentage") {
      orderDiscount = afterItemDiscounts * (state.cart.discountValue / 100);
    } else {
      orderDiscount = state.cart.discountValue;
    }
    orderDiscount = Math.min(orderDiscount, afterItemDiscounts);
  }

  const afterAllDiscounts = afterItemDiscounts - orderDiscount;
  const serviceChargeAmount = selectServiceChargeAmount(state);

  if (!state.cart.isTaxEnabled) {
    return Math.round((afterAllDiscounts + serviceChargeAmount) * 100) / 100;
  }

  const taxAmount = selectTaxAmount(state);
  return Math.round((afterAllDiscounts + taxAmount + serviceChargeAmount) * 100) / 100;
};

export default cartSlice.reducer;
