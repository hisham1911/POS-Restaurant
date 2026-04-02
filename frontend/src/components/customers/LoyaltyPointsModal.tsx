import { useState } from "react";
import { X, Plus, Minus, AlertCircle } from "lucide-react";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import {
  useAddLoyaltyPointsMutation,
  useRedeemLoyaltyPointsMutation,
} from "@/api/customersApi";
import { toast } from "react-hot-toast";
import { Portal } from "@/components/common/Portal";
import { handleApiError } from "@/utils/errorHandler";

interface LoyaltyPointsModalProps {
  customerId: number;
  customerName: string;
  currentPoints: number;
  mode: "add" | "redeem";
  onClose: () => void;
  onSuccess: () => void;
}

export const LoyaltyPointsModal = ({
  customerId,
  customerName,
  currentPoints,
  mode,
  onClose,
  onSuccess,
}: LoyaltyPointsModalProps) => {
  const [points, setPoints] = useState<string>("");
  const [addLoyaltyPoints, { isLoading: isAdding }] =
    useAddLoyaltyPointsMutation();
  const [redeemLoyaltyPoints, { isLoading: isRedeeming }] =
    useRedeemLoyaltyPointsMutation();

  const isLoading = isAdding || isRedeeming;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const pointsValue = parseInt(points);

    // Validation
    if (!points || isNaN(pointsValue) || pointsValue <= 0) {
      toast.error("يرجى إدخال عدد نقاط صحيح");
      return;
    }

    if (mode === "redeem" && pointsValue > currentPoints) {
      toast.error("عدد النقاط المطلوب أكبر من الرصيد المتاح");
      return;
    }

    try {
      if (mode === "add") {
        await addLoyaltyPoints({
          customerId,
          points: pointsValue,
        }).unwrap();

        toast.success(`تم إضافة ${pointsValue} نقطة بنجاح`);
        onSuccess();
        onClose();
      } else {
        await redeemLoyaltyPoints({
          customerId,
          points: pointsValue,
        }).unwrap();

        toast.success(`تم استبدال ${pointsValue} نقطة بنجاح`);
        onSuccess();
        onClose();
      }
    } catch (error) {
      toast.error(handleApiError(error));
    }
  };

  const isAddMode = mode === "add";
  const title = isAddMode ? "إضافة نقاط ولاء" : "استبدال نقاط ولاء";
  const icon = isAddMode ? (
    <Plus className="w-5 h-5" />
  ) : (
    <Minus className="w-5 h-5" />
  );
  const iconBgColor = isAddMode ? "bg-green-100" : "bg-orange-100";
  const iconColor = isAddMode ? "text-green-600" : "text-orange-600";
  const buttonColor = isAddMode ? "primary" : "secondary";

  return (
    <Portal>
      {/* Backdrop */}
      <div className="fixed inset-0 bg-black/50 z-[100]" onClick={onClose} />

      {/* Modal */}
      <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
        <div
          className="bg-white rounded-2xl shadow-xl w-full max-w-md"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b">
            <div className="flex items-center gap-3">
              <div
                className={`w-10 h-10 ${iconBgColor} rounded-full flex items-center justify-center`}
              >
                <span className={iconColor}>{icon}</span>
              </div>
              <h2 className="text-lg font-bold text-gray-800">{title}</h2>
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            >
              <X className="w-5 h-5 text-gray-500" />
            </button>
          </div>

          {/* Content */}
          <form onSubmit={handleSubmit} className="p-6 space-y-4">
            {/* Customer Info */}
            <div className="bg-gray-50 rounded-lg p-4">
              <p className="text-sm text-gray-500 mb-1">العميل</p>
              <p className="font-semibold text-gray-800">
                {customerName || "عميل بدون اسم"}
              </p>
              <p className="text-sm text-gray-600 mt-2">
                الرصيد الحالي:{" "}
                <span className="font-bold text-yellow-600">
                  {currentPoints} نقطة
                </span>
              </p>
            </div>

            {/* Points Input */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                عدد النقاط
              </label>
              <Input
                type="number"
                value={points === "0" ? "" : points}
                onChange={(e) => setPoints(e.target.value)}
                placeholder="أدخل عدد النقاط"
                min="1"
                max={mode === "redeem" ? currentPoints : undefined}
                required
                autoFocus
              />
            </div>

            {/* Warning for Redeem */}
            {mode === "redeem" && currentPoints === 0 && (
              <div className="flex items-start gap-2 bg-orange-50 border border-orange-200 rounded-lg p-3">
                <AlertCircle className="w-5 h-5 text-orange-600 flex-shrink-0 mt-0.5" />
                <p className="text-sm text-orange-800">
                  لا يوجد رصيد كافٍ من النقاط للاستبدال
                </p>
              </div>
            )}

            {/* Actions */}
            <div className="flex gap-3 pt-2">
              <Button
                type="button"
                variant="outline"
                onClick={onClose}
                disabled={isLoading}
                className="flex-1"
              >
                إلغاء
              </Button>
              <Button
                type="submit"
                variant={buttonColor}
                disabled={
                  isLoading || (mode === "redeem" && currentPoints === 0)
                }
                className="flex-1"
              >
                {isLoading
                  ? "جاري المعالجة..."
                  : isAddMode
                    ? "إضافة"
                    : "استبدال"}
              </Button>
            </div>
          </form>
        </div>
      </div>
    </Portal>
  );
};
