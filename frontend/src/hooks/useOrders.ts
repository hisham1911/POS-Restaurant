import {
  useGetOrdersQuery,
  useGetTodayOrdersQuery,
  useCreateOrderMutation,
  useCompleteOrderMutation,
  useCancelOrderMutation,
} from "../api/ordersApi";
import { useCart } from "./useCart";
import { CompleteOrderRequest, Order } from "../types/order.types";
import { toast } from "sonner";
import { useAppSelector } from "../store/hooks";
import { selectCurrentBranch } from "../store/slices/branchSlice";
import { extractApiData } from "@/utils/apiResponse";
import { useGetCurrentTenantQuery } from "@/api/branchesApi";
import { printOrderReceiptFallback } from "@/utils/browserReceiptPrinter";
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

  const orders = ordersData?.data?.items || [];
  const todayOrders = todayOrdersData?.data || [];

  const createOrder = async (customerId?: number): Promise<Order | null> => {
    if (items.length === 0) {
      toast.error("السلة فارغة");
      return null;
    }

    if (!currentBranch?.id) {
      toast.error("اختر فرعًا أولًا");
      return null;
    }

    const orderItems = items.map((item) => ({
      productId: item.product.id,
      quantity: item.quantity,
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
      }).unwrap();

      return extractApiData(
        response,
        "ORDER_CREATE_EMPTY_RESPONSE",
        "تعذر إنشاء الطلب",
      );
    } catch {
      return null;
    }
  };

  const completeOrder = async (
    orderId: number,
    data: CompleteOrderRequest,
  ): Promise<Order | null> => {
    try {
      const response = await completeMutation({ orderId, data }).unwrap();
      const order = extractApiData(
        response,
        "ORDER_COMPLETE_EMPTY_RESPONSE",
        "تعذر إكمال الطلب",
      );

      if (printMode === "browser") {
        const isPrintWindowOpened = printOrderReceiptFallback(
          order,
          tenantData?.data,
        );

        if (isPrintWindowOpened) {
          toast.info("تم فتح طباعة المتصفح حسب إعدادات هذا الجهاز");
        } else {
          toast.error(
            "تعذر فتح نافذة الطباعة. تأكد من السماح بالنوافذ المنبثقة",
          );
        }
      } else {
        const shouldFallbackToBrowserPrint =
          printMode === "auto" &&
          response.printAttempted === true &&
          response.printDelivered === false;

        if (shouldFallbackToBrowserPrint) {
          const isPrintWindowOpened = printOrderReceiptFallback(
            order,
            tenantData?.data,
          );

          if (isPrintWindowOpened) {
            toast.info(
              "تعذر الوصول لتطبيق الطابعة. تم التحويل تلقائيًا لطباعة المتصفح",
            );
          } else {
            toast.error(
              "تعذر فتح نافذة الطباعة. تأكد من السماح بالنوافذ المنبثقة",
            );
          }
        }

        if (
          printMode === "bridge" &&
          response.printAttempted === true &&
          response.printDelivered === false
        ) {
          toast.error(
            "تعذر الوصول لتطبيق الطابعة. راجع حالة اتصال Bridge في إعدادات الجهاز",
          );
        }
      }

      toast.success("تم إكمال الطلب بنجاح");
      return order;
    } catch {
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
        "تعذر إلغاء الطلب",
      );

      if (!options?.silent) {
        toast.success("تم إلغاء الطلب");
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
    isCreating,
    isCompleting,
    isCancelling,
  };
};
