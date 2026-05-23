import { useAppDispatch, useAppSelector } from "../store/hooks";
import { toast } from "sonner";
import { usePermission } from "./usePermission";
import {
  addItem,
  removeItem,
  updateQuantity,
  updateNotes,
  clearCart,
  replaceCart,
  updateItemBatch,
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

  const add = (
    product: Product, 
    quantity = 1,
    batchInfo?: {
      batchId: number;
      batchNumber?: string;
      expiryDate: string;
      sellingPrice?: number;
      batchQuantity?: number;
    }
  ) => {
    dispatch(addItem({ 
      product, 
      quantity,
      batchId: batchInfo?.batchId,
      batchNumber: batchInfo?.batchNumber,
      expiryDate: batchInfo?.expiryDate,
      batchSellingPrice: batchInfo?.sellingPrice,
      batchQuantity: batchInfo?.batchQuantity,
    }));
  };

  const changeBatch = (
    productId: number,
    currentBatchId: number | undefined,
    batchId: number,
    batchNumber: string | undefined,
    expiryDate: string,
    sellingPrice?: number,
    batchQuantity?: number,
  ) => {
    dispatch(updateItemBatch({ productId, currentBatchId, batchId, batchNumber, expiryDate, sellingPrice, batchQuantity }));
  };

  const remove = (productId: number, batchId?: number) => {
    dispatch(removeItem({ productId, batchId }));
  };

  const setQuantity = (productId: number, quantity: number, batchId?: number) => {
    dispatch(updateQuantity({ productId, quantity, batchId }));
  };

  const setNotes = (productId: number, notes: string) => {
    dispatch(updateNotes({ productId, notes }));
  };

  const clear = () => {
    dispatch(clearCart());
  };

  const replace = (
    nextCart: {
      items: ReturnType<typeof selectCartItems>;
      discountType?: DiscountType;
      discountValue?: number;
    },
  ) => {
    dispatch(replaceCart(nextCart));
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
    updateItemBatch: changeBatch,
    clearCart: clear,
    replaceCart: replace,
    applyDiscount,
    removeDiscount,
    applyItemDiscount,
    removeItemDiscount,
  };
};
