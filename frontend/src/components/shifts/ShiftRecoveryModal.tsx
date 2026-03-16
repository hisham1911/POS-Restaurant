import { Shift } from "../../types/shift.types";
import { formatDateTimeFull } from "../../utils/formatters";
import { Portal } from "../../components/common/Portal";

interface ShiftRecoveryModalProps {
  shift: Shift;
  savedAt: string;
  isOpen: boolean;
  onRestore: () => void;
  onDiscard: () => void;
}

export default function ShiftRecoveryModal({
  shift,
  savedAt,
  isOpen,
  onRestore,
  onDiscard,
}: ShiftRecoveryModalProps) {
  if (!isOpen) return null;

  const timeSinceLastSave = Math.floor(
    (new Date().getTime() - new Date(savedAt).getTime()) / 1000 / 60,
  );

  return (
    <Portal>
      <div 
        className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/50"
        onClick={onDiscard}
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
              <h2 className="text-xl font-bold text-gray-800">استعادة الوردية</h2>
            </div>
          </div>

          <div className="p-6 space-y-4 overflow-y-auto flex-1">
            <p className="text-gray-700">
              تم العثور على وردية مفتوحة من جلسة سابقة. هل تريد استعادتها؟
            </p>

            {/* Shift Info */}
            <div className="bg-gradient-to-br from-blue-50 to-blue-100 p-4 rounded-xl border border-blue-200 space-y-2">
              <div className="flex justify-between">
                <span className="text-sm text-gray-600">رقم الوردية:</span>
                <span className="font-medium text-gray-800">#{shift.id}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-gray-600">وقت الفتح:</span>
                <span className="font-medium text-gray-800">
                  {formatDateTimeFull(shift.openedAt)}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-gray-600">الرصيد المتوقع:</span>
                <span className="font-medium text-gray-800">
                  {shift.expectedBalance.toFixed(2)} ج.م
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-gray-600">عدد الطلبات:</span>
                <span className="font-medium text-gray-800">{shift.totalOrders}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-gray-600">آخر حفظ:</span>
                <span className="font-medium text-gray-800">منذ {timeSinceLastSave} دقيقة</span>
              </div>
            </div>

            {/* Info */}
            <div className="bg-yellow-50 border border-yellow-200 rounded-xl p-4">
              <p className="text-sm text-yellow-800">
                💡 <strong>ملاحظة:</strong> إذا اخترت "تجاهل"، سيتم حذف البيانات
                المحفوظة ولن يمكن استعادتها.
              </p>
            </div>
          </div>

          {/* Action Buttons */}
          <div className="flex flex-col gap-2 p-6 border-t border-gray-200 flex-shrink-0">
            <button
              onClick={onRestore}
              className="w-full px-4 py-3 bg-blue-600 text-white rounded-xl hover:bg-blue-700 font-medium transition-colors"
            >
              ✓ استعادة الوردية
            </button>

            <button
              onClick={onDiscard}
              className="w-full px-4 py-2.5 border border-red-300 text-red-600 rounded-xl hover:bg-red-50 transition-colors"
            >
              تجاهل وبدء جديد
            </button>
          </div>
        </div>
      </div>
    </Portal>
  );
}
