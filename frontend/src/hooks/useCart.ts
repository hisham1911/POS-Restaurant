import { useAppDispatch, useAppSelector } from "../store/hooks";
import { toast } from "sonner";
import { usePermission } from "./usePermission";
import {
  addItem,
  removeItem,
  updateQuantity,
  updateNotes,
  clearCart,
  selectCartItems,
  selectItemsCount,
  selectSubtotal,
  selectTaxAmount,
  selectTotal,
  selectTaxRate,
  selectIsTaxEnabled,
  selectServiceChargeAmount,
  selectServiceChargeRate,
  selectDiscountAmount,
  selectDiscountType,
  selectDiscountValue,
  selectItemDiscountsTotal,
  setDiscount,
  setItemDiscount,
  DiscountType,
  ItemDiscount,
} from "../store/slices/cartSlice";
import { Product } from "../types/product.types";

export const useCart = () => {
  const dispatch = useAppDispatch();
  const { hasPermission } = usePermission();

  const items = useAppSelector(selectCartItems);
  const itemsCount = useAppSelector(selectItemsCount);
  const subtotal = useAppSelector(selectSubtotal);
  const discountAmount = useAppSelector(selectDiscountAmount);
  const discountType = useAppSelector(selectDiscountType);
  const discountValue = useAppSelector(selectDiscountValue);
  const itemDiscountsTotal = useAppSelector(selectItemDiscountsTotal);
  const taxAmount = useAppSelector(selectTaxAmount);
  const total = useAppSelector(selectTotal);
  const taxRate = useAppSelector(selectTaxRate);
  const isTaxEnabled = useAppSelector(selectIsTaxEnabled);
  const serviceChargeRate = useAppSelector(selectServiceChargeRate);
  const serviceChargeAmount = useAppSelector(selectServiceChargeAmount);

  const canManageDiscounts = hasPermission("PosApplyDiscount");

  const notifyDiscountPermissionDenied = () => {
    toast.error("ليس لديك صلاحية تطبيق أو تعديل الخصومات");
  };

  const add = (product: Product, quantity = 1) => {
    dispatch(addItem({ product, quantity }));
  };

  const remove = (productId: number) => {
    dispatch(removeItem(productId));
  };

  const setQuantity = (productId: number, quantity: number) => {
    dispatch(updateQuantity({ productId, quantity }));
  };

  const setNotes = (productId: number, notes: string) => {
    dispatch(updateNotes({ productId, notes }));
  };

  const clear = () => {
    dispatch(clearCart());
  };

  const applyDiscount = (type: DiscountType, value: number) => {
    if (!canManageDiscounts) {
      notifyDiscountPermissionDenied();
      return;
    }

    dispatch(setDiscount({ type, value }));
  };

  const removeDiscount = () => {
    if (!canManageDiscounts) {
      notifyDiscountPermissionDenied();
      return;
    }

    dispatch(setDiscount(undefined));
  };

  const applyItemDiscount = (productId: number, discount: ItemDiscount) => {
    if (!canManageDiscounts) {
      notifyDiscountPermissionDenied();
      return;
    }

    dispatch(setItemDiscount({ productId, discount }));
  };

  const removeItemDiscount = (productId: number) => {
    if (!canManageDiscounts) {
      notifyDiscountPermissionDenied();
      return;
    }

    dispatch(setItemDiscount({ productId, discount: undefined }));
  };

  return {
    items,
    itemsCount,
    subtotal,
    discountAmount,
    discountType,
    discountValue,
    itemDiscountsTotal,
    taxAmount,
    total,
    taxRate,
    isTaxEnabled,
    serviceChargeRate,
    serviceChargeAmount,
    canManageDiscounts,
    addItem: add,
    removeItem: remove,
    updateQuantity: setQuantity,
    updateNotes: setNotes,
    clearCart: clear,
    applyDiscount,
    removeDiscount,
    applyItemDiscount,
    removeItemDiscount,
  };
};
