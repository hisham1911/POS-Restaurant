import {
  useGetOrdersQuery,
  useGetTodayOrdersQuery,
  useCreateOrderMutation,
  useCompleteOrderMutation,
  useCancelOrderMutation,
  useAddOrderItemMutation,
  useAddCustomItemMutation,
  useSendToKitchenMutation,
} from "../api/ordersApi";
import { useCart } from "./useCart";
import {
  AddCustomItemRequest,
  CompleteOrderRequest,
  KitchenTicket,
  Order,
  OrderSource,
  OrderType,
} from "../types/order.types";
import { toast } from "sonner";
import { useAppSelector } from "../store/hooks";
import { selectCurrentBranch } from "../store/slices/branchSlice";
import { extractApiData } from "@/utils/apiResponse";
import { useGetCurrentTenantQuery } from "@/api/branchesApi";
import {
  printKitchenTicketFallback,
  printOrderReceiptFallback,
} from "@/utils/browserReceiptPrinter";
import { useDevicePrintPreferences } from "@/hooks/useDevicePrintPreferences";

export const useOrders = () => {
  const { items, discountType, discountValue } = useCart();
  const currentBranch = useAppSelector(selectCurrentBranch);

  const {
    data: ordersData,
    isLoading: isLoadingOrders,
    refetch,
  } = useGetOrdersQuery();
  const { data: tenantData } = useGetCurrentTenantQuery();
  const { printMode } = useDevicePrintPreferences();
  const { data: todayOrdersData, isLoading: isLoadingToday } =
    useGetTodayOrdersQuery();

  const [createMutation, { isLoading: isCreating }] = useCreateOrderMutation();
  const [completeMutation, { isLoading: isCompleting }] =
    useCompleteOrderMutation();
  const [cancelMutation, { isLoading: isCancelling }] =
    useCancelOrderMutation();
  const [addOrderItemMutation] = useAddOrderItemMutation();
  const [addCustomItemMutation] = useAddCustomItemMutation();
  const [sendToKitchenMutation, { isLoading: isSendingToKitchen }] =
    useSendToKitchenMutation();

  const orders = ordersData?.data?.items || [];
  const todayOrders = todayOrdersData?.data || [];

  const createOrder = async (
    customerId?: number,
    orderType?: OrderType,
    deliveryAddress?: string,
    deliveryFee?: number,
    deliveryNotes?: string,
    options?: {
      tableId?: number;
      orderSource?: OrderSource;
      externalOrderNumber?: string;
      notes?: string;
    },
  ): Promise<Order | null> => {
    if (items.length === 0) {
      toast.error("Ø§Ù„Ø³Ù„Ø© ÙØ§Ø±ØºØ©");
      return null;
    }

    if (!currentBranch?.id) {
      toast.error("Ø§Ø®ØªØ± ÙØ±Ø¹Ù‹Ø§ Ø£ÙˆÙ„Ù‹Ø§");
      return null;
    }

    const realCartItems = items.filter((item) => item.product.id > 0);

    if (realCartItems.length === 0) {
      toast.error("Ø£Ø¶Ù Ù…Ù†ØªØ¬Ù‹Ø§ Ø£Ø³Ø§Ø³ÙŠÙ‹Ø§ Ù‚Ø¨Ù„ Ø§Ù„Ø¥Ø¶Ø§ÙØ§Øª Ø§Ù„Ù…Ø¯ÙÙˆØ¹Ø©");
      return null;
    }

    let hasParentProduct = false;
    for (const item of items) {
      if (item.product.id > 0) {
        hasParentProduct = true;
        continue;
      }

      if (!hasParentProduct) {
        toast.error("Ø£Ø¶Ù Ø§Ù„Ø¥Ø¶Ø§ÙØ© Ø§Ù„Ù…Ø¯ÙÙˆØ¹Ø© Ø¨Ø¹Ø¯ Ø§Ù„Ù…Ù†ØªØ¬ Ø§Ù„Ø£Ø³Ø§Ø³ÙŠ ÙÙŠ Ø§Ù„Ø³Ù„Ø©");
        return null;
      }
    }

    const orderItems = realCartItems.map((item) => ({
      productId: item.product.id,
      quantity: item.quantity,
      batchId: item.batchId,
      notes: item.notes,
      ...(item.discount
        ? {
            discountType: item.discount.type,
            discountValue: item.discount.value,
            discountReason: item.discount.reason,
          }
        : {}),
    }));

    try {
      const response = await createMutation({
        branchId: currentBranch.id,
        items: orderItems,
        customerId,
        discountType,
        discountValue,
        orderType,
        tableId: options?.tableId,
        orderSource: options?.orderSource,
        externalOrderNumber: options?.externalOrderNumber,
        deliveryAddress,
        deliveryFee,
        deliveryNotes,
        notes: options?.notes,
      }).unwrap();

      let order = extractApiData(
        response,
        "ORDER_CREATE_EMPTY_RESPONSE",
        "ØªØ¹Ø°Ø± Ø¥Ù†Ø´Ø§Ø¡ Ø§Ù„Ø·Ù„Ø¨",
      );

      let lastParentOrderItemId = order.items
        .filter((orderItem) => !orderItem.isCustomItem)
        .at(-1)?.id;

      for (const item of items) {
        if (item.product.id > 0) {
          const matchingOrderItem = order.items
            .filter((orderItem) => orderItem.productId === item.product.id)
            .at(-1);
          lastParentOrderItemId = matchingOrderItem?.id ?? lastParentOrderItemId;
          continue;
        }

        const customItem: AddCustomItemRequest = {
          name: item.product.name,
          unitPrice: item.product.price,
          quantity: item.quantity,
          parentOrderItemId: lastParentOrderItemId,
          taxRate: item.product.taxRate,
          notes: item.notes,
        };
        const customResponse = await addCustomItemMutation({
          orderId: order.id,
          item: customItem,
        }).unwrap();

        order = extractApiData(
          customResponse,
          "ORDER_ADD_CUSTOM_ITEM_EMPTY_RESPONSE",
          "ØªØ¹Ø°Ø± Ø¥Ø¶Ø§ÙØ© Ø§Ù„Ø¥Ø¶Ø§ÙØ© Ù„Ù„Ø·Ù„Ø¨",
        );
      }

      return order;
    } catch {
      return null;
    }
  };

  const addCartItemsToOrder = async (orderId: number): Promise<Order | null> => {
    let latestOrder: Order | null = null;
    let lastParentOrderItemId: number | undefined;

    let hasParentProduct = false;
    for (const item of items) {
      if (item.product.id > 0) {
        hasParentProduct = true;
        continue;
      }

      if (!hasParentProduct) {
        toast.error("Ø£Ø¶Ù Ø§Ù„Ø¥Ø¶Ø§ÙØ© Ø§Ù„Ù…Ø¯ÙÙˆØ¹Ø© Ø¨Ø¹Ø¯ Ø§Ù„Ù…Ù†ØªØ¬ Ø§Ù„Ø£Ø³Ø§Ø³ÙŠ ÙÙŠ Ø§Ù„Ø³Ù„Ø©");
        return null;
      }
    }

    try {
      for (const item of items) {
        if (item.product.id > 0) {
          const response = await addOrderItemMutation({
            orderId,
            item: {
              productId: item.product.id,
              quantity: item.quantity,
              batchId: item.batchId,
              notes: item.notes,
              ...(item.discount
                ? {
                    discountType: item.discount.type,
                    discountValue: item.discount.value,
                    discountReason: item.discount.reason,
                  }
                : {}),
            },
          }).unwrap();

          latestOrder = extractApiData(
            response,
            "ORDER_ADD_ITEM_EMPTY_RESPONSE",
            "ØªØ¹Ø°Ø± Ø¥Ø¶Ø§ÙØ© Ø§Ù„Ù…Ù†ØªØ¬ Ù„Ù„Ø·Ù„Ø¨",
          );
          lastParentOrderItemId = latestOrder.items
            .filter((orderItem) => !orderItem.isCustomItem)
            .at(-1)?.id;
        } else {
          const customItem: AddCustomItemRequest = {
            name: item.product.name,
            unitPrice: item.product.price,
            quantity: item.quantity,
            parentOrderItemId: lastParentOrderItemId,
            taxRate: item.product.taxRate,
            notes: item.notes,
          };
          const response = await addCustomItemMutation({
            orderId,
            item: customItem,
          }).unwrap();

          latestOrder = extractApiData(
            response,
            "ORDER_ADD_CUSTOM_ITEM_EMPTY_RESPONSE",
            "ØªØ¹Ø°Ø± Ø¥Ø¶Ø§ÙØ© Ø§Ù„Ø¥Ø¶Ø§ÙØ© Ù„Ù„Ø·Ù„Ø¨",
          );
        }
      }

      return latestOrder;
    } catch {
      return null;
    }
  };

  const sendToKitchen = async (orderId: number): Promise<KitchenTicket | null> => {
    try {
      const response = await sendToKitchenMutation(orderId).unwrap();
      const ticket = extractApiData(
        response,
        "KITCHEN_TICKET_EMPTY_RESPONSE",
        "ØªØ¹Ø°Ø± Ø¥Ø±Ø³Ø§Ù„ Ø§Ù„Ø·Ù„Ø¨ Ù„Ù„Ù…Ø·Ø¨Ø®",
      );
      toast.success("ØªÙ… Ø¥Ø±Ø³Ø§Ù„ Ø§Ù„Ø·Ù„Ø¨ Ù„Ù„Ù…Ø·Ø¨Ø®");
      return ticket;
    } catch {
      return null;
    }
  };

  const completeOrder = async (
    orderId: number,
    data: CompleteOrderRequest,
    options?: { printKitchenTicket?: boolean },
  ): Promise<Order | null> => {
    try {
      const printKitchenTicket = options?.printKitchenTicket ?? true;
      const response = await completeMutation({
        orderId,
        data,
        printKitchenTicket,
      }).unwrap();
      const order = extractApiData(
        response,
        "ORDER_COMPLETE_EMPTY_RESPONSE",
        "ØªØ¹Ø°Ø± Ø¥ÙƒÙ…Ø§Ù„ Ø§Ù„Ø·Ù„Ø¨",
      );

      setTimeout(() => {
        const printBrowserCopies = () => {
          const kitchenOpened = printKitchenTicket
            ? printKitchenTicketFallback(order, tenantData?.data)
            : true;
          const receiptOpened = printOrderReceiptFallback(order, tenantData?.data);

          if (kitchenOpened && receiptOpened) {
            toast.info(
              printKitchenTicket
                ? "تم فتح فاتورة المطبخ وفاتورة العميل من المتصفح"
                : "تم فتح طباعة المتصفح حسب إعدادات هذا الجهاز",
            );
            return;
          }

          toast.error("تعذر فتح نافذة الطباعة. تأكد من السماح بالنوافذ المنبثقة");
        };

        if (printMode === "browser") {
          printBrowserCopies();
        } else {
          const shouldFallbackToBrowserPrint =
            printMode === "auto" &&
            ((response.printAttempted === true && response.printDelivered === false) ||
              (printKitchenTicket &&
                response.kitchenPrintAttempted === true &&
                response.kitchenPrintDelivered === false));

          if (shouldFallbackToBrowserPrint) {
            printBrowserCopies();
          }

          if (
            printMode === "bridge" &&
            ((response.printAttempted === true && response.printDelivered === false) ||
              (printKitchenTicket &&
                response.kitchenPrintAttempted === true &&
                response.kitchenPrintDelivered === false))
          ) {
            toast.error(
              "تعذر الوصول لتطبيق الطابعة. راجع حالة اتصال Bridge في إعدادات الجهاز",
            );
          }
        }

        toast.success("تم إكمال الطلب بنجاح");
      }, 0);

      return order;
    } catch (error) {
      console.error("âŒ Complete Order Error:", error);
      console.error("âŒ Error details:", JSON.stringify(error, null, 2));
      return null;
    }
  };

  const cancelOrder = async (
    orderId: number,
    reason?: string,
    options?: { silent?: boolean },
  ): Promise<boolean> => {
    try {
      const response = await cancelMutation({ orderId, reason }).unwrap();
      extractApiData(
        response,
        "ORDER_CANCEL_EMPTY_RESPONSE",
        "ØªØ¹Ø°Ø± Ø¥Ù„ØºØ§Ø¡ Ø§Ù„Ø·Ù„Ø¨",
      );

      if (!options?.silent) {
        toast.success("ØªÙ… Ø¥Ù„ØºØ§Ø¡ Ø§Ù„Ø·Ù„Ø¨");
        refetch();
      }

      return true;
    } catch {
      return false;
    }
  };

  return {
    orders,
    todayOrders,
    isLoadingOrders,
    isLoadingToday,
    refetch,
    createOrder,
    completeOrder,
    cancelOrder,
    addCartItemsToOrder,
    sendToKitchen,
    isCreating,
    isCompleting,
    isCancelling,
    isSendingToKitchen,
  };
};
