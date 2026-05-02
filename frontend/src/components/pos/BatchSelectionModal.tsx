import { X } from "lucide-react";
import { ProductBatch } from "@/types/productBatch.types";
import { formatCurrency } from "@/utils/formatters";
import clsx from "clsx";

interface BatchSelectionModalProps {
  isOpen: boolean;
  onClose: () => void;
  productName: string;
  batches: ProductBatch[];
  selectedBatchId?: number;
  onSelectBatch: (batch: ProductBatch) => void;
}

export const BatchSelectionModal = ({
  isOpen,
  onClose,
  productName,
  batches,
  selectedBatchId,
  onSelectBatch,
}: BatchSelectionModalProps) => {
  if (!isOpen) return null;

  const formatDate = (dateString?: string) => {
    if (!dateString) return "بدون تاريخ انتهاء";

    const date = new Date(dateString);
    return date.toLocaleDateString("ar-EG", {
      year: "numeric",
      month: "long",
      day: "numeric",
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/50"
        onClick={onClose}
      />

      {/* Modal */}
      <div className="relative z-10 w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl">
        {/* Header */}
        <div className="mb-4 flex items-start justify-between">
          <div>
            <h3 className="text-lg font-bold text-gray-900">اختر الدفعة</h3>
            <p className="mt-1 text-sm text-gray-600">{productName}</p>
          </div>
          <button
            onClick={onClose}
            className="rounded-lg p-2 text-gray-400 transition-colors hover:bg-gray-100 hover:text-gray-600"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Batches List */}
        <div className="space-y-2 max-h-[60vh] overflow-y-auto">
          {batches.length === 0 ? (
            <div className="py-8 text-center text-gray-500">
              لا توجد دفعات متاحة
            </div>
          ) : (
            batches.map((batch) => {
              const isSelected = selectedBatchId === batch.id;
              
              return (
                <button
                  key={batch.id}
                  onClick={() => {
                    onSelectBatch(batch);
                    onClose();
                  }}
                  className={clsx(
                    "w-full rounded-xl border-2 p-4 text-start transition-all",
                    "hover:bg-gray-50",
                    isSelected
                      ? "border-primary-600 bg-primary-100 ring-2 ring-primary-200"
                      : batch.isRecommended
                        ? "border-primary-400 bg-primary-50"
                        : "border-gray-200 bg-white",
                  )}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1">
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-gray-900">
                          {batch.batchNumber || "بدون رقم دفعة"}
                        </span>
                        {isSelected && (
                          <span className="rounded-md bg-primary-600 px-2 py-0.5 text-xs font-bold text-white">
                            محدد حالياً
                          </span>
                        )}
                        {!isSelected && batch.isRecommended && (
                          <span className="rounded-md bg-primary-100 px-2 py-0.5 text-xs font-bold text-primary-700">
                            مقترح (الأقدم)
                          </span>
                        )}
                      </div>
                      <div className="mt-1 text-sm text-gray-600">
                        ينتهي: {formatDate(batch.expiryDate)}
                        {typeof batch.daysUntilExpiry === "number" && batch.daysUntilExpiry < 30 && (
                          <span className="ms-2 text-warning-600">
                            (باقي {batch.daysUntilExpiry} يوم)
                          </span>
                        )}
                      </div>
                    </div>

                    <div className="text-end">
                      {batch.sellingPrice && (
                        <div className="text-lg font-bold text-gray-900">
                          {formatCurrency(batch.sellingPrice)}
                        </div>
                      )}
                      <div className="text-sm text-gray-600">
                        متاح: {batch.quantity}
                      </div>
                    </div>
                  </div>
                </button>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
};
