import { useState, useMemo } from "react";
import { X, AlertTriangle, RotateCcw, Minus, Plus } from "lucide-react";
import { Order, OrderItem } from "@/types/order.types";
import { useRefundOrderMutation } from "@/api/ordersApi";
import { formatCurrency } from "@/utils/formatters";
import { Button } from "@/components/common/Button";
import { toast } from "sonner";
import clsx from "clsx";
import { Portal } from "@/components/common/Portal";
import { handleApiError } from "@/utils/errorHandler";

interface RefundModalProps {
  order: Order;
  onClose: () => void;
  onSuccess?: () => void;
}

interface RefundItemState {
  itemId: number;
  maxQuantity: number;
  refundQuantity: number;
  unitPrice: number;
  productName: string;
  discountAmount?: number;
  discountType?: string;
  discountValue?: number;
}

const refundReasons = [
  { id: "damaged", label: "منتج تالف" },
  { id: "wrong_order", label: "خطأ في الطلب" },
  { id: "customer_request", label: "طلب العميل" },
  { id: "quality_issue", label: "مشكلة في الجودة" },
  { id: "other", label: "أسباب أخرى" },
];

export const RefundModal = ({
  order,
  onClose,
  onSuccess,
}: RefundModalProps) => {
  // Refund type: "full" or "partial"
  const [refundType, setRefundType] = useState<"full" | "partial">("full");
  const [selectedReason, setSelectedReason] = useState<string>("");
  const [customReason, setCustomReason] = useState("");

  // Track refund quantities for each item
  const [refundItems, setRefundItems] = useState<RefundItemState[]>(() =>
    order.items
      .filter((item) => item.quantity - (item.refundedQuantity || 0) > 0)
      .map((item) => ({
        itemId: item.id,
        maxQuantity: item.quantity - (item.refundedQuantity || 0),
        refundQuantity: 0,
        unitPrice: item.total / item.quantity,
        productName: item.productName,
        discountAmount: item.discountAmount,
        discountType: item.discountType,
        discountValue: item.discountValue,
      })),
  );

  const [refundOrder, { isLoading }] = useRefundOrderMutation();
  const remainingRefundableAmount = Math.max(
    0,
    order.total - (order.refundAmount || 0),
  );

  // Calculate total refund amount
  const totalRefundAmount = useMemo(() => {
    if (refundType === "full") {
      return remainingRefundableAmount;
    }
    return refundItems.reduce(
      (sum, item) => sum + item.refundQuantity * item.unitPrice,
      0,
    );
  }, [refundType, refundItems, remainingRefundableAmount]);

  // Check if any items selected for partial refund
  const hasSelectedItems = refundItems.some((item) => item.refundQuantity > 0);

  const finalReason =
    selectedReason === "other"
      ? customReason
      : refundReasons.find((r) => r.id === selectedReason)?.label || "";

  const canSubmit =
    (refundType === "full" &&
      selectedReason &&
      (selectedReason !== "other" || customReason.trim())) ||
    (refundType === "partial" && hasSelectedItems);

  const updateRefundQuantity = (itemId: number, delta: number) => {
    setRefundItems((prev) =>
      prev.map((item) => {
        if (item.itemId !== itemId) return item;
        const newQty = Math.max(
          0,
          Math.min(item.maxQuantity, item.refundQuantity + delta),
        );
        return { ...item, refundQuantity: newQty };
      }),
    );
  };

  const setRefundQuantity = (itemId: number, quantity: number) => {
    setRefundItems((prev) =>
      prev.map((item) => {
        if (item.itemId !== itemId) return item;
        const newQty = Math.max(0, Math.min(item.maxQuantity, quantity));
        return { ...item, refundQuantity: newQty };
      }),
    );
  };

  const handleSubmit = async () => {
    if (!canSubmit) {
      toast.error(
        refundType === "full"
          ? "يرجى اختيار سبب الاسترجاع"
          : "يرجى تحديد المنتجات للاسترجاع",
      );
      return;
    }

    try {
      let result;

      if (refundType === "full") {
        // Full refund - send reason only
        result = await refundOrder({
          orderId: order.id,
          reason: finalReason,
        }).unwrap();
      } else {
        // Partial refund - send items list
        const itemsToRefund = refundItems
          .filter((item) => item.refundQuantity > 0)
          .map((item) => ({
            itemId: item.itemId,
            quantity: item.refundQuantity,
            reason: finalReason || undefined,
          }));

        result = await refundOrder({
          orderId: order.id,
          reason: finalReason || undefined,
          items: itemsToRefund,
        }).unwrap();
      }

      const returnOrderNumber = result.data?.orderNumber;
      const message =
        refundType === "full"
          ? `تم إنشاء فاتورة المرتجع ${
              returnOrderNumber ? `#${returnOrderNumber}` : ""
            }`
          : `تم إنشاء فاتورة المرتجع الجزئي ${
              returnOrderNumber ? `#${returnOrderNumber}` : ""
            }`;
      toast.success(message);
      onSuccess?.();
      onClose();
    } catch (err: unknown) {
      toast.error(handleApiError(err));
    }
  };

  return (
    <Portal>
      <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-[100] p-4">
        <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-hidden animate-scale-in flex flex-col">
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b bg-orange-50 shrink-0">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-orange-100 flex items-center justify-center">
                <RotateCcw className="w-5 h-5 text-orange-600" />
              </div>
              <div>
                <h2 className="text-lg font-bold text-orange-700">
                  إنشاء فاتورة مرتجع
                </h2>
                <p className="text-sm text-orange-600">
                  للطلب #{order.orderNumber}
                </p>
              </div>
            </div>
            <button
              onClick={onClose}
              className="w-10 h-10 rounded-lg bg-white flex items-center justify-center hover:bg-orange-100 text-orange-500 transition-colors"
            >
              <X className="w-5 h-5" />
            </button>
          </div>

          <div className="flex-1 overflow-y-auto p-6 space-y-6">
            {/* Info Banner */}
            <div className="flex items-start gap-3 p-4 bg-blue-50 border border-blue-200 rounded-xl">
              <AlertTriangle className="w-6 h-6 text-blue-600 shrink-0 mt-0.5" />
              <div className="text-sm text-blue-800">
                <p className="font-semibold mb-1">
                  سيتم إنشاء فاتورة مرتجع جديدة
                </p>
                <ul className="list-disc list-inside space-y-1 text-blue-700">
                  <li>سيتم إنشاء طلب جديد من نوع "مرتجع" بقيمة سالبة</li>
                  <li>سيتم استرجاع المنتجات للمخزون</li>
                  <li>سيتم تسجيل العملية في سجل المراجعة</li>
                </ul>
              </div>
            </div>

            {/* Refund Type Toggle */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-3">
                نوع الاسترجاع
              </label>
              <div className="grid grid-cols-2 gap-3">
                <button
                  type="button"
                  onClick={() => setRefundType("full")}
                  className={clsx(
                    "p-4 rounded-xl border-2 transition-all text-center",
                    refundType === "full"
                      ? "border-danger-500 bg-danger-50 text-danger-700"
                      : "border-gray-200 hover:border-gray-300",
                  )}
                >
                  <p className="font-semibold">استرجاع كامل</p>
                  <p className="text-sm text-gray-500 mt-1">كل المنتجات</p>
                </button>
                <button
                  type="button"
                  onClick={() => setRefundType("partial")}
                  className={clsx(
                    "p-4 rounded-xl border-2 transition-all text-center",
                    refundType === "partial"
                      ? "border-danger-500 bg-danger-50 text-danger-700"
                      : "border-gray-200 hover:border-gray-300",
                  )}
                >
                  <p className="font-semibold">استرجاع جزئي</p>
                  <p className="text-sm text-gray-500 mt-1">منتجات محددة</p>
                </button>
              </div>
            </div>

            {/* Items Table - For Partial Refund */}
            {refundType === "partial" && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-3">
                  المنتجات للاسترجاع <span className="text-danger-500">*</span>
                </label>
                <div className="border border-gray-200 rounded-xl overflow-hidden">
                  <table className="w-full">
                    <thead className="bg-gray-50">
                      <tr>
                        <th className="text-right px-4 py-3 text-sm font-semibold text-gray-600">
                          المنتج
                        </th>
                        <th className="text-center px-4 py-3 text-sm font-semibold text-gray-600">
                          السعر
                        </th>
                        <th className="text-center px-4 py-3 text-sm font-semibold text-gray-600">
                          الأصلي
                        </th>
                        <th className="text-center px-4 py-3 text-sm font-semibold text-gray-600">
                          الكمية المستردة
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {refundItems.map((item) => (
                        <tr key={item.itemId} className="hover:bg-gray-50">
                          <td className="px-4 py-3">
                            <p className="font-medium text-gray-800">
                              {item.productName}
                            </p>
                            {item.discountAmount && item.discountAmount > 0 ? (
                              <p className="text-xs text-success-600 mt-0.5">
                                خصم{" "}
                                {item.discountType === "Percentage" ||
                                item.discountType === "percentage"
                                  ? `${item.discountValue}%`
                                  : formatCurrency(item.discountValue ?? 0)}
                              </p>
                            ) : null}
                          </td>
                          <td className="px-4 py-3 text-center text-gray-600">
                            {formatCurrency(item.unitPrice)}
                          </td>
                          <td className="px-4 py-3 text-center text-gray-500">
                            {item.maxQuantity}
                          </td>
                          <td className="px-4 py-3">
                            <div className="flex items-center justify-center gap-2">
                              <button
                                type="button"
                                onClick={() =>
                                  updateRefundQuantity(item.itemId, -1)
                                }
                                disabled={item.refundQuantity === 0}
                                className="w-8 h-8 rounded-lg bg-gray-100 flex items-center justify-center hover:bg-gray-200 disabled:opacity-50 disabled:cursor-not-allowed"
                              >
                                <Minus className="w-4 h-4" />
                              </button>
                              <input
                                type="number"
                                value={item.refundQuantity || ""}
                                onChange={(e) =>
                                  setRefundQuantity(
                                    item.itemId,
                                    parseInt(e.target.value) || 0,
                                  )
                                }
                                min={0}
                                max={item.maxQuantity}
                                placeholder="0"
                                className="w-16 text-center py-1 border border-gray-200 rounded-lg focus:ring-2 focus:ring-danger-500 focus:border-danger-500"
                              />
                              <button
                                type="button"
                                onClick={() =>
                                  updateRefundQuantity(item.itemId, 1)
                                }
                                disabled={
                                  item.refundQuantity >= item.maxQuantity
                                }
                                className="w-8 h-8 rounded-lg bg-gray-100 flex items-center justify-center hover:bg-gray-200 disabled:opacity-50 disabled:cursor-not-allowed"
                              >
                                <Plus className="w-4 h-4" />
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Refund Reason - Required for Full, Optional for Partial */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-3">
                سبب الاسترجاع{" "}
                {refundType === "full" && (
                  <span className="text-danger-500">*</span>
                )}
              </label>
              <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
                {refundReasons.map((reason) => (
                  <button
                    key={reason.id}
                    type="button"
                    onClick={() =>
                      setSelectedReason(
                        reason.id === selectedReason ? "" : reason.id,
                      )
                    }
                    className={clsx(
                      "p-2 rounded-lg border-2 transition-all text-sm text-center",
                      selectedReason === reason.id
                        ? "border-danger-500 bg-danger-50 text-danger-700"
                        : "border-gray-200 hover:border-gray-300",
                    )}
                  >
                    {reason.label}
                  </button>
                ))}
              </div>

              {selectedReason === "other" && (
                <div className="mt-3">
                  <input
                    type="text"
                    value={customReason}
                    onChange={(e) =>
                      setCustomReason(e.target.value.slice(0, 500))
                    }
                    placeholder="أدخل سبب الاسترجاع..."
                    maxLength={500}
                    className="w-full px-4 py-2 border border-gray-200 rounded-lg focus:ring-2 focus:ring-danger-500 focus:border-danger-500"
                    autoFocus
                  />
                  {customReason.length > 450 && (
                    <p className="text-xs text-gray-500 mt-1 text-left">
                      {customReason.length}/500
                    </p>
                  )}
                </div>
              )}
            </div>

            {/* Total Refund Amount */}
            <div className="bg-danger-50 rounded-xl p-4">
              <div className="flex justify-between items-center">
                <span className="font-semibold text-danger-700">
                  إجمالي المبلغ المسترجع
                </span>
                <span className="text-2xl font-bold text-danger-600">
                  {formatCurrency(totalRefundAmount)}
                </span>
              </div>
              {refundType === "partial" && !hasSelectedItems && (
                <p className="text-sm text-danger-500 mt-2">
                  يرجى تحديد كمية واحدة على الأقل
                </p>
              )}
            </div>
          </div>

          {/* Footer */}
          <div className="flex gap-3 p-6 border-t bg-gray-50 shrink-0">
            <Button
              variant="secondary"
              onClick={onClose}
              className="flex-1"
              disabled={isLoading}
            >
              إلغاء
            </Button>
            <Button
              variant="primary"
              onClick={handleSubmit}
              isLoading={isLoading}
              disabled={isLoading || !canSubmit}
              className="flex-1 !bg-orange-500 hover:!bg-orange-600"
            >
              {refundType === "full"
                ? "إنشاء فاتورة المرتجع"
                : "إنشاء فاتورة المرتجع الجزئي"}
            </Button>
          </div>
        </div>
      </div>
    </Portal>
  );
};
