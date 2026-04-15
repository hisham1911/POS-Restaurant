import { useEffect, useRef, useState } from "react";
import { useCart } from "./useCart";
import { Order } from "@/types/order.types";

const DRAFT_CANCEL_REASON = "POS payment draft discarded";

interface UsePreparedPaymentOrderOptions {
  enabled: boolean;
  customerId?: number;
  createOrder: (customerId?: number) => Promise<Order | null>;
  cancelOrder: (
    orderId: number,
    reason?: string,
    options?: { silent?: boolean },
  ) => Promise<boolean>;
  onPrepareFailed?: () => void;
}

export const usePreparedPaymentOrder = ({
  enabled,
  customerId,
  createOrder,
  cancelOrder,
  onPrepareFailed,
}: UsePreparedPaymentOrderOptions) => {
  const { items, discountType, discountValue } = useCart();
  const [preparedOrder, setPreparedOrder] = useState<Order | null>(null);
  const [isPreparingOrder, setIsPreparingOrder] = useState(false);

  const preparedOrderRef = useRef<Order | null>(null);
  const completedOrderIdRef = useRef<number | null>(null);
  const createOrderRef = useRef(createOrder);
  const cancelOrderRef = useRef(cancelOrder);
  const onPrepareFailedRef = useRef(onPrepareFailed);

  createOrderRef.current = createOrder;
  cancelOrderRef.current = cancelOrder;
  onPrepareFailedRef.current = onPrepareFailed;

  const cartSignature = JSON.stringify({
    customerId: customerId ?? null,
    discountType: discountType ?? null,
    discountValue: discountValue ?? null,
    items: items.map((item) => ({
      id: item.product.id,
      quantity: item.quantity,
      notes: item.notes ?? "",
      discountType: item.discount?.type ?? null,
      discountValue: item.discount?.value ?? null,
      discountReason: item.discount?.reason ?? "",
    })),
  });

  const cancelPreparedOrderSilently = async (order: Order | null) => {
    if (!order || completedOrderIdRef.current === order.id) {
      return;
    }

    await cancelOrderRef.current(order.id, DRAFT_CANCEL_REASON, {
      silent: true,
    });
  };

  const markPreparedOrderCompleted = (orderId: number) => {
    completedOrderIdRef.current = orderId;

    if (preparedOrderRef.current?.id === orderId) {
      preparedOrderRef.current = null;
      setPreparedOrder(null);
    }
  };

  const discardPreparedOrder = async () => {
    const orderToCancel = preparedOrderRef.current;
    preparedOrderRef.current = null;
    setPreparedOrder(null);
    await cancelPreparedOrderSilently(orderToCancel);
  };

  useEffect(() => {
    let isDisposed = false;

    const prepareOrder = async () => {
      if (!enabled || items.length === 0) {
        setIsPreparingOrder(false);
        await discardPreparedOrder();
        return;
      }

      completedOrderIdRef.current = null;
      await discardPreparedOrder();
      setIsPreparingOrder(true);

      const order = await createOrderRef.current(customerId);

      if (isDisposed) {
        await cancelPreparedOrderSilently(order);
        return;
      }

      if (!order) {
        setIsPreparingOrder(false);
        onPrepareFailedRef.current?.();
        return;
      }

      preparedOrderRef.current = order;
      setPreparedOrder(order);
      setIsPreparingOrder(false);
    };

    void prepareOrder();

    return () => {
      isDisposed = true;
      const orderToCancel = preparedOrderRef.current;
      preparedOrderRef.current = null;
      void cancelPreparedOrderSilently(orderToCancel);
    };
  }, [enabled, items.length, cartSignature, customerId]);

  return {
    preparedOrder,
    isPreparingOrder,
    markPreparedOrderCompleted,
    discardPreparedOrder,
  };
};
