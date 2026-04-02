import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  useGetPurchaseInvoiceByIdQuery,
  useConfirmPurchaseInvoiceMutation,
  useDeletePurchaseInvoiceMutation,
} from '../../api/purchaseInvoiceApi';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { Loading } from '../../components/common/Loading';
import { formatCurrency, formatDateOnly } from '../../utils/formatters';
import { toast } from 'sonner';
import { AddPaymentModal } from '../../components/purchase-invoices/AddPaymentModal';
import { CancelInvoiceModal } from '../../components/purchase-invoices/CancelInvoiceModal';
import { handleApiError } from '../../utils/errorHandler';

export function PurchaseInvoiceDetailsPage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const [showAddPaymentModal, setShowAddPaymentModal] = useState(false);
  const [showCancelModal, setShowCancelModal] = useState(false);

  const { data: invoiceResponse, isLoading } = useGetPurchaseInvoiceByIdQuery(Number(id));
  const [confirmInvoice, { isLoading: isConfirming }] = useConfirmPurchaseInvoiceMutation();
  const [deleteInvoice] = useDeletePurchaseInvoiceMutation();

  const invoice = invoiceResponse?.data;

  const handleConfirm = async () => {
    if (!confirm('هل أنت متأكد من تأكيد الفاتورة؟ سيتم تحديث المخزون.')) return;

    try {
      await confirmInvoice(Number(id)).unwrap();
      toast.success('تم تأكيد الفاتورة بنجاح');
    } catch (error) {
      console.error('Error confirming invoice:', error);
      toast.error(handleApiError(error));
    }
  };

  const handleDelete = async () => {
    if (!confirm('هل أنت متأكد من حذف الفاتورة؟')) return;

    try {
      await deleteInvoice(Number(id)).unwrap();
      toast.success('تم حذف الفاتورة بنجاح');
      navigate('/purchase-invoices');
    } catch (error) {
      console.error('Error deleting invoice:', error);
      toast.error(handleApiError(error));
    }
  };

  const getStatusBadge = (status: string) => {
    const statusColors: Record<string, string> = {
      Draft: 'bg-gray-100 text-gray-800',
      Confirmed: 'bg-blue-100 text-blue-800',
      Paid: 'bg-green-100 text-green-800',
      PartiallyPaid: 'bg-yellow-100 text-yellow-800',
      Cancelled: 'bg-red-100 text-red-800',
    };

    const statusLabels: Record<string, string> = {
      Draft: 'مسودة',
      Confirmed: 'مؤكدة',
      Paid: 'مدفوعة',
      PartiallyPaid: 'مدفوعة جزئياً',
      Cancelled: 'ملغاة',
    };

    return (
      <span className={`px-3 py-1 rounded-full text-sm font-medium ${statusColors[status] || 'bg-gray-100 text-gray-800'}`}>
        {statusLabels[status] || status}
      </span>
    );
  };

  if (isLoading) return <Loading />;
  if (!invoice) return <div className="p-6">الفاتورة غير موجودة</div>;

  return (
    <div className="p-6 pb-20">
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="text-2xl font-bold">فاتورة شراء {invoice.invoiceNumber}</h1>
          <div className="mt-2">{getStatusBadge(invoice.status)}</div>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => navigate('/purchase-invoices')}>
            رجوع
          </Button>
          {invoice.status === 'Draft' && (
            <>
              <Button variant="outline" onClick={() => navigate(`/purchase-invoices/${id}/edit`)}>
                تعديل
              </Button>
              <Button onClick={handleConfirm} disabled={isConfirming}>
                {isConfirming ? 'جاري التأكيد...' : 'تأكيد الفاتورة'}
              </Button>
            </>
          )}
        </div>
      </div>

      {/* Invoice Info */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
        <Card>
          <h2 className="text-lg font-semibold mb-4">معلومات المورد</h2>
          <div className="space-y-2">
            <div className="flex justify-between">
              <span className="text-gray-600">الاسم:</span>
              <span className="font-medium">{invoice.supplierName}</span>
            </div>
            {invoice.supplierPhone && (
              <div className="flex justify-between">
                <span className="text-gray-600">الهاتف:</span>
                <span className="font-medium">{invoice.supplierPhone}</span>
              </div>
            )}
          </div>
        </Card>

        <Card>
          <h2 className="text-lg font-semibold mb-4">معلومات الفاتورة</h2>
          <div className="space-y-2">
            <div className="flex justify-between">
              <span className="text-gray-600">التاريخ:</span>
              <span className="font-medium">{formatDateOnly(invoice.invoiceDate)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-600">أنشئت بواسطة:</span>
              <span className="font-medium">{invoice.createdByUserName}</span>
            </div>
            {invoice.confirmedByUserName && (
              <div className="flex justify-between">
                <span className="text-gray-600">أكدت بواسطة:</span>
                <span className="font-medium">{invoice.confirmedByUserName}</span>
              </div>
            )}
            {invoice.notes && (
              <div className="flex justify-between">
                <span className="text-gray-600">ملاحظات:</span>
                <span className="font-medium">{invoice.notes}</span>
              </div>
            )}
          </div>
        </Card>
      </div>

      {/* Items */}
      <Card padding="none" className="mb-6">
        <div className="p-4 border-b">
          <h2 className="text-lg font-semibold">المنتجات</h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">المنتج</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">الكمية</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">سعر الشراء</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">الإجمالي</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">ملاحظات</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {invoice.items.map((item) => (
                <tr key={item.id}>
                  <td className="px-4 py-3 text-sm">
                    <div>{item.productName}</div>
                    {item.productSku && (
                      <div className="text-xs text-gray-500">{item.productSku}</div>
                    )}
                  </td>
                  <td className="px-4 py-3 text-sm">{item.quantity}</td>
                  <td className="px-4 py-3 text-sm">{formatCurrency(item.purchasePrice)}</td>
                  <td className="px-4 py-3 text-sm font-medium">{formatCurrency(item.total)}</td>
                  <td className="px-4 py-3 text-sm text-gray-500">{item.notes || '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Totals */}
        <div className="p-4 border-t bg-gray-50">
          <div className="flex justify-end">
            <div className="w-64 space-y-2">
              <div className="flex justify-between">
                <span className="text-sm">المجموع الفرعي:</span>
                <span className="text-sm font-medium">{formatCurrency(invoice.subtotal)}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm">الضريبة ({invoice.taxRate}%):</span>
                <span className="text-sm font-medium">{formatCurrency(invoice.taxAmount)}</span>
              </div>
              <div className="flex justify-between text-lg font-bold border-t pt-2">
                <span>الإجمالي:</span>
                <span>{formatCurrency(invoice.total)}</span>
              </div>
              <div className="flex justify-between text-green-600">
                <span>المدفوع:</span>
                <span className="font-medium">{formatCurrency(invoice.amountPaid)}</span>
              </div>
              <div className="flex justify-between text-red-600">
                <span>المتبقي:</span>
                <span className="font-medium">{formatCurrency(invoice.amountDue)}</span>
              </div>
            </div>
          </div>
        </div>
      </Card>

      {/* Payments */}
      <Card padding="none" className="mb-6">
        <div className="p-4 border-b flex justify-between items-center">
          <h2 className="text-lg font-semibold">الدفعات</h2>
          {invoice.amountDue > 0 && invoice.status !== 'Cancelled' && invoice.status !== 'Draft' && (
            <Button onClick={() => setShowAddPaymentModal(true)}>إضافة دفعة</Button>
          )}
        </div>

        {invoice.payments.length === 0 ? (
          <div className="text-center py-8 text-gray-500">لا توجد دفعات</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">التاريخ</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">المبلغ</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">الطريقة</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">رقم المرجع</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">بواسطة</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {invoice.payments.map((payment) => (
                  <tr key={payment.id}>
                    <td className="px-4 py-3 text-sm">{formatDateOnly(payment.paymentDate)}</td>
                    <td className="px-4 py-3 text-sm font-medium">{formatCurrency(payment.amount)}</td>
                    <td className="px-4 py-3 text-sm">{payment.method}</td>
                    <td className="px-4 py-3 text-sm">{payment.referenceNumber || '-'}</td>
                    <td className="px-4 py-3 text-sm">{payment.createdByUserName}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* Actions */}
      <div className="flex justify-end gap-4 mb-6">
        {invoice.status === 'Draft' && (
          <Button variant="outline" onClick={handleDelete} className="text-red-600 border-red-600 hover:bg-red-50">
            حذف الفاتورة
          </Button>
        )}
        {(invoice.status === 'Confirmed' || invoice.status === 'PartiallyPaid') && (
          <Button variant="outline" onClick={() => setShowCancelModal(true)} className="text-red-600 border-red-600 hover:bg-red-50">
            إلغاء الفاتورة
          </Button>
        )}
      </div>

      {/* Modals */}
      {showAddPaymentModal && (
        <AddPaymentModal
          invoiceId={Number(id)}
          amountDue={invoice.amountDue}
          onClose={() => setShowAddPaymentModal(false)}
        />
      )}

      {showCancelModal && (
        <CancelInvoiceModal
          invoiceId={Number(id)}
          isConfirmed={invoice.status === 'Confirmed' || invoice.status === 'PartiallyPaid'}
          onClose={() => setShowCancelModal(false)}
        />
      )}
    </div>
  );
}
