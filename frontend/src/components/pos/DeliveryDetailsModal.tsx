import { FormEvent } from "react";
import { Banknote, MapPin, StickyNote, Truck } from "lucide-react";
import { Modal } from "@/components/common/Modal";
import { Button } from "@/components/common/Button";
import { formatCurrency } from "@/utils/formatters";

interface DeliveryDetailsModalProps {
  isOpen: boolean;
  onClose: () => void;
  deliveryAddress: string;
  onDeliveryAddressChange: (value: string) => void;
  deliveryFee: string;
  onDeliveryFeeChange: (value: string) => void;
  deliveryNotes: string;
  onDeliveryNotesChange: (value: string) => void;
  orderTotal: number;
}

export const DeliveryDetailsModal = ({
  isOpen,
  onClose,
  deliveryAddress,
  onDeliveryAddressChange,
  deliveryFee,
  onDeliveryFeeChange,
  deliveryNotes,
  onDeliveryNotesChange,
  orderTotal,
}: DeliveryDetailsModalProps) => {
  const parsedDeliveryFee = Number.parseFloat(deliveryFee || "0") || 0;
  const checkoutTotal = orderTotal + parsedDeliveryFee;

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onClose();
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="بيانات التوصيل" size="lg">
      <form onSubmit={handleSubmit} className="space-y-5">
        <div className="rounded-xl border border-primary-100 bg-primary-50 p-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-white text-primary-600 shadow-sm">
              <Truck className="h-5 w-5" strokeWidth={2} />
            </div>
            <div>
              <p className="text-sm font-bold text-gray-900">طلب توصيل</p>
              <p className="text-xs text-gray-600">
                أدخل بيانات العميل والرسوم قبل الدفع.
              </p>
            </div>
          </div>
        </div>

        <label className="block">
          <span className="mb-2 flex items-center gap-2 text-sm font-semibold text-gray-700">
            <MapPin className="h-4 w-4 text-primary-500" strokeWidth={2} />
            عنوان التوصيل
          </span>
          <input
            type="text"
            value={deliveryAddress}
            onChange={(event) => onDeliveryAddressChange(event.target.value)}
            placeholder="اكتب العنوان أو أقرب علامة مميزة"
            className="w-full rounded-lg border border-gray-200 px-3 py-2.5 text-sm font-medium text-gray-700 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-100"
            autoFocus
          />
        </label>

        <label className="block">
          <span className="mb-2 flex items-center gap-2 text-sm font-semibold text-gray-700">
            <Banknote className="h-4 w-4 text-primary-500" strokeWidth={2} />
            رسوم التوصيل
          </span>
          <input
            type="number"
            min="0"
            step="0.01"
            value={deliveryFee}
            onChange={(event) => onDeliveryFeeChange(event.target.value)}
            placeholder="0.00"
            className="w-full rounded-lg border border-gray-200 px-3 py-2.5 text-sm font-medium text-gray-700 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-100"
          />
        </label>

        <label className="block">
          <span className="mb-2 flex items-center gap-2 text-sm font-semibold text-gray-700">
            <StickyNote className="h-4 w-4 text-primary-500" strokeWidth={2} />
            ملاحظات التوصيل
          </span>
          <textarea
            value={deliveryNotes}
            onChange={(event) => onDeliveryNotesChange(event.target.value)}
            placeholder="اختياري"
            rows={3}
            className="w-full resize-none rounded-lg border border-gray-200 px-3 py-2.5 text-sm font-medium text-gray-700 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-100"
          />
        </label>

        <div className="rounded-xl border border-gray-200 bg-gray-50 p-4">
          <div className="flex items-center justify-between text-sm text-gray-600">
            <span>إجمالي الطلب</span>
            <span className="font-semibold text-gray-900">
              {formatCurrency(orderTotal)}
            </span>
          </div>
          <div className="mt-2 flex items-center justify-between text-sm text-gray-600">
            <span>رسوم التوصيل</span>
            <span className="font-semibold text-gray-900">
              {formatCurrency(parsedDeliveryFee)}
            </span>
          </div>
          <div className="mt-3 flex items-center justify-between border-t border-gray-200 pt-3 font-bold text-primary-700">
            <span>الإجمالي بعد التوصيل</span>
            <span>{formatCurrency(checkoutTotal)}</span>
          </div>
        </div>

        <div className="flex items-center justify-end gap-2 pt-1">
          <Button type="button" variant="ghost" onClick={onClose}>
            إلغاء
          </Button>
          <Button type="submit">حفظ</Button>
        </div>
      </form>
    </Modal>
  );
};
