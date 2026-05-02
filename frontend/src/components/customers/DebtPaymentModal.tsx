import React, { useState } from "react";
import {
  X,
  DollarSign,
  CreditCard,
  Banknote,
  AlertTriangle,
} from "lucide-react";
import { usePayDebtMutation } from "../../api/customersApi";
import { Customer, PayDebtRequest } from "../../types/customer.types";
import { toast } from "sonner";
import { Portal } from "@/components/common/Portal";
import clsx from "clsx";
import { extractApiData } from "@/utils/apiResponse";
import { useGetCurrentShiftQuery } from "@/api/shiftsApi";

interface DebtPaymentModalProps {
  customer: Customer;
  onClose: () => void;
  onSuccess?: () => void;
}

export const DebtPaymentModal: React.FC<DebtPaymentModalProps> = ({
  customer,
  onClose,
  onSuccess,
}) => {
  const noOpenShiftMessage = "يجب فتح وردية أولاً قبل تسجيل سداد الدين";
  const [payDebt, { isLoading }] = usePayDebtMutation();
  const {
    data: currentShiftResponse,
    isLoading: isShiftLoading,
    isFetching: isShiftFetching,
  } = useGetCurrentShiftQuery(undefined, {
    refetchOnMountOrArgChange: true,
  });
  const hasOpenShift = Boolean(currentShiftResponse?.data);
  const isSubmitDisabled =
    isLoading || isShiftLoading || isShiftFetching || !hasOpenShift;

  const [formData, setFormData] = useState<
    Omit<PayDebtRequest, "amount"> & { amount: string | number }
  >({
    amount: String(customer.totalDue) as string | number,
    paymentMethod: "Cash",
    referenceNumber: "",
    notes: "",
  });

  const [errors, setErrors] = useState<Record<string, string>>({});

  const validate = (): boolean => {
    const newErrors: Record<string, string> = {};

    const numAmount = Number(formData.amount) || 0;
    if (!formData.amount || Number(formData.amount) <= 0) {
      newErrors.amount = "المبلغ مطلوب";
    } else if (Number(formData.amount) > customer.branchAmountDue) {
      newErrors.amount = `المبلغ لا يمكن أن يتجاوز دين هذا الفرع (${formatCurrency(customer.branchAmountDue)})`;
    }

    if (
      formData.paymentMethod !== "Cash" &&
      !String(formData.referenceNumber || "").trim()
    ) {
      newErrors.referenceNumber =
        "رقم المعاملة مطلوب عند الدفع بفودافون كاش أو فيزا";
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!hasOpenShift) {
      toast.error(noOpenShiftMessage);
      return;
    }

    if (!validate()) return;

    try {
      const response = await payDebt({
        customerId: customer.id,
        data: {
          ...formData,
          amount: Number(formData.amount) || 0,
        },
      }).unwrap();
      const paymentResult = extractApiData(
        response,
        "CUSTOMER_DEBT_PAYMENT_EMPTY_RESPONSE",
        "فشل تسديد الدين",
      );

      toast.success(paymentResult.message || "تم تسديد الدين بنجاح");
      onSuccess?.();
      onClose();
    } catch (error) {
      console.error("Error paying debt:", error);
      // baseApi.ts already shows error toast
    }
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat("ar-EG", {
      style: "currency",
      currency: "EGP",
      minimumFractionDigits: 2,
    }).format(amount);
  };

  const paymentMethods = [
    { id: "Cash", label: "نقدي", icon: <Banknote className="w-5 h-5" /> },
    { id: "Card", label: "فيزا", icon: <CreditCard className="w-5 h-5" /> },
    {
      id: "BankTransfer",
      label: "تحويل بنكي",
      icon: <DollarSign className="w-5 h-5" />,
    },
    {
      id: "Fawry",
      label: "فودافون كاش",
      icon: <DollarSign className="w-5 h-5" />,
    },
  ];

  return (
    <Portal>
      {/* Backdrop */}
      <div className="fixed inset-0 bg-black/60 z-[110]" onClick={onClose} />

      {/* Modal */}
      <div className="fixed inset-0 z-[110] flex items-center justify-center p-4">
        <div
          className="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] flex flex-col"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b">
            <div className="flex items-center gap-3">
              <div className="w-12 h-12 bg-orange-100 rounded-full flex items-center justify-center">
                <DollarSign className="w-6 h-6 text-orange-600" />
              </div>
              <div>
                <h2 className="text-xl font-bold text-gray-800">تسديد دين</h2>
                <p className="text-sm text-gray-500">
                  {customer.name || customer.phone}
                </p>
              </div>
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
              disabled={isLoading}
            >
              <X className="w-5 h-5 text-gray-500" />
            </button>
          </div>

          {/* Body */}
          <form
            id="debt-payment-form"
            onSubmit={handleSubmit}
            className="flex-1 overflow-y-auto p-6 space-y-5"
          >
            {/* Customer Info */}
            <div className="bg-gradient-to-br from-orange-50 to-orange-100 p-4 rounded-xl border border-orange-200">
              <div className="flex justify-between items-center mb-3">
                <span className="text-sm text-gray-600">دين هذا الفرع</span>
                <span className="text-3xl font-bold text-orange-600">
                  {formatCurrency(customer.branchAmountDue)}
                </span>
              </div>
              {customer.totalDue > customer.branchAmountDue && (
                <div className="text-xs text-gray-500 text-center mb-1">
                  إجمالي الدين عبر جميع الفروع: {formatCurrency(customer.totalDue)}
                </div>
              )}
              {customer.creditLimit > 0 && (
                <div className="text-xs text-gray-500 text-center">
                  حد الائتمان: {formatCurrency(customer.creditLimit)}
                </div>
              )}
            </div>

            {isShiftLoading || isShiftFetching ? (
              <div className="rounded-xl border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-700">
                جارٍ التحقق من الوردية المفتوحة قبل تسجيل السداد...
              </div>
            ) : !hasOpenShift ? (
              <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3">
                <div className="flex items-start gap-3 text-red-700">
                  <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" />
                  <div>
                    <p className="font-semibold">لا توجد وردية مفتوحة</p>
                    <p className="mt-1 text-sm text-red-600">
                      {noOpenShiftMessage} حتى يظهر المبلغ في تقارير النقدية
                      الخاصة بالوردية.
                    </p>
                  </div>
                </div>
              </div>
            ) : null}

            {/* Amount */}
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-2">
                المبلغ المدفوع <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <input
                  type="number"
                  step="0.01"
                  value={formData.amount}
                  onChange={(e) =>
                    setFormData({ ...formData, amount: e.target.value })
                  }
                  className={clsx(
                    "w-full px-4 py-3 pl-12 border-2 rounded-xl focus:ring-2 focus:ring-orange-500 focus:border-orange-500 text-lg font-semibold text-right",
                    errors.amount ? "border-red-500" : "border-gray-200",
                  )}
                  placeholder="0.00"
                  disabled={isLoading}
                  dir="rtl"
                />
                <span className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-400 text-sm">
                  ج.م
                </span>
              </div>
              {errors.amount && (
                <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                  <span>⚠️</span> {errors.amount}
                </p>
              )}
              <div className="flex gap-2 mt-3">
                <button
                  type="button"
                  onClick={() =>
                    setFormData({ ...formData, amount: customer.branchAmountDue })
                  }
                  className="flex-1 text-sm px-3 py-2 bg-orange-100 hover:bg-orange-200 text-orange-700 font-medium rounded-lg transition-colors"
                  disabled={isLoading}
                >
                  الكل ({formatCurrency(customer.branchAmountDue)})
                </button>
                <button
                  type="button"
                  onClick={() =>
                    setFormData({ ...formData, amount: customer.branchAmountDue / 2 })
                  }
                  className="flex-1 text-sm px-3 py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 font-medium rounded-lg transition-colors"
                  disabled={isLoading}
                >
                  النصف ({formatCurrency(customer.branchAmountDue / 2)})
                </button>
              </div>
            </div>

            {/* Payment Method */}
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-3">
                طريقة الدفع <span className="text-red-500">*</span>
              </label>
              <div className="grid grid-cols-4 gap-3">
                {paymentMethods.map((method) => (
                  <button
                    key={method.id}
                    type="button"
                    onClick={() =>
                      setFormData({
                        ...formData,
                        paymentMethod:
                          method.id as PayDebtRequest["paymentMethod"],
                      })
                    }
                    className={clsx(
                      "flex flex-col items-center gap-2 p-4 rounded-xl border-2 transition-all",
                      formData.paymentMethod === method.id
                        ? "border-orange-600 bg-orange-50 text-orange-600"
                        : "border-gray-200 hover:border-gray-300 text-gray-600",
                    )}
                    disabled={isLoading}
                  >
                    {method.icon}
                    <span className="font-medium text-sm">{method.label}</span>
                  </button>
                ))}
              </div>
            </div>

            {/* Reference Number */}
            {formData.paymentMethod !== "Cash" && (
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-2">
                  رقم المعاملة <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={formData.referenceNumber}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      referenceNumber: e.target.value,
                    })
                  }
                  className={clsx(
                    "w-full px-4 py-3 border-2 rounded-xl focus:ring-2 focus:ring-orange-500 focus:border-orange-500",
                    errors.referenceNumber
                      ? "border-red-500"
                      : "border-gray-200",
                  )}
                  placeholder="اكتب رقم العملية من فودافون كاش أو الفيزا"
                  disabled={isLoading}
                />
                {errors.referenceNumber && (
                  <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                    <span>⚠️</span> {errors.referenceNumber}
                  </p>
                )}
              </div>
            )}

            {/* Notes */}
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-2">
                ملاحظات
              </label>
              <textarea
                value={formData.notes}
                onChange={(e) =>
                  setFormData({ ...formData, notes: e.target.value })
                }
                className="w-full px-4 py-3 border-2 border-gray-200 rounded-xl focus:ring-2 focus:ring-orange-500 focus:border-orange-500 resize-none"
                rows={3}
                placeholder="ملاحظات إضافية (اختياري)"
                disabled={isLoading}
              />
            </div>

            {/* Remaining Balance Preview */}
            {(() => {
              const numAmount = Number(formData.amount) || 0;
              return (
                numAmount > 0 &&
                numAmount <= customer.totalDue && (
                  <div className="bg-gradient-to-br from-green-50 to-green-100 p-4 rounded-xl border border-green-200">
                    <div className="flex justify-between items-center">
                      <span className="text-sm text-gray-600">
                        المتبقي بعد الدفع
                      </span>
                      <span className="text-2xl font-bold text-green-600">
                        {formatCurrency(customer.totalDue - numAmount)}
                      </span>
                    </div>
                    {customer.totalDue - numAmount === 0 && (
                      <p className="text-xs text-green-600 text-center mt-2">
                        ✅ سيتم تسديد الدين بالكامل
                      </p>
                    )}
                  </div>
                )
              );
            })()}
          </form>

          {/* Footer Actions */}
          <div className="flex gap-3 p-6 border-t bg-gray-50">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 px-6 py-3 border-2 border-gray-300 text-gray-700 font-semibold rounded-xl hover:bg-gray-100 transition-colors"
              disabled={isLoading}
            >
              إلغاء
            </button>
            <button
              type="submit"
              form="debt-payment-form"
              className="flex-1 px-6 py-3 bg-orange-600 text-white font-semibold rounded-xl hover:bg-orange-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors shadow-lg shadow-orange-600/30"
              disabled={isSubmitDisabled}
            >
              {isLoading ? (
                <span className="flex items-center justify-center gap-2">
                  <span className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  جاري التسديد...
                </span>
              ) : isShiftLoading || isShiftFetching ? (
                <span className="flex items-center justify-center gap-2">
                  جارٍ التحقق من الوردية...
                </span>
              ) : !hasOpenShift ? (
                <span className="flex items-center justify-center gap-2">
                  افتح وردية أولاً
                </span>
              ) : (
                <span className="flex items-center justify-center gap-2">
                  <DollarSign className="w-5 h-5" />
                  تسديد {formatCurrency(Number(formData.amount) || 0)}
                </span>
              )}
            </button>
          </div>
        </div>
      </div>
    </Portal>
  );
};
