import type { Order, OrderStatus } from "@/types/order.types";

export const ORDER_STATUS_ACTION_LABELS: Partial<Record<OrderStatus, string>> = {
  Pending: "جديد",
  Preparing: "بدء التحضير",
  Prepared: "تم التحضير",
  Delivered: "تم التسليم",
  Cancelled: "إلغاء",
};

const NEXT_OPERATIONAL_STATUS: Partial<Record<OrderStatus, OrderStatus>> = {
  Draft: "Pending",
  Pending: "Preparing",
  Preparing: "Prepared",
  Prepared: "Delivered",
};

const FINAL_STATUSES: OrderStatus[] = [
  "Cancelled",
  "Completed",
  "Refunded",
  "PartiallyRefunded",
];

export const getNextOperationalStatus = (
  status: OrderStatus,
): OrderStatus | null => NEXT_OPERATIONAL_STATUS[status] ?? null;

export const canCancelOrderFromStatusFlow = (order: Order) => {
  if (FINAL_STATUSES.includes(order.status)) {
    return false;
  }

  const hasPayment =
    order.amountPaid > 0 || order.payments.some((payment) => payment.amount > 0);

  return !hasPayment;
};

export const getValidOrderStatusActions = (order: Order): OrderStatus[] => {
  const actions: OrderStatus[] = [];
  const nextStatus = getNextOperationalStatus(order.status);

  if (nextStatus) {
    actions.push(nextStatus);
  }

  if (canCancelOrderFromStatusFlow(order)) {
    actions.push("Cancelled");
  }

  return actions;
};
