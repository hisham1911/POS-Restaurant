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
import {
  ApiError,
  getApiErrorCode,
  handleApiError,
} from "../utils/errorHandler";
import { extractApiData } from "@/utils/apiResponse";

export const useOrders = () => {
  const { items, clearCart, discountType, discountValue } = useCart();
  const currentBranch = useAppSelector(selectCurrentBranch);

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

  const orders = ordersData?.data?.items || [];
  const todayOrders = todayOrdersData?.data || [];

  const createOrder = async (customerId?: number): Promise<Order | null> => {
    if (items.length === 0) {
      toast.error("Cart is empty");
      return null;
    }

    if (!currentBranch?.id) {
      toast.error("Select a branch first");
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
        "Unable to create order",
      );
    } catch (error) {
      const apiError = error as ApiError;
      if (
        !getApiErrorCode(error) &&
        apiError.status !== 400 &&
        apiError.status !== 403 &&
        apiError.status !== 409
      ) {
        toast.error(handleApiError(error));
      }
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
        "Unable to complete order",
      );

      clearCart();
      toast.success("Order completed successfully");
      return order;
    } catch (error) {
      const apiError = error as ApiError;
      if (
        !getApiErrorCode(error) &&
        apiError.status !== 400 &&
        apiError.status !== 409
      ) {
        toast.error(handleApiError(error));
      }
      return null;
    }
  };

  const cancelOrder = async (
    orderId: number,
    reason?: string,
  ): Promise<boolean> => {
    try {
      const response = await cancelMutation({ orderId, reason }).unwrap();
      extractApiData(
        response,
        "ORDER_CANCEL_EMPTY_RESPONSE",
        "Unable to cancel order",
      );

      toast.success("Order cancelled");
      refetch();
      return true;
    } catch (error) {
      const apiError = error as ApiError;
      if (
        !getApiErrorCode(error) &&
        apiError.status !== 400 &&
        apiError.status !== 403 &&
        apiError.status !== 409
      ) {
        toast.error(handleApiError(error));
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
