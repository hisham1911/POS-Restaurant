import { useState } from "react";
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
import { useCart } from "@/hooks/useCart";
import { useOrders } from "@/hooks/useOrders";
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
  onOrderComplete?: () => void;
}

const paymentMethods: {
  id: PaymentMethod;
  label: string;
  icon: React.ReactNode;
}[] = [
  { id: "Cash", label: "نقدي", icon: <Banknote className="w-8 h-8" /> },
  { id: "Card", label: "بطاقة", icon: <CreditCard className="w-8 h-8" /> },
  { id: "Fawry", label: "فوري", icon: <Building2 className="w-8 h-8" /> },
];

export const PaymentModal = ({
  onClose,
  selectedCustomer,
  onOrderComplete,
}: PaymentModalProps) => {
  const { total, clearCart } = useCart();
  const { createOrder, completeOrder, isCreating, isCompleting } = useOrders();
  const [selectedMethod, setSelectedMethod] = useState<PaymentMethod>("Cash");
  const [amountPaid, setAmountPaid] = useState<string>(total.toFixed(2));
  const [showError, setShowError] = useState(false);
  const [allowPartialPayment, setAllowPartialPayment] = useState(false);

  const customerId = selectedCustomer?.id;

  const numericAmount = parseFloat(amountPaid) || 0;
  const change = numericAmount - total;
  const amountDue = total - numericAmount;

  // Calculate available credit for customer
  const availableCredit = selectedCustomer
    ? selectedCustomer.creditLimit - selectedCustomer.totalDue
    : 0;

  // Check if customer can take credit
  const canTakeCredit =
    selectedCustomer &&
    selectedCustomer.isActive &&
    (selectedCustomer.creditLimit === 0 ||
      amountDue <= availableCredit);

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
    if (numericAmount < total && selectedCustomer && !selectedCustomer.isActive) {
      toast.error("العميل غير نشط - لا يمكن البيع الآجل");
      return;
    }

    // Validate credit limit
    if (numericAmount < total && creditLimitExceeded) {
      toast.error(
        `تجاوز حد الائتمان. المتاح: ${formatCurrency(availableCredit)} ج.م، المطلوب: ${formatCurrency(amountDue)} ج.م`,
        { duration: 5000 }
      );
      return;
    }

    try {
      // 1. إنشاء الطلب أولاً (مع العميل إن وجد)
      const order = await createOrder(customerId);
      if (!order) {
        // فشل إنشاء الطلب - لا نغلق النافذة، السلة محفوظة
        return;
      }

      // 2. إكمال الطلب بالدفع
      const completedOrder = await completeOrder(order.id, {
        payments: [
          {
            method: selectedMethod,
            amount: numericAmount,
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

  return (
    <Portal>
      <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-[100] p-4">
        <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-hidden animate-scale-in flex flex-col">
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b shrink-0">
            <h2 className="text-xl font-bold">الدفع</h2>
            <button
              onClick={onClose}
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
                              100
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
                        تجاوز حد الائتمان - المتاح: {formatCurrency(availableCredit)}
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
