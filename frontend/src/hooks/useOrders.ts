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

// Error response type from API
interface ApiErrorData {
  message?: string;
  errorCode?: string;
}

export const useOrders = () => {
  const { items, clearCart, discountType, discountValue } = useCart();
  const currentBranch = useAppSelector(selectCurrentBranch);

  // Note: useGetOrdersQuery now returns paginated data, but we keep it for backward compatibility
  // For the full list with filters, use useGetOrdersQuery directly in components
  const {
    data: ordersData,
    isLoading: isLoadingOrders,
    refetch,
  } = useGetOrdersQuery();
  const { data: todayOrdersData, isLoading: isLoadingToday } =
    useGetTodayOrdersQuery();

  const [createMutation, { isLoading: isCreating }] = useCreateOrderMutation();
  const [completeMutation, { isLoading: isCompleting }] =
    useCompleteOrderMutation();
  const [cancelMutation, { isLoading: isCancelling }] =
    useCancelOrderMutation();

  // Extract items from paginated response
  const orders = ordersData?.data?.items || [];
  const todayOrders = todayOrdersData?.data || [];

  const createOrder = async (customerId?: number): Promise<Order | null> => {
    if (items.length === 0) {
      toast.error("السلة فارغة");
      return null;
    }

    if (!currentBranch?.id) {
      toast.error("يرجى اختيار الفرع أولاً");
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
      const result = await createMutation({
        branchId: currentBranch.id,
        items: orderItems,
        customerId,
        discountType,
        discountValue,
      }).unwrap();
      if (result.success && result.data) {
        return result.data;
      }
      // Backend returned success: false
      toast.error(result.message || "فشل في إنشاء الطلب");
      return null;
    } catch (error) {
      // Error already handled by baseQueryWithReauth
      // Only show generic error if not already shown
      const apiError = error as { data?: ApiErrorData };
      if (!apiError.data?.errorCode) {
        toast.error("فشل في إنشاء الطلب");
      }
      return null;
    }
  };

  const completeOrder = async (
    orderId: number,
    data: CompleteOrderRequest,
  ): Promise<Order | null> => {
    try {
      const result = await completeMutation({ orderId, data }).unwrap();
      if (result.success && result.data) {
        // ✅ مسح السلة فقط عند نجاح الدفع
        clearCart();
        toast.success("تم إتمام الدفع وإغلاق الطلب");
        return result.data;
      }
      // Backend returned success: false
      toast.error(result.message || "فشل في إكمال الطلب");
      return null;
    } catch (error) {
      // ❌ لا نمسح السلة عند الفشل - البيانات محفوظة
      // Error is handled by baseQueryWithReauth, but we check for specific cases
      const apiError = error as { data?: ApiErrorData; status?: number };

      // Don't show generic error if baseQueryWithReauth already showed specific error
      // Only show generic error for unexpected cases
      if (
        !apiError.data?.errorCode &&
        apiError.status !== 400 &&
        apiError.status !== 409
      ) {
        toast.error("فشل في إكمال الطلب");
      }
      return null;
    }
  };

  const cancelOrder = async (
    orderId: number,
    reason?: string,
  ): Promise<boolean> => {
    try {
      const result = await cancelMutation({ orderId, reason }).unwrap();
      if (result.success) {
        toast.success("تم إلغاء الطلب");
        refetch();
        return true;
      }
      toast.error(result.message || "فشل في إلغاء الطلب");
      return false;
    } catch (error) {
      const apiError = error as { data?: ApiErrorData };
      if (!apiError.data?.errorCode) {
        toast.error("فشل في إلغاء الطلب");
      }
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
