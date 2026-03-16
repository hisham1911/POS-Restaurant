import { useState } from "react";
import { X, Printer, RotateCcw, User, Phone, Tag } from "lucide-react";
import { Order } from "@/types/order.types";
import { formatCurrency, formatDateTime } from "@/utils/formatters";
import { ORDER_STATUS, PAYMENT_METHODS } from "@/utils/constants";
import { Button } from "@/components/common/Button";
import { RefundModal } from "./RefundModal";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentUser } from "@/store/slices/authSlice";
import { usePrintReceiptMutation } from "@/api/ordersApi";
import { toast } from "sonner";
import clsx from "clsx";
import { Portal } from "@/components/common/Portal";

interface OrderDetailsModalProps {
  order: Order;
  onClose: () => void;
}

export const OrderDetailsModal = ({
  order,
  onClose,
}: OrderDetailsModalProps) => {
  const [showRefundModal, setShowRefundModal] = useState(false);
  const user = useAppSelector(selectCurrentUser);
  const [printReceipt, { isLoading: isPrinting }] = usePrintReceiptMutation();

  // Only Admin or SystemOwner can refund - can also do additional partial refund on PartiallyRefunded orders
  const canRefund =
    (user?.role === "Admin" || user?.role === "SystemOwner") &&
    (order.status === "Completed" || order.status === "PartiallyRefunded");

  const isFullyRefunded = order.status === "Refunded";
  const isPartiallyRefunded = order.status === "PartiallyRefunded";
  const hasRefund = isFullyRefunded || isPartiallyRefunded;

  const handlePrint = async () => {
    try {
      await printReceipt(order.id).unwrap();
      toast.success("تم إرسال أمر الطباعة بنجاح");
    } catch (error) {
      toast.error("فشل إرسال أمر الطباعة");
      console.error("Print error:", error);
    }
  };

  return (
    <Portal>
      <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-[100] p-4">
        <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-hidden animate-scale-in flex flex-col">
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b shrink-0">
            <div>
              <h2 className="text-xl font-bold">طلب #{order.orderNumber}</h2>
              <p className="text-sm text-gray-500">
                {formatDateTime(order.createdAt)}
              </p>
            </div>
            <div className="flex gap-2">
              {canRefund && (
                <Button
                  variant="danger"
                  size="sm"
                  onClick={() => setShowRefundModal(true)}
                  title="استرجاع الطلب"
                >
                  <RotateCcw className="w-4 h-4" />
                </Button>
              )}
              <Button
                variant="outline"
                size="sm"
                onClick={handlePrint}
                disabled={isPrinting}
                title="طباعة الفاتورة"
              >
                <Printer className="w-4 h-4" />
              </Button>
              <button
                onClick={onClose}
                className="w-10 h-10 rounded-lg bg-gray-100 flex items-center justify-center hover:bg-danger-50 hover:text-danger-500 transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
          </div>

          <div className="flex-1 overflow-y-auto p-6 space-y-6">
            {/* Status */}
            <div className="flex items-center justify-between">
              <span className="text-gray-500">الحالة</span>
              <span
                className={clsx(
                  "px-3 py-1 rounded-full text-sm font-medium",
                  order.status === "Completed"
                    ? "bg-success-50 text-success-500"
                    : order.status === "Pending"
                      ? "bg-warning-50 text-warning-500"
                      : order.status === "PartiallyRefunded"
                        ? "bg-amber-50 text-amber-600"
                        : order.status === "Refunded"
                          ? "bg-danger-50 text-danger-500"
                          : "bg-gray-50 text-gray-500",
                )}
              >
                {ORDER_STATUS[order.status]?.label}
              </span>
            </div>

            {/* Customer */}
            {order.customerId ? (
              <div className="p-3 bg-gray-50 rounded-lg border border-gray-100">
                <p className="text-sm font-medium text-gray-500 mb-2">
                  معلومات العميل
                </p>
                <div className="space-y-1.5">
                  {order.customerName && (
                    <div className="flex items-center gap-2">
                      <User className="w-4 h-4 text-primary-500" />
                      <span className="font-medium text-gray-800">
                        {order.customerName}
                      </span>
                    </div>
                  )}
                  {order.customerPhone && (
                    <div className="flex items-center gap-2 text-sm text-gray-600">
                      <Phone className="w-4 h-4 text-gray-400" />
                      <span dir="ltr">{order.customerPhone}</span>
                    </div>
                  )}
                </div>
              </div>
            ) : (
              <div className="flex items-center justify-between">
                <span className="text-gray-500">العميل</span>
                <span className="text-gray-400">عميل نقدي</span>
              </div>
            )}

            {/* Items */}
            <div>
              <h3 className="font-semibold mb-3">المنتجات</h3>
              <div className="space-y-2">
                {order.items.map((item) => (
                  <div key={item.id} className="p-3 bg-gray-50 rounded-lg">
                    <div className="flex items-center justify-between">
                      <div>
                        <p className="font-medium">{item.productName}</p>
                        <p className="text-sm text-gray-500">
                          {item.quantity} × {formatCurrency(item.unitPrice)}
                          {item.refundedQuantity > 0 && (
                            <span className="text-orange-500 mr-2">
                              (مسترجع: {item.refundedQuantity})
                            </span>
                          )}
                        </p>
                      </div>
                      <div className="text-left shrink-0">
                        {item.discountAmount > 0 ? (
                          <>
                            <p className="text-sm text-gray-400 line-through">
                              {formatCurrency(item.unitPrice * item.quantity)}
                            </p>
                            <p className="font-semibold text-success-600">
                              {formatCurrency(item.total)}
                            </p>
                          </>
                        ) : (
                          <p className="font-semibold">
                            {formatCurrency(item.total)}
                          </p>
                        )}
                      </div>
                    </div>
                    {item.discountAmount > 0 && (
                      <div className="flex items-center gap-1.5 mt-1.5">
                        <Tag className="w-3.5 h-3.5 text-success-600" />
                        <span className="text-xs text-success-600 font-medium">
                          خصم{" "}
                          {item.discountType === "Percentage" ||
                          item.discountType === "percentage"
                            ? `${item.discountValue}%`
                            : formatCurrency(item.discountValue ?? 0)}{" "}
                          (-{formatCurrency(item.discountAmount)})
                        </span>
                        {item.discountReason && (
                          <span className="text-xs text-gray-400">
                            • {item.discountReason}
                          </span>
                        )}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>

            {/* Summary */}
            <div className="border-t pt-4 space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-gray-500">المجموع الفرعي</span>
                <span>{formatCurrency(order.subtotal)}</span>
              </div>
              {/* Item-level discounts total */}
              {order.items.some((i) => i.discountAmount > 0) && (
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 flex items-center gap-1">
                    <Tag className="w-3.5 h-3.5" />
                    خصومات المنتجات
                  </span>
                  <span className="text-success-600">
                    -
                    {formatCurrency(
                      order.items.reduce((s, i) => s + i.discountAmount, 0),
                    )}
                  </span>
                </div>
              )}
              {order.discountAmount > 0 && (
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500">
                    خصم الطلب
                    {(order.discountType === "Percentage" ||
                      order.discountType === "percentage") &&
                    order.discountValue
                      ? ` (${order.discountValue}%)`
                      : ""}
                  </span>
                  <span className="text-danger-500">
                    -{formatCurrency(order.discountAmount)}
                  </span>
                </div>
              )}
              <div className="flex justify-between text-sm">
                <span className="text-gray-500">
                  الضريبة ({order.taxRate}%)
                </span>
                <span>{formatCurrency(order.taxAmount)}</span>
              </div>
              <div className="flex justify-between text-lg font-bold pt-2 border-t">
                <span>الإجمالي</span>
                <span className="text-primary-600">
                  {formatCurrency(order.total)}
                </span>
              </div>

              {/* Refund Amount - For partial or full refund */}
              {hasRefund && order.refundAmount > 0 && (
                <div className="flex justify-between text-sm pt-2 border-t border-dashed">
                  <span className="text-danger-500 font-medium">
                    {isFullyRefunded
                      ? "مبلغ الاسترجاع الكامل"
                      : "مبلغ الاسترجاع الجزئي"}
                  </span>
                  <span className="text-danger-500 font-semibold">
                    -{formatCurrency(order.refundAmount)}
                  </span>
                </div>
              )}

              {/* Net Amount After Partial Refund */}
              {isPartiallyRefunded && order.refundAmount > 0 && (
                <div className="flex justify-between text-sm font-medium">
                  <span className="text-gray-600">الصافي بعد الاسترجاع</span>
                  <span className="text-success-600">
                    {formatCurrency(order.total - order.refundAmount)}
                  </span>
                </div>
              )}
            </div>

            {/* Payments */}
            {order.payments.length > 0 && (
              <div>
                <h3 className="font-semibold mb-3">المدفوعات</h3>
                <div className="space-y-2">
                  {order.payments.map((payment) => (
                    <div
                      key={payment.id}
                      className="flex items-center justify-between p-3 bg-gray-50 rounded-lg"
                    >
                      <span>{PAYMENT_METHODS[payment.method]?.label}</span>
                      <span className="font-semibold">
                        {formatCurrency(payment.amount)}
                      </span>
                    </div>
                  ))}
                </div>
                {order.changeAmount > 0 && (
                  <div className="flex justify-between mt-2 text-sm">
                    <span className="text-gray-500">الباقي</span>
                    <span className="text-success-500">
                      {formatCurrency(order.changeAmount)}
                    </span>
                  </div>
                )}
              </div>
            )}

            {/* Notes */}
            {order.notes && (
              <div>
                <h3 className="font-semibold mb-2">ملاحظات</h3>
                <p className="text-gray-500 bg-gray-50 p-3 rounded-lg">
                  {order.notes}
                </p>
              </div>
            )}

            {/* Refund Info - For both full and partial refunds */}
            {hasRefund && order.refundReason && (
              <div
                className={clsx(
                  "border rounded-lg p-4",
                  isFullyRefunded
                    ? "bg-danger-50 border-danger-200"
                    : "bg-amber-50 border-amber-200",
                )}
              >
                <h3
                  className={clsx(
                    "font-semibold mb-2",
                    isFullyRefunded ? "text-danger-700" : "text-amber-700",
                  )}
                >
                  {isFullyRefunded
                    ? "معلومات الاسترجاع الكامل"
                    : "معلومات الاسترجاع الجزئي"}
                </h3>
                <div
                  className={clsx(
                    "text-sm space-y-1",
                    isFullyRefunded ? "text-danger-600" : "text-amber-700",
                  )}
                >
                  <p>
                    <span className="font-medium">السبب:</span>{" "}
                    {order.refundReason}
                  </p>
                  {order.refundedAt && (
                    <p>
                      <span className="font-medium">التاريخ:</span>{" "}
                      {formatDateTime(order.refundedAt)}
                    </p>
                  )}
                  {order.refundedByUserName && (
                    <p>
                      <span className="font-medium">بواسطة:</span>{" "}
                      {order.refundedByUserName}
                    </p>
                  )}
                  {order.refundAmount > 0 && (
                    <p>
                      <span className="font-medium">المبلغ المسترد:</span>{" "}
                      {formatCurrency(order.refundAmount)}
                    </p>
                  )}
                </div>
              </div>
            )}
          </div>

          {/* Refund Modal */}
          {showRefundModal && (
            <RefundModal
              order={order}
              onClose={() => setShowRefundModal(false)}
              onSuccess={onClose}
            />
          )}
        </div>
      </div>
    </Portal>
  );
};
