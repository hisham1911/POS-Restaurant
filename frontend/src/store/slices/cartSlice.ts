import { createSlice, PayloadAction } from "@reduxjs/toolkit";
import { Product } from "../../types/product.types";
import { getProductCurrentStock } from "../../utils/productStock";

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
}

interface CartState {
  items: CartItem[];
  taxRate: number;
  isTaxEnabled: boolean;
  allowNegativeStock: boolean;
  // Order-level discount
  discountType?: DiscountType;
  discountValue?: number;
}

const initialState: CartState = {
  items: [],
  taxRate: 0,
  isTaxEnabled: true,
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
      action: PayloadAction<{ product: Product; quantity?: number }>,
    ) => {
      const { product, quantity = 1 } = action.payload;
      const existingItem = state.items.find(
        (item) => item.product.id === product.id,
      );

      // Check stock availability
      const currentQty = existingItem?.quantity ?? 0;
      const newQty = currentQty + quantity;
      const availableStock = product.trackInventory
        ? getProductCurrentStock(product)
        : Infinity;

      // If allowNegativeStock is enabled, skip stock check
      if (state.allowNegativeStock) {
        if (existingItem) {
          existingItem.quantity = newQty;
        } else {
          state.items.push({ product, quantity });
        }
        return;
      }

      // Don't allow adding if track inventory and exceeds stock
      if (product.trackInventory && newQty > availableStock) {
        // Limit to available stock
        const maxAddable = Math.max(0, availableStock - currentQty);
        if (maxAddable <= 0) return; // Can't add more

        if (existingItem) {
          existingItem.quantity = availableStock;
        } else {
          state.items.push({ product, quantity: maxAddable });
        }
        return;
      }

      if (existingItem) {
        existingItem.quantity = newQty;
      } else {
        state.items.push({ product, quantity });
      }
    },

    removeItem: (state, action: PayloadAction<number>) => {
      state.items = state.items.filter(
        (item) => item.product.id !== action.payload,
      );
    },

    updateQuantity: (
      state,
      action: PayloadAction<{ productId: number; quantity: number }>,
    ) => {
      const { productId, quantity } = action.payload;

      if (quantity <= 0) {
        state.items = state.items.filter(
          (item) => item.product.id !== productId,
        );
        return;
      }

      const item = state.items.find((item) => item.product.id === productId);
      if (item) {
        // If allowNegativeStock is enabled, skip stock check
        if (state.allowNegativeStock) {
          item.quantity = quantity;
          return;
        }
        // Check stock availability
        const availableStock = item.product.trackInventory
          ? getProductCurrentStock(item.product)
          : Infinity;
        if (item.product.trackInventory && quantity > availableStock) {
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
        allowNegativeStock?: boolean;
      }>,
    ) => {
      state.taxRate = action.payload.taxRate;
      state.isTaxEnabled = action.payload.isTaxEnabled;
      if (action.payload.allowNegativeStock !== undefined) {
        state.allowNegativeStock = action.payload.allowNegativeStock;
      }
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
  },
});

export const {
  addItem,
  removeItem,
  updateQuantity,
  updateNotes,
  clearCart,
  setTaxSettings,
  setDiscount,
  setItemDiscount,
} = cartSlice.actions;

// Selectors
export const selectCartItems = (state: { cart: CartState }) => state.cart.items;
export const selectTaxRate = (state: { cart: CartState }) => state.cart.taxRate;
export const selectIsTaxEnabled = (state: { cart: CartState }) =>
  state.cart.isTaxEnabled;

export const selectAllowNegativeStock = (state: { cart: CartState }) =>
  state.cart.allowNegativeStock;

export const selectDiscountType = (state: { cart: CartState }) =>
  state.cart.discountType;

export const selectDiscountValue = (state: { cart: CartState }) =>
  state.cart.discountValue;

export const selectItemsCount = (state: { cart: CartState }) =>
  state.cart.items.reduce((sum, item) => sum + item.quantity, 0);

/**
 * Helper: calculate item-level discount for a single cart item
 */
const calcItemDiscount = (item: CartItem): number => {
  if (!item.discount) return 0;
  const lineTotal = item.product.price * item.quantity;
  if (item.discount.type === "percentage") {
    return Math.min(lineTotal * (item.discount.value / 100), lineTotal);
  }
  return Math.min(item.discount.value, lineTotal);
};

/**
 * Total item-level discounts across all cart items
 */
export const selectItemDiscountsTotal = (state: { cart: CartState }) =>
  Math.round(
    state.cart.items.reduce((sum, item) => sum + calcItemDiscount(item), 0) *
      100,
  ) / 100;

/**
 * Subtotal = Sum of all item prices (Net, before tax and discount)
 * Product.price is the NET price (excluding tax)
 */
export const selectSubtotal = (state: { cart: CartState }) =>
  Math.round(
    state.cart.items.reduce(
      (sum, item) => sum + item.product.price * item.quantity,
      0,
    ) * 100,
  ) / 100;

/**
 * Calculate order-level discount amount based on type and value
 */
export const selectDiscountAmount = (state: { cart: CartState }) => {
  if (!state.cart.discountType || !state.cart.discountValue) return 0;

  const subtotal = state.cart.items.reduce(
    (sum, item) => sum + item.product.price * item.quantity,
    0,
  );

  // Subtract item-level discounts first
  const itemDiscounts = state.cart.items.reduce(
    (sum, item) => sum + calcItemDiscount(item),
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
    (sum, item) => sum + item.product.price * item.quantity,
    0,
  );

  // Item-level discounts
  const itemDiscounts = state.cart.items.reduce(
    (sum, item) => sum + calcItemDiscount(item),
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

  const taxableAmount = afterItemDiscounts - orderDiscount;
  const taxAmount = taxableAmount * (state.cart.taxRate / 100);
  return Math.round(taxAmount * 100) / 100;
};

/**
 * Total = Subtotal - ItemDiscounts - OrderDiscount + Tax
 */
export const selectTotal = (state: { cart: CartState }) => {
  const subtotal = state.cart.items.reduce(
    (sum, item) => sum + item.product.price * item.quantity,
    0,
  );

  // Item-level discounts
  const itemDiscounts = state.cart.items.reduce(
    (sum, item) => sum + calcItemDiscount(item),
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

  if (!state.cart.isTaxEnabled) {
    return Math.round(afterAllDiscounts * 100) / 100;
  }

  const taxAmount = afterAllDiscounts * (state.cart.taxRate / 100);
  return Math.round((afterAllDiscounts + taxAmount) * 100) / 100;
};

export default cartSlice.reducer;

