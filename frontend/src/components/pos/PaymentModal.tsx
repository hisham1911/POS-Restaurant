import { useEffect, useState } from "react";
import {
  X,
  Check,
  Banknote,
  CreditCard,
  Building2,
  User,
  Phone,
  Star,
  AlertCircle,
} from "lucide-react";
import { useOrders } from "@/hooks/useOrders";
import { usePreparedPaymentOrder } from "@/hooks/usePreparedPaymentOrder";
import { useCart } from "@/hooks/useCart";
import { formatCurrency } from "@/utils/formatters";
import { PaymentMethod } from "@/types/order.types";
import { Customer } from "@/types/customer.types";
import { Button } from "@/components/common/Button";
import { toast } from "sonner";
import clsx from "clsx";
import { Portal } from "@/components/common/Portal";

interface PaymentModalProps {
  onClose: () => void;
  selectedCustomer?: Customer | null;
  orderType: "Standard" | "Delivery";
  deliveryAddress: string;
  deliveryFee: string;
  deliveryNotes: string;
  onOrderComplete?: () => void;
}

const paymentMethods: {
  id: PaymentMethod;
  label: string;
  icon: React.ReactNode;
}[] = [
  { id: "Cash", label: "نقدي", icon: <Banknote className="w-8 h-8" /> },
  { id: "Card", label: "فيزا", icon: <CreditCard className="w-8 h-8" /> },
  {
    id: "Fawry",
    label: "فودافون كاش",
    icon: <Building2 className="w-8 h-8" />,
  },
];

