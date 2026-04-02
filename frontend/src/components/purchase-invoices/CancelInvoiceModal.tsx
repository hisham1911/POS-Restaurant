import { useState } from 'react';
import { useCancelPurchaseInvoiceMutation } from '../../api/purchaseInvoiceApi';
import { Modal } from '../common/Modal';
import { Button } from '../common/Button';
import { toast } from 'sonner';
import { handleApiError } from '../../utils/errorHandler';

interface CancelInvoiceModalProps {
  invoiceId: number;
  isConfirmed: boolean;
  onClose: () => void;
}

export function CancelInvoiceModal({ invoiceId, isConfirmed, onClose }: CancelInvoiceModalProps) {
  const [reason, setReason] = useState<string>('');
  const [adjustInventory, setAdjustInventory] = useState<boolean>(true);

  const [cancelInvoice, { isLoading }] = useCancelPurchaseInvoiceMutation();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!reason.trim()) {
      toast.error('يرجى إدخال سبب الإلغاء');
      return;
    }

    try {
      await cancelInvoice({
        id: invoiceId,
        data: {
          reason: reason.trim(),
          adjustInventory: isConfirmed ? adjustInventory : false,
        },
      }).unwrap();

      toast.success('تم إلغاء الفاتورة بنجاح');
      onClose();
    } catch (error) {
      console.error('Error cancelling invoice:', error);
      toast.error(handleApiError(error));
    }
  };

  return (
    <Modal isOpen onClose={onClose} title="إلغاء الفاتورة">
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm font-medium mb-1">
            سبب الإلغاء <span className="text-red-500">*</span>
          </label>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            className="w-full px-3 py-2 border rounded-lg"
            rows={4}
            placeholder="يرجى إدخال سبب إلغاء الفاتورة"
            required
          />
        </div>

        {isConfirmed && (
          <div className="flex items-start gap-2">
            <input
              type="checkbox"
              id="adjustInventory"
              checked={adjustInventory}
              onChange={(e) => setAdjustInventory(e.target.checked)}
              className="mt-1"
            />
            <label htmlFor="adjustInventory" className="text-sm">
              <div className="font-medium">تعديل المخزون</div>
              <div className="text-gray-500">
                سيتم خصم الكميات من المخزون عند إلغاء الفاتورة المؤكدة
              </div>
            </label>
          </div>
        )}

        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-3">
          <p className="text-sm text-yellow-800">
            <strong>تحذير:</strong> لا يمكن التراجع عن إلغاء الفاتورة
          </p>
        </div>

        <div className="flex justify-end gap-2 pt-4">
          <Button type="button" variant="outline" onClick={onClose}>
            إلغاء
          </Button>
          <Button type="submit" disabled={isLoading} className="bg-red-600 hover:bg-red-700">
            {isLoading ? 'جاري الإلغاء...' : 'إلغاء الفاتورة'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
