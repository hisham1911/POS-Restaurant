import {
  Truck,
  Loader2,
  AlertCircle,
  DollarSign,
  AlertTriangle,
  Clock,
  Phone,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDateOnly } from "@/utils/formatters";
import { useGetSupplierDebtsReportQuery } from "@/api/supplierReportsApi";

export const SupplierDebtsReportPage = () => {
  const { data, isLoading, isError, error } = useGetSupplierDebtsReportQuery();
  const report = data?.data;

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-primary-500" />
        <span className="mr-2 text-gray-600">جاري تحميل التقرير...</span>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="h-full flex items-center justify-center">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 text-red-500 mx-auto mb-4" />
          <p className="text-red-600">فشل في تحميل التقرير</p>
          <p className="text-gray-500 text-sm mt-2">
            {(error as any)?.data?.message || "حدث خطأ غير متوقع"}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="h-full overflow-auto p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-800">
          تقرير ديون الموردين
        </h1>
        <p className="text-gray-500 mt-1">
          {report?.branchName || "المستحقات المالية للموردين"}
        </p>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card className="bg-gradient-to-br from-orange-50 to-orange-100 border-orange-200">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-orange-500 rounded-xl flex items-center justify-center">
              <Truck className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-orange-700 font-medium">
                موردون مدينون
              </p>
              <p className="text-2xl font-bold text-orange-600">
                {report?.totalSuppliersWithDebt || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center">
              <DollarSign className="w-6 h-6 text-red-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">إجمالي المستحق</p>
              <p className="text-2xl font-bold text-red-600">
                {formatCurrency(report?.totalOutstandingAmount || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-yellow-100 rounded-xl flex items-center justify-center">
              <AlertTriangle className="w-6 h-6 text-yellow-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">المتأخر</p>
              <p className="text-2xl font-bold text-yellow-600">
                {formatCurrency(report?.totalOverdueAmount || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <Clock className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">فواتير متأخرة</p>
              <p className="text-2xl font-bold text-blue-600">
                {report?.overdueInvoicesCount || 0}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Table */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          تفاصيل ديون الموردين
        </h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  #
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المورد
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الرصيد المستحق
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  فواتير غير مدفوعة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  أقدم فاتورة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  آخر دفع
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.supplierDebts && report.supplierDebts.length > 0 ? (
                report.supplierDebts.map((supplier, index) => (
                  <tr
                    key={supplier.supplierId}
                    className="border-b hover:bg-gray-50"
                  >
                    <td className="px-4 py-3 text-gray-500">{index + 1}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-orange-100 rounded-full flex items-center justify-center">
                          <Truck className="w-5 h-5 text-orange-600" />
                        </div>
                        <div>
                          <span className="font-medium text-gray-800">
                            {supplier.supplierName}
                          </span>
                          {supplier.phone && (
                            <div className="flex items-center gap-1 text-xs text-gray-400">
                              <Phone className="w-3 h-3" />
                              {supplier.phone}
                            </div>
                          )}
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3 font-semibold text-red-600">
                      {formatCurrency(supplier.totalDue)}
                    </td>
                    <td className="px-4 py-3 font-medium text-gray-800">
                      {supplier.unpaidInvoicesCount}
                    </td>
                    <td className="px-4 py-3 text-sm">
                      {supplier.oldestUnpaidInvoiceDate ? (
                        <div>
                          <span className="text-gray-600">
                            {formatDateOnly(supplier.oldestUnpaidInvoiceDate)}
                          </span>
                          <p className="text-xs text-gray-400">
                            منذ {supplier.daysSinceOldestInvoice} يوم
                          </p>
                        </div>
                      ) : (
                        "-"
                      )}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {supplier.lastPaymentDate
                        ? formatDateOnly(supplier.lastPaymentDate)
                        : "لا يوجد"}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={6}
                    className="px-4 py-8 text-center text-gray-400"
                  >
                    لا توجد ديون للموردين
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>

      {/* Info Card */}
      <Card className="bg-blue-50 border-blue-200">
        <div className="flex items-start gap-3">
          <Info className="w-5 h-5 text-blue-600 mt-0.5 flex-shrink-0" />
          <div className="flex-1">
            <h3 className="text-sm font-semibold text-blue-900 mb-2">
              معلومات التقرير
            </h3>
            <ul className="text-sm text-blue-800 space-y-1">
              <li>
                • <strong>الديون:</strong> المبالغ المستحقة الدفع للموردين
                (الفواتير غير المدفوعة)
              </li>
              <li>
                • <strong>الحالات:</strong> فاتورة|فواتير غير مدفوعة أو مدفوعة
                جزئياً
              </li>
              <li>
                • <strong>المتأخر:</strong> الفواتير التي تجاوز موعد سدادها
              </li>
              <li>
                • <strong>الأهمية:</strong> معرفة التزاماتك المالية تجاه
                الموردين
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default SupplierDebtsReportPage;