export const PaymentModal = ({
  onClose,
  selectedCustomer,
  orderType,
  deliveryAddress,
  deliveryFee,
  deliveryNotes,
  onOrderComplete,
}: PaymentModalProps) => {
  const { createOrder, completeOrder, cancelOrder, isCreating, isCompleting } =
    useOrders();
  const { clearCart } = useCart();
  const [selectedMethod, setSelectedMethod] = useState<PaymentMethod>("Cash");
  const [amountPaid, setAmountPaid] = useState<string>("");
  const [transactionReference, setTransactionReference] = useState("");
  const [showError, setShowError] = useState(false);
  const [allowPartialPayment, setAllowPartialPayment] = useState(false);
  const isDeliveryOrder = orderType === "Delivery";
  const parsedDeliveryFee =
    isDeliveryOrder ? Number.parseFloat(deliveryFee || "0") || 0 : 0;

  const customerId = selectedCustomer?.id;
  const {
    preparedOrder,
    isPreparingOrder,
    markPreparedOrderCompleted,
    discardPreparedOrder,
  } = usePreparedPaymentOrder({
    enabled: true,
    customerId,
    orderType: isDeliveryOrder ? "Delivery" : "DineIn",
    deliveryAddress: isDeliveryOrder ? deliveryAddress || undefined : undefined,
    deliveryFee: isDeliveryOrder ? parsedDeliveryFee : 0,
    deliveryNotes: isDeliveryOrder ? deliveryNotes || undefined : undefined,
    createOrder,
    cancelOrder,
    onPrepareFailed: onClose,
  });
  const total = preparedOrder?.total ?? 0;

  useEffect(() => {
    if (preparedOrder) {
      setAmountPaid(preparedOrder.total.toFixed(2));
    }
  }, [preparedOrder]);

  useEffect(() => {
    if (selectedMethod === "Cash") {
      setTransactionReference("");
      return;
    }

    setAllowPartialPayment(false);
    setAmountPaid(total.toFixed(2));
  }, [selectedMethod, total]);

  const numericAmount = parseFloat(amountPaid) || 0;
  const change = numericAmount - total;
  const amountDue = total - numericAmount;
  const requiresTransactionReference = selectedMethod !== "Cash";

  const handleClose = async () => {
    await discardPreparedOrder();
    onClose();
  };

  // Calculate available credit for customer
  const availableCredit = selectedCustomer
    ? selectedCustomer.creditLimit - selectedCustomer.totalDue
    : 0;

  // Check if customer can take credit
  const canTakeCredit =
    selectedCustomer &&
    selectedCustomer.isActive &&
    (selectedCustomer.creditLimit === 0 || amountDue <= availableCredit);

  const creditLimitExceeded =
    selectedCustomer &&
    selectedCustomer.creditLimit > 0 &&
    amountDue > availableCredit;

  const handleNumpadClick = (value: string) => {
    if (value === "C") {
      setAmountPaid("");
    } else if (value === "←") {
      setAmountPaid((prev) => prev.slice(0, -1));
    } else if (value === ".") {
      if (!amountPaid.includes(".")) {
        setAmountPaid((prev) => prev + ".");
      }
    } else {
      setAmountPaid((prev) => prev + value);
    }
  };

  const handleQuickAmount = (amount: number) => {
    setAmountPaid(amount.toFixed(2));
  };

  const handleComplete = async () => {
    if (!preparedOrder) {
      return;
    }

    // Validate payment amount
    if (numericAmount < total && !allowPartialPayment) {
      setShowError(true);
      setTimeout(() => setShowError(false), 500);
      toast.error("المبلغ المدفوع أقل من الإجمالي");
      return;
    }

    // Validate partial payment requires customer
    if (numericAmount < total && !selectedCustomer) {
      toast.error("البيع الآجل يتطلب ربط عميل بالطلب");
      return;
    }

    // Validate customer is active
    if (
      numericAmount < total &&
      selectedCustomer &&
      !selectedCustomer.isActive
    ) {
      toast.error("العميل غير نشط - لا يمكن البيع الآجل");
      return;
    }

    // Validate credit limit
    if (numericAmount < total && creditLimitExceeded) {
      toast.error(
        `تجاوز حد الائتمان. المتاح بعد رصيد العميل عبر كل الفروع: ${formatCurrency(availableCredit)} ج.م، المطلوب آجلاً: ${formatCurrency(amountDue)} ج.م`,
        { duration: 5000 },
      );
      return;

      toast.error(
        `تجاوز حد الائتمان. المتاح: ${formatCurrency(availableCredit)} ج.م، المطلوب: ${formatCurrency(amountDue)} ج.م`,
        { duration: 5000 },
      );
      return;
    }

    if (requiresTransactionReference && !transactionReference.trim()) {
      toast.error("رقم المعاملة مطلوب عند الدفع بفودافون كاش أو فيزا");
      return;
    }

    try {
      // 1. إنشاء الطلب أولاً (مع العميل إن وجد)
      const order = preparedOrder;
      if (!order) {
        // فشل إنشاء الطلب - لا نغلق النافذة، السلة محفوظة
        return;
      }

      // 2. إكمال الطلب بالدفع
      const completedOrder = await completeOrder(preparedOrder.id, {
        payments: [
          {
            method: selectedMethod,
            amount: numericAmount,
            reference: requiresTransactionReference
              ? transactionReference.trim()
              : undefined,
          },
        ],
      });

      if (completedOrder) {
        // نجاح - عرض الباقي أو المبلغ المستحق
        if (change > 0) {
          toast.success(`تم إتمام الدفع! الباقي: ${formatCurrency(change)}`);
        } else if (amountDue > 0) {
          toast.success(
            `تم إتمام البيع الآجل! المبلغ المستحق: ${formatCurrency(amountDue)}`,
          );
        } else {
          toast.success("تم إتمام الدفع بنجاح!");
        }
        // مسح العميل المحدد
        if (isDeliveryOrder) {
          toast.info(
            "تم إنشاء طلب التوصيل — يمكن تعيين المندوب من شاشة إدارة التوصيل",
          );
        }

        markPreparedOrderCompleted(preparedOrder.id);
        clearCart();
        setTransactionReference("");
        onOrderComplete?.();
        onClose();
      }
      // فشل إكمال الطلب - لا نغلق النافذة، السلة محفوظة
    } catch {
      // خطأ غير متوقع - لا نغلق النافذة
      toast.error("حدث خطأ غير متوقع");
    }
  };

  const quickAmounts = [50, 100, 200, 500];

  if (isPreparingOrder || !preparedOrder) {
    return (
      <Portal>
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 p-4">
          <div className="flex w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl">
            <div className="flex items-center justify-between border-b p-6">
              <h2 className="text-xl font-bold">الدفع</h2>
              <button
                onClick={handleClose}
                className="flex h-10 w-10 items-center justify-center rounded-lg bg-gray-100 transition-colors hover:bg-danger-50 hover:text-danger-500"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <div className="flex min-h-[280px] flex-col items-center justify-center gap-4 p-6 text-center">
              <div className="h-10 w-10 animate-spin rounded-full border-4 border-primary-100 border-t-primary-600" />
              <div>
                <p className="text-lg font-bold text-slate-900">
                  جارٍ تأكيد إجمالي الطلب
                </p>
                <p className="mt-1 text-sm text-slate-500">
                  يتم الآن إنشاء الطلب ومزامنة الإجمالي من الباك-إند قبل الدفع.
                </p>
              </div>
            </div>
          </div>
        </div>
      </Portal>
    );
  }

  return (
    <Portal>
      <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-[100] p-4">
        <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-hidden animate-scale-in flex flex-col">
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b shrink-0">
            <h2 className="text-xl font-bold">الدفع</h2>
            <button
              onClick={handleClose}
              className="w-10 h-10 rounded-lg bg-gray-100 flex items-center justify-center hover:bg-danger-50 hover:text-danger-500 transition-colors"
            >
              <X className="w-5 h-5" />
            </button>
          </div>

          <div className="flex-1 overflow-y-auto p-6 space-y-6">
            {/* Customer Info */}
            <div className="p-4 bg-gray-50 rounded-xl border border-gray-200">
              <p className="text-sm font-medium text-gray-500 mb-2">
                معلومات العميل
              </p>
              {selectedCustomer ? (
                <div className="space-y-2">
                  <div className="flex items-center gap-2">
                    <User className="w-4 h-4 text-primary-500" />
                    <span className="font-medium text-gray-800">
                      {selectedCustomer.name || selectedCustomer.phone}
                    </span>
                  </div>
                  {selectedCustomer.phone && selectedCustomer.name && (
                    <div className="flex items-center gap-2 text-sm text-gray-600">
                      <Phone className="w-4 h-4 text-gray-400" />
                      <span dir="ltr">{selectedCustomer.phone}</span>
                    </div>
                  )}
                  {(selectedCustomer.loyaltyPoints ?? 0) > 0 && (
                    <div className="flex items-center gap-2 text-sm text-amber-600">
                      <Star className="w-4 h-4 text-amber-500" />
                      <span>{selectedCustomer.loyaltyPoints} نقطة ولاء</span>
                    </div>
                  )}
                  {selectedCustomer.totalDue > 0 && (
                    <div className="flex items-center gap-2 text-sm text-orange-600">
                      <AlertCircle className="w-4 h-4 text-orange-500" />
                      <span>
                        رصيد مستحق: {formatCurrency(selectedCustomer.totalDue)}
                      </span>
                    </div>
                  )}
                  {selectedCustomer.creditLimit > 0 && (
                    <div className="mt-2 p-2 bg-white rounded border border-gray-200">
                      <div className="flex justify-between text-xs mb-1">
                        <span className="text-gray-600">حد الائتمان:</span>
                        <span className="font-medium">
                          {formatCurrency(selectedCustomer.creditLimit)}
                        </span>
                      </div>
                      <div className="flex justify-between text-xs mb-1">
                        <span className="text-gray-600">المستخدم:</span>
                        <span className="font-medium text-orange-600">
                          {formatCurrency(selectedCustomer.totalDue)}
                        </span>
                      </div>
                      <div className="flex justify-between text-xs">
                        <span className="text-gray-600">المتاح:</span>
                        <span
                          className={`font-medium ${
                            availableCredit < 0
                              ? "text-danger-600"
                              : "text-success-600"
                          }`}
                        >
                          {formatCurrency(availableCredit)}
                        </span>
                      </div>
                      {/* Progress bar */}
                      <div className="mt-2 h-1.5 bg-gray-200 rounded-full overflow-hidden">
                        <div
                          className={`h-full transition-all ${
                            selectedCustomer.totalDue /
                              selectedCustomer.creditLimit >
                            0.9
                              ? "bg-danger-500"
                              : selectedCustomer.totalDue /
                                    selectedCustomer.creditLimit >
                                  0.7
                                ? "bg-orange-500"
                                : "bg-success-500"
                          }`}
                          style={{
                            width: `${Math.min(
                              (selectedCustomer.totalDue /
                                selectedCustomer.creditLimit) *
                                100,
                              100,
                            )}%`,
                          }}
                        />
                      </div>
                    </div>
                  )}
                </div>
              ) : (
                <div className="flex items-center gap-2 text-gray-500">
                  <User className="w-4 h-4" />
                  <span>عميل نقدي</span>
                </div>
              )}
            </div>

            {isDeliveryOrder && (
              <div className="rounded-xl border border-primary-200 bg-primary-50 p-4">
                <p className="mb-2 text-sm font-medium text-primary-700">
                  بيانات التوصيل
                </p>
                <div className="space-y-2 text-sm text-gray-700">
                  <div className="flex items-center justify-between gap-3">
                    <span className="text-gray-500">نوع الطلب</span>
                    <span className="font-semibold text-primary-700">
                      توصيل
                    </span>
                  </div>
                  <div className="flex items-center justify-between gap-3">
                    <span className="text-gray-500">العنوان</span>
                    <span className="text-end font-medium text-gray-900">
                      {deliveryAddress || "—"}
                    </span>
                  </div>
                  <div className="flex items-center justify-between gap-3">
                    <span className="text-gray-500">رسوم التوصيل</span>
                    <span className="font-semibold text-gray-900">
                      {formatCurrency(parsedDeliveryFee)}
                    </span>
                  </div>
                  <div className="flex items-center justify-between gap-3">
                    <span className="text-gray-500">الملاحظات</span>
                    <span className="text-end font-medium text-gray-900">
                      {deliveryNotes || "—"}
                    </span>
                  </div>
                </div>
              </div>
            )}

            {/* Total Amount */}
            <div className="text-center p-6 bg-gray-50 rounded-xl">
              <p className="text-gray-500 mb-1">الإجمالي المطلوب</p>
              <p className="text-4xl font-bold text-primary-600">
                {formatCurrency(total)}
              </p>
            </div>

            {/* Payment Methods */}
            <div>
              <p className="text-sm font-medium text-gray-500 mb-3">
                طريقة الدفع
              </p>
              <div className="grid grid-cols-3 gap-3">
                {paymentMethods.map((method) => (
                  <button
                    key={method.id}
                    onClick={() => setSelectedMethod(method.id)}
                    className={clsx(
                      "flex flex-col items-center gap-2 p-4 rounded-xl border-2 transition-all",
                      selectedMethod === method.id
                        ? "border-primary-600 bg-primary-50 text-primary-600"
                        : "border-gray-200 hover:border-gray-300",
                    )}
                  >
                    {method.icon}
                    <span className="mt-2 font-medium">{method.label}</span>
                  </button>
                ))}
              </div>
            </div>

            {requiresTransactionReference && (
              <div>
                <p className="text-sm font-medium text-gray-500 mb-3">
                  رقم المعاملة <span className="text-danger-500">*</span>
                </p>
                <input
                  type="text"
                  value={transactionReference}
                  onChange={(event) =>
                    setTransactionReference(event.target.value)
                  }
                  className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm font-medium text-gray-700 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-100"
                  placeholder="اكتب رقم العملية من فودافون كاش أو الفيزا"
                />
              </div>
            )}

            {/* Amount Input (for Cash) */}
            {selectedMethod === "Cash" && (
              <div className="space-y-4">
                <div>
                  <p className="text-sm font-medium text-gray-500 mb-3">
                    المبلغ المدفوع
                  </p>
                  <div
                    className={clsx(
                      "text-center p-4 bg-gray-50 rounded-xl transition-all",
                      showError && "animate-shake border-2 border-danger-500",
                    )}
                  >
                    <p className="text-3xl font-bold">
                      {amountPaid || "0"}{" "}
                      <span className="text-lg text-gray-400">ج.م</span>
                    </p>
                  </div>
                </div>

                {/* Quick Amounts */}
                <div className="flex gap-2">
                  {quickAmounts.map((amount) => (
                    <button
                      key={amount}
                      onClick={() => handleQuickAmount(amount)}
                      className="flex-1 py-2 rounded-lg bg-gray-100 font-medium hover:bg-primary-100 hover:text-primary-600 transition-colors"
                    >
                      {amount}
                    </button>
                  ))}
                  <button
                    onClick={() => handleQuickAmount(total)}
                    className="flex-1 py-2 rounded-lg bg-primary-600 text-white font-medium hover:bg-primary-700 transition-colors"
                  >
                    تمام
                  </button>
                </div>

                {/* Numpad */}
                <div className="grid grid-cols-4 gap-2">
                  {[
                    "7",
                    "8",
                    "9",
                    "←",
                    "4",
                    "5",
                    "6",
                    "C",
                    "1",
                    "2",
                    "3",
                    ".",
                    "0",
                    "00",
                  ].map((key) => (
                    <button
                      key={key}
                      onClick={() => handleNumpadClick(key)}
                      className={clsx(
                        "h-14 rounded-lg bg-gray-100 font-semibold text-xl hover:bg-gray-200 active:bg-gray-300 transition-colors",
                        key === "0" && "col-span-2",
                      )}
                    >
                      {key}
                    </button>
                  ))}
                </div>

                {/* Change */}
                {change > 0 && (
                  <div className="text-center p-4 bg-success-50 rounded-xl border border-success-200">
                    <p className="text-gray-500 text-sm">الباقي</p>
                    <p className="text-2xl font-bold text-success-500">
                      {formatCurrency(change)}
                    </p>
                  </div>
                )}

                {/* Amount Due (Partial Payment) */}
                {numericAmount < total && numericAmount > 0 && (
                  <div
                    className={clsx(
                      "text-center p-4 rounded-xl border",
                      creditLimitExceeded
                        ? "bg-danger-50 border-danger-200"
                        : "bg-orange-50 border-orange-200",
                    )}
                  >
                    <p className="text-gray-500 text-sm">المبلغ المستحق</p>
                    <p
                      className={clsx(
                        "text-2xl font-bold",
                        creditLimitExceeded
                          ? "text-danger-500"
                          : "text-orange-500",
                      )}
                    >
                      {formatCurrency(amountDue)}
                    </p>
                    {creditLimitExceeded && (
                      <p className="text-xs text-danger-600 mt-1">
                        تجاوز حد الائتمان - المتاح:{" "}
                        {formatCurrency(availableCredit)}
                      </p>
                    )}
                    {selectedCustomer && !selectedCustomer.isActive && (
                      <p className="text-xs text-danger-600 mt-1">
                        العميل غير نشط
                      </p>
                    )}
                  </div>
                )}
              </div>
            )}

            {/* Partial Payment Option */}
            {selectedCustomer && canTakeCredit && (
              <div className="flex items-center gap-3 p-4 bg-blue-50 rounded-xl border border-blue-200">
                <input
                  type="checkbox"
                  id="partialPayment"
                  checked={allowPartialPayment}
                  onChange={(e) => setAllowPartialPayment(e.target.checked)}
                  className="w-5 h-5 text-primary-600 rounded focus:ring-2 focus:ring-primary-500"
                />
                <label
                  htmlFor="partialPayment"
                  className="flex-1 cursor-pointer"
                >
                  <p className="font-medium text-gray-800">
                    السماح بالدفع الجزئي (بيع آجل)
                  </p>
                  <p className="text-sm text-gray-600">
                    يمكن للعميل دفع جزء من المبلغ والباقي يُسجل كدين
                  </p>
                </label>
              </div>
            )}

            {/* Complete Button */}
            <Button
              variant="success"
              size="xl"
              className="w-full"
              onClick={handleComplete}
              isLoading={isCreating || isCompleting}
              disabled={
                isCreating ||
                isCompleting ||
                (numericAmount < total && !allowPartialPayment) ||
                (numericAmount < total && creditLimitExceeded)
              }
              rightIcon={<Check className="w-5 h-5" />}
            >
              {isCreating
                ? "جاري إنشاء الطلب..."
                : isCompleting
                  ? "جاري الدفع..."
                  : numericAmount < total && allowPartialPayment
                    ? `إتمام البيع الآجل (مستحق: ${formatCurrency(amountDue)})`
                    : "إتمام الدفع"}
            </Button>
          </div>
        </div>
      </div>
    </Portal>
  );
};
