import { useState } from "react";
import { X, AlertTriangle, Clock, User, DollarSign } from "lucide-react";
import { useForceCloseShiftMutation } from "../../api/shiftsApi";
import { Shift } from "../../types/shift.types";
import { formatDateTimeFull } from "../../utils/formatters";
import { Portal } from "../../components/common/Portal";
import { handleApiError } from "../../utils/errorHandler";

interface ForceCloseShiftModalProps {
  shift: Shift;
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

export default function ForceCloseShiftModal({
  shift,
  isOpen,
  onClose,
  onSuccess,
}: ForceCloseShiftModalProps) {
  const [reason, setReason] = useState("");
  const [actualBalance, setActualBalance] = useState<string>("");
  const [notes, setNotes] = useState("");
  const [forceCloseShift, { isLoading }] = useForceCloseShiftMutation();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!reason.trim()) {
      alert("يرجى إدخال سبب الإغلاق");
      return;
    }

    try {
      await forceCloseShift({
        id: shift.id,
        request: {
          reason: reason.trim(),
          actualBalance: actualBalance ? parseFloat(actualBalance) : undefined,
          notes: notes.trim() || undefined,
        },
      }).unwrap();

      alert("تم إغلاق الوردية بالقوة بنجاح");
      onSuccess?.();
      onClose();
    } catch (error) {
      alert(handleApiError(error));
    }
  };

  if (!isOpen) return null;

  return (
    <Portal>
      <div 
        className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/50"
        onClick={onClose}
      >
        <div 
          className="bg-white rounded-2xl shadow-2xl w-full max-w-md max-h-[90vh] flex flex-col overflow-hidden"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b border-gray-200 flex-shrink-0">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-red-100 rounded-xl flex items-center justify-center">
                <AlertTriangle className="w-5 h-5 text-red-600" />
              </div>
              <h2 className="text-xl font-bold text-gray-800">
                إغلاق الوردية بالقوة
              </h2>
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
              disabled={isLoading}
            >
              <X className="w-5 h-5 text-gray-500" />
            </button>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto flex-1">
            {/* Shift Info */}
            <div className="bg-gradient-to-br from-gray-50 to-gray-100 p-4 rounded-xl border border-gray-200 space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-gray-600">
                  <User className="w-4 h-4" />
                  <span className="text-sm">الكاشير:</span>
                </div>
                <span className="font-medium text-gray-800">{shift.userName}</span>
              </div>
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-gray-600">
                  <Clock className="w-4 h-4" />
                  <span className="text-sm">وقت الفتح:</span>
                </div>
                <span className="font-medium text-gray-800">
                  {formatDateTimeFull(shift.openedAt)}
                </span>
              </div>
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-gray-600">
                  <Clock className="w-4 h-4" />
                  <span className="text-sm">المدة:</span>
                </div>
                <span className="font-medium text-gray-800">
                  {shift.durationHours} ساعة و {shift.durationMinutes} دقيقة
                </span>
              </div>
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-gray-600">
                  <DollarSign className="w-4 h-4" />
                  <span className="text-sm">الرصيد المتوقع:</span>
                </div>
                <span className="font-medium text-gray-800">
                  {shift.expectedBalance.toFixed(2)} ج.م
                </span>
              </div>
            </div>

            {/* Reason */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                سبب الإغلاق بالقوة <span className="text-red-500">*</span>
              </label>
              <textarea
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-red-500 focus:border-red-500 resize-none"
                rows={3}
                placeholder="مثال: الكاشير نسي إغلاق الوردية، مشكلة تقنية، طوارئ..."
                required
                maxLength={500}
              />
              <div className="text-xs text-gray-500 mt-1">
                {reason.length}/500 حرف
              </div>
            </div>

            {/* Actual Balance */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                الرصيد الفعلي (اختياري)
              </label>
              <div className="relative">
                <DollarSign className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <input
                  type="number"
                  step="0.01"
                  value={actualBalance === "0" ? "" : actualBalance}
                  onChange={(e) => setActualBalance(e.target.value)}
                  className="w-full pr-10 pl-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-red-500 focus:border-red-500"
                  placeholder="0.00"
                />
              </div>
              <p className="mt-1 text-xs text-gray-500">
                إذا لم يتم إدخاله، سيتم استخدام الرصيد المتوقع
              </p>
            </div>

            {/* Notes */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                ملاحظات إضافية (اختياري)
              </label>
              <textarea
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-red-500 focus:border-red-500 resize-none"
                rows={2}
                placeholder="أي ملاحظات إضافية..."
                maxLength={1000}
              />
            </div>

            {/* Warning */}
            <div className="bg-yellow-50 border border-yellow-200 rounded-xl p-4">
              <div className="flex gap-3">
                <AlertTriangle className="w-5 h-5 text-yellow-600 flex-shrink-0 mt-0.5" />
                <div>
                  <p className="text-sm font-medium text-yellow-800 mb-1">
                    تحذير هام
                  </p>
                  <p className="text-sm text-yellow-700">
                    هذا الإجراء سيغلق الوردية فوراً ولا يمكن التراجع عنه. سيتم تسجيل هذه العملية في سجل التدقيق.
                  </p>
                </div>
              </div>
            </div>

            {/* Actions */}
            <div className="flex gap-3 pt-2">
              <button
                type="button"
                onClick={onClose}
                className="flex-1 px-4 py-2.5 border border-gray-300 text-gray-700 rounded-xl hover:bg-gray-50 transition-colors font-medium"
                disabled={isLoading}
              >
                إلغاء
              </button>
              <button
                type="submit"
                className="flex-1 px-4 py-2.5 bg-red-600 text-white rounded-xl hover:bg-red-700 transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                disabled={isLoading}
              >
                {isLoading ? "جاري الإغلاق..." : "إغلاق بالقوة"}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Portal>
  );
}
