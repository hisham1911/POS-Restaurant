import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { ClipboardList, Building2, ChevronDown } from "lucide-react";
import {
  useGetPurchaseInvoicesQuery,
  useDeletePurchaseInvoiceMutation,
} from "../../api/purchaseInvoiceApi";
import { useGetSuppliersQuery } from "../../api/suppliersApi";
import { Button } from "../../components/common/Button";
import { Card } from "../../components/common/Card";
import { Loading } from "../../components/common/Loading";
import { formatCurrency, formatDateOnly } from "../../utils/formatters";
import { PurchaseInvoiceStatus } from "../../types/purchaseInvoice.types";
import { toast } from "sonner";
import { handleApiError } from "../../utils/errorHandler";

export function PurchaseInvoicesPage() {
  const navigate = useNavigate();
  const [pageNumber, setPageNumber] = useState(1);
  const [supplierId, setSupplierId] = useState<number | undefined>();
  const [status, setStatus] = useState<PurchaseInvoiceStatus | undefined>();
  const [fromDate, setFromDate] = useState<string>("");
  const [toDate, setToDate] = useState<string>("");

  const { data: invoicesResponse, isLoading } = useGetPurchaseInvoicesQuery({
    supplierId,
    status,
    fromDate: fromDate || undefined,
    toDate: toDate || undefined,
    pageNumber,
    pageSize: 20,
  });

  const { data: suppliersResponse } = useGetSuppliersQuery();
  const [deletePurchaseInvoice] = useDeletePurchaseInvoiceMutation();

  const invoices = invoicesResponse?.data?.items || [];
  const totalPages = invoicesResponse?.data?.totalPages || 1;
  const suppliers = suppliersResponse?.data || [];

  const handleDelete = async (id: number, invoiceNumber: string) => {
    if (!confirm(`هل أنت متأكد من حذف الفاتورة ${invoiceNumber}؟`)) return;

    try {
      await deletePurchaseInvoice(id).unwrap();
      toast.success("تم حذف الفاتورة بنجاح");
    } catch (error) {
      console.error("Error deleting invoice:", error);
      toast.error(handleApiError(error));
    }
  };

  const getStatusBadge = (status: string) => {
    const statusColors: Record<string, string> = {
      Draft: "bg-gray-100 text-gray-800",
      Confirmed: "bg-blue-100 text-blue-800",
      Paid: "bg-green-100 text-green-800",
      PartiallyPaid: "bg-yellow-100 text-yellow-800",
      Cancelled: "bg-red-100 text-red-800",
    };

    const statusLabels: Record<string, string> = {
      Draft: "مسودة",
      Confirmed: "مؤكدة",
      Paid: "مدفوعة",
      PartiallyPaid: "مدفوعة جزئياً",
      Cancelled: "ملغاة",
    };

    return (
      <span
        className={`px-2 py-1 rounded-full text-xs font-medium ${statusColors[status] || "bg-gray-100 text-gray-800"}`}
      >
        {statusLabels[status] || status}
      </span>
    );
  };

  if (isLoading) return <Loading />;

  const totalAmount = invoices.reduce((sum, inv) => sum + inv.total, 0);
  const paidCount = invoices.filter((inv) => inv.status === "Paid").length;

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
        <div className="mb-8">
          <div className="flex items-center gap-3 mb-2">
            <div className="w-8 h-8 rounded-full bg-violet-100 flex items-center justify-center">
              <ClipboardList className="w-5 h-5 text-violet-600" />
            </div>
            <h1 className="text-3xl font-bold text-gray-900">فواتير الشراء</h1>
          </div>
          <p className="text-gray-600">
            إدارة فواتير الشراء من الموردين والمستودع
          </p>
        </div>

        <div className="flex justify-end">
          <Button onClick={() => navigate("/purchase-invoices/new")}>
            إنشاء فاتورة جديدة
          </Button>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <Card className="border-violet-100">
            <p className="text-sm text-gray-600">إجمالي الفواتير</p>
            <p className="text-2xl font-bold text-gray-900 mt-1">
              {invoices.length}
            </p>
          </Card>
          <Card className="border-blue-100">
            <p className="text-sm text-gray-600">المبلغ الإجمالي</p>
            <p className="text-2xl font-bold text-blue-700 mt-1">
              {formatCurrency(totalAmount)}
            </p>
          </Card>
          <Card className="border-green-100">
            <p className="text-sm text-gray-600">الفواتير المدفوعة</p>
            <p className="text-2xl font-bold text-green-700 mt-1">
              {paidCount}
            </p>
          </Card>
        </div>

        <Card>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div>
              <label className="block text-sm font-medium mb-1">المورد</label>
              <div className="relative">
                <select
                  value={supplierId || ""}
                  onChange={(e) =>
                    setSupplierId(
                      e.target.value ? Number(e.target.value) : undefined,
                    )
                  }
                  className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
                >
                  <option value="">الكل</option>
                  {suppliers.map((supplier) => (
                    <option key={supplier.id} value={supplier.id}>
                      {supplier.name}
                    </option>
                  ))}
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium mb-1">الحالة</label>
              <div className="relative">
                <select
                  value={status || ""}
                  onChange={(e) =>
                    setStatus(
                      (e.target.value as PurchaseInvoiceStatus) || undefined,
                    )
                  }
                  className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
                >
                  <option value="">الكل</option>
                  <option value="Draft">مسودة</option>
                  <option value="Confirmed">مؤكدة</option>
                  <option value="Paid">مدفوعة</option>
                  <option value="PartiallyPaid">مدفوعة جزئياً</option>
                  <option value="Cancelled">ملغاة</option>
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium mb-1">من تاريخ</label>
              <input
                type="date"
                value={fromDate}
                onChange={(e) => setFromDate(e.target.value)}
                className="w-full px-3 py-2 border rounded-lg"
              />
            </div>

            <div>
              <label className="block text-sm font-medium mb-1">
                إلى تاريخ
              </label>
              <input
                type="date"
                value={toDate}
                onChange={(e) => setToDate(e.target.value)}
                className="w-full px-3 py-2 border rounded-lg"
              />
            </div>
          </div>
        </Card>

        <Card padding="none">
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-gray-50 sticky top-0 z-10">
                <tr>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    رقم الفاتورة
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    المورد
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    التاريخ
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    الحالة
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    الإجمالي
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    المدفوع
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    المتبقي
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    إجراءات
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {invoices.length === 0 ? (
                  <tr>
                    <td
                      colSpan={8}
                      className="px-4 py-8 text-center text-gray-500"
                    >
                      لا توجد فواتير
                    </td>
                  </tr>
                ) : (
                  invoices.map((invoice) => (
                    <tr key={invoice.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3 text-sm font-medium">
                        {invoice.invoiceNumber}
                      </td>
                      <td className="px-4 py-3 text-sm">
                        <div>{invoice.supplierName}</div>
                        {invoice.supplierPhone && (
                          <div className="text-xs text-gray-500">
                            {invoice.supplierPhone}
                          </div>
                        )}
                      </td>
                      <td className="px-4 py-3 text-sm">
                        {formatDateOnly(invoice.invoiceDate)}
                      </td>
                      <td className="px-4 py-3 text-sm">
                        {getStatusBadge(invoice.status)}
                      </td>
                      <td className="px-4 py-3 text-sm font-medium">
                        {formatCurrency(invoice.total)}
                      </td>
                      <td className="px-4 py-3 text-sm text-green-600">
                        {formatCurrency(invoice.amountPaid)}
                      </td>
                      <td className="px-4 py-3 text-sm text-red-600">
                        {formatCurrency(invoice.amountDue)}
                      </td>
                      <td className="px-4 py-3 text-sm">
                        <div className="flex gap-2">
                          <button
                            onClick={() =>
                              navigate(`/purchase-invoices/${invoice.id}`)
                            }
                            className="text-blue-600 hover:text-blue-800"
                          >
                            عرض
                          </button>
                          {invoice.status === "Draft" && (
                            <>
                              <button
                                onClick={() =>
                                  navigate(
                                    `/purchase-invoices/${invoice.id}/edit`,
                                  )
                                }
                                className="text-green-600 hover:text-green-800"
                              >
                                تعديل
                              </button>
                              <button
                                onClick={() =>
                                  handleDelete(
                                    invoice.id,
                                    invoice.invoiceNumber,
                                  )
                                }
                                className="text-red-600 hover:text-red-800"
                              >
                                حذف
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex justify-center items-center gap-2 p-4 border-t bg-gray-50">
              <Button
                variant="outline"
                onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                disabled={pageNumber === 1}
              >
                السابق
              </Button>
              <span className="text-sm">
                صفحة {pageNumber} من {totalPages}
              </span>
              <Button
                variant="outline"
                onClick={() =>
                  setPageNumber((p) => Math.min(totalPages, p + 1))
                }
                disabled={pageNumber === totalPages}
              >
                التالي
              </Button>
            </div>
          )}
        </Card>

        {/* Help Section */}
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-6">
          <h3 className="text-lg font-semibold text-blue-900 mb-3">
            💡 نصائح إدارة فواتير الشراء
          </h3>
          <ul className="space-y-2 text-sm text-blue-800">
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>فاتورة جديدة:</strong> أضف فاتورة شراء من الموردين مع
                تفاصيل البنود والأسعار
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>الحالات:</strong> تتبع حالة كل فاتورة من المسودة إلى
                الدفع الكامل
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>التصفية:</strong> استخدم الفلاتر للبحث حسب المورد
                والحالة والتاريخ
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>الحسابات:</strong> راقب إجمالي الشراء والفواتير المدفوعة
                والمعلقة
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>التدقيق:</strong> جميع الفواتير موثقة ويمكن تحديثها
                وحذفها عند الحاجة
              </span>
            </li>
          </ul>
        </div>
      </div>
    </div>
  );
}
