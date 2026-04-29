import { useState } from "react";
import { toast } from "sonner";
import { useReconcileMutation } from "@/api/cashRegisterApi";
import { Button } from "@/components/common/Button";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  branchId: number;
  shiftId: number;
  expectedAmount: number;
}

export const ReconcileModal = ({
  isOpen,
  onClose,
  branchId,
  shiftId,
  expectedAmount,
}: Props) => {
  const [actualAmount, setActualAmount] = useState<string>("");
  const [notes, setNotes] = useState("");
  const [done, setDone] = useState(false);
  const [reconcile, { isLoading }] = useReconcileMutation();

  const actual = parseFloat(actualAmount) || 0;
  const difference = actual - expectedAmount;

  const handleReconcile = async () => {
    if (!actualAmount) {
      toast.error("أدخل المبلغ الفعلي في الدرج");
      return;
    }

    try {
      await reconcile({
        branchId,
        shiftId,
        actualCashAmount: actual,
        notes: notes || undefined,
      }).unwrap();
      setDone(true);
      toast.success("تم مطابقة الخزينة وإغلاق الشيفت");
    } catch (err) {
      const error = err as { data: { errorCode: string; message: string } };
      switch (error.data?.errorCode) {
        case "NO_OPEN_SHIFT":
          toast.error("لا يوجد شيفت مفتوح");
          break;
        default:
          toast.error(error.data?.message ?? "حدث خطأ");
      }
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
        <h2 className="text-lg font-bold text-gray-800 mb-6">
          مطابقة الخزينة وإغلاق الشيفت
        </h2>

        {!done ? (
          <div className="flex flex-col gap-4">
            <div className="bg-gray-50 rounded-xl p-4">
              <p className="text-sm text-gray-500 mb-1">الرصيد المتوقع (حسابي)</p>
              <p className="text-2xl font-bold text-gray-800">
                {expectedAmount.toLocaleString("ar-EG", {
                  minimumFractionDigits: 2,
                })}{" "}
                ج.م
              </p>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                المبلغ الفعلي في الدرج
              </label>
              <input
                type="number"
                step="0.01"
                min="0"
                value={actualAmount}
                onChange={(e) => setActualAmount(e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-lg font-semibold"
                placeholder="0.00"
                autoFocus
              />
            </div>

            {actualAmount && (
              <div
                className={`rounded-xl p-3 text-sm font-medium ${
                  difference === 0
                    ? "bg-success-50 text-success-700"
                    : difference > 0
                    ? "bg-warning-50 text-warning-700"
                    : "bg-danger-50 text-danger-700"
                }`}
              >
                {difference === 0 && "لا يوجد فرق — الخزينة متطابقة"}
                {difference > 0 && `زيادة: +${difference.toFixed(2)} ج.م`}
                {difference < 0 && `عجز: ${difference.toFixed(2)} ج.م`}
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                ملاحظة (اختياري)
              </label>
              <textarea
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm resize-none"
                rows={2}
                placeholder="سبب الفرق إن وجد..."
              />
            </div>

            <div className="flex gap-3">
              <Button
                variant="outline"
                className="flex-1"
                onClick={onClose}
              >
                إلغاء
              </Button>
              <Button
                variant="primary"
                className="flex-1"
                onClick={handleReconcile}
                disabled={isLoading || !actualAmount}
              >
                {isLoading ? "جاري الإغلاق..." : "تأكيد وإغلاق الشيفت"}
              </Button>
            </div>
          </div>
        ) : (
          <div className="flex flex-col gap-4 text-center">
            <div className="text-4xl">
              {difference === 0 ? "✅" : difference > 0 ? "⚠️" : "❌"}
            </div>
            <p className="font-bold text-gray-800">تم إغلاق الشيفت</p>
            <div className="bg-gray-50 rounded-xl p-4 text-start">
              <div className="flex justify-between text-sm mb-2">
                <span className="text-gray-500">المتوقع</span>
                <span className="font-medium">
                  {expectedAmount.toFixed(2)} ج.م
                </span>
              </div>
              <div className="flex justify-between text-sm mb-2">
                <span className="text-gray-500">الفعلي</span>
                <span className="font-medium">{actual.toFixed(2)} ج.م</span>
              </div>
              <div
                className={`flex justify-between text-sm font-bold ${
                  difference === 0
                    ? "text-success-700"
                    : difference > 0
                    ? "text-warning-700"
                    : "text-danger-700"
                }`}
              >
                <span>الفرق</span>
                <span>
                  {difference >= 0 ? "+" : ""}
                  {difference.toFixed(2)} ج.م
                </span>
              </div>
            </div>
            <Button variant="primary" className="w-full" onClick={onClose}>
              إغلاق
            </Button>
          </div>
        )}
      </div>
    </div>
  );
};
