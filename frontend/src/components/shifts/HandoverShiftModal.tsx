import { useState, useEffect } from "react";
import { ChevronDown } from "lucide-react";
import { useHandoverShiftMutation } from "../../api/shiftsApi";
import { Shift } from "../../types/shift.types";
import { formatDateTimeFull } from "../../utils/formatters";
import { Portal } from "../../components/common/Portal";
import { handleApiError } from "../../utils/errorHandler";

interface HandoverShiftModalProps {
  shift: Shift;
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: () => void;
  availableUsers?: Array<{ id: number; name: string; email: string }>;
}

export default function HandoverShiftModal({
  shift,
  isOpen,
  onClose,
  onSuccess,
  availableUsers = [],
}: HandoverShiftModalProps) {
  const [toUserId, setToUserId] = useState<number | "">("");
  const [currentBalance, setCurrentBalance] = useState<string>(
    shift.expectedBalance.toString(),
  );
  const [notes, setNotes] = useState("");
  const [handoverShift, { isLoading }] = useHandoverShiftMutation();

  useEffect(() => {
    if (isOpen) {
      setCurrentBalance(shift.expectedBalance.toString());
      setToUserId("");
      setNotes("");
    }
  }, [isOpen, shift.expectedBalance]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!toUserId) {
      alert("يرجى اختيار المستخدم المستلم");
      return;
    }

    if (!currentBalance || parseFloat(currentBalance) < 0) {
      alert("يرجى إدخال الرصيد الحالي");
      return;
    }

    try {
      await handoverShift({
        id: shift.id,
        request: {
          toUserId: Number(toUserId),
          currentBalance: parseFloat(currentBalance),
          notes: notes.trim() || undefined,
        },
      }).unwrap();

      alert("تم تسليم الوردية بنجاح");
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
              <div className="w-10 h-10 bg-blue-100 rounded-xl flex items-center justify-center">
                <span className="text-2xl">🔄</span>
              </div>
              <h2 className="text-xl font-bold text-gray-800">تسليم الوردية</h2>
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
              disabled={isLoading}
            >
              <span className="text-gray-500 text-xl">×</span>
            </button>
          </div>

          <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto flex-1">
            {/* Current Shift Info */}
            <div className="bg-gradient-to-br from-blue-50 to-blue-100 p-4 rounded-xl border border-blue-200 space-y-2">
              <div className="flex justify-between">
                <span className="text-sm text-gray-600">الكاشير الحالي:</span>
                <span className="font-medium text-gray-800">{shift.userName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-gray-600">وقت الفتح:</span>
                <span className="font-medium text-gray-800">
                  {formatDateTimeFull(shift.openedAt)}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-gray-600">المدة:</span>
                <span className="font-medium text-gray-800">
                  {shift.durationHours} ساعة و {shift.durationMinutes} دقيقة
                </span>
              </div>
            </div>

            {/* Target User */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                تسليم إلى <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <select
                  value={toUserId}
                  onChange={(e) =>
                    setToUserId(e.target.value ? Number(e.target.value) : "")
                  }
                  className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
                  required
                >
                  <option value="">-- اختر المستخدم --</option>
                  {availableUsers.map((user) => (
                    <option key={user.id} value={user.id}>
                      {user.name} ({user.email})
                    </option>
                  ))}
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
              <p className="text-xs text-gray-500 mt-1">
                سيتم نقل الوردية للمستخدم المحدد
              </p>
            </div>

            {/* Current Balance */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                الرصيد الحالي <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                step="0.01"
                value={currentBalance === "0" ? "" : currentBalance}
                onChange={(e) => setCurrentBalance(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                placeholder="الرصيد في الدرج"
                required
              />
              <p className="text-xs text-gray-500 mt-1">
                الرصيد المتوقع: {shift.expectedBalance.toFixed(2)} ج.م
              </p>
            </div>

            {/* Notes */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                ملاحظات التسليم (اختياري)
              </label>
              <textarea
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-blue-500 resize-none"
                rows={3}
                placeholder="أي ملاحظات للمستخدم المستلم..."
                maxLength={500}
              />
              <p className="text-xs text-gray-500 mt-1">
                {notes.length}/500 حرف
              </p>
            </div>

            {/* Info */}
            <div className="bg-blue-50 border border-blue-200 rounded-xl p-4">
              <p className="text-sm text-blue-800">
                ℹ️ سيتم نقل الوردية للمستخدم المحدد وسيستمر بنفس الرقم. سيتم
                تسجيل عملية التسليم في سجل التدقيق.
              </p>
            </div>
          </form>

          {/* Buttons */}
          <div className="flex gap-3 p-6 border-t border-gray-200 flex-shrink-0">
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
              onClick={handleSubmit}
              className="flex-1 px-4 py-2.5 bg-blue-600 text-white rounded-xl hover:bg-blue-700 transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed"
              disabled={isLoading}
            >
              {isLoading ? "جاري التسليم..." : "تسليم الوردية"}
            </button>
          </div>
        </div>
      </div>
    </Portal>
  );
}
