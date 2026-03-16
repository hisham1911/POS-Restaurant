import { useState } from "react";
import {
  Truck,
  Calendar,
  Loader2,
  AlertCircle,
  DollarSign,
  Receipt,
  ShoppingBag,
  Phone,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDateOnly } from "@/utils/formatters";
import { useGetSupplierPurchasesReportQuery } from "@/api/supplierReportsApi";

export const SupplierPurchasesReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(1)).toISOString().split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);

  const { data, isLoading, isError, error } =
    useGetSupplierPurchasesReportQuery({ fromDate, toDate });
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
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">
            تقرير مشتريات الموردين
          </h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "تحليل مشتريات الموردين"}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <Calendar className="w-5 h-5 text-gray-400" />
            <input
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
              className="px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
            />
            <span className="text-gray-500">إلى</span>
            <input
              type="date"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
              className="px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
            />
          </div>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card className="bg-gradient-to-br from-primary-50 to-primary-100 border-primary-200">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-primary-500 rounded-xl flex items-center justify-center">
              <Truck className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-primary-700 font-medium">
                عدد الموردين
              </p>
              <p className="text-2xl font-bold text-primary-600">
                {report?.totalSuppliers || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <Receipt className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">عدد الفواتير</p>
              <p className="text-2xl font-bold text-blue-600">
                {report?.totalInvoices || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-success-100 rounded-xl flex items-center justify-center">
              <DollarSign className="w-6 h-6 text-success-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">إجمالي المشتريات</p>
              <p className="text-2xl font-bold text-success-600">
                {formatCurrency(report?.totalPurchases || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-orange-100 rounded-xl flex items-center justify-center">
              <DollarSign className="w-6 h-6 text-orange-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">المستحق</p>
              <p className="text-2xl font-bold text-orange-600">
                {formatCurrency(report?.totalOutstanding || 0)}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Table */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          تفاصيل مشتريات الموردين
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
                  الفواتير
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المشتريات
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المدفوع
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المستحق
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  آخر شراء
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.supplierDetails && report.supplierDetails.length > 0 ? (
                report.supplierDetails.map((supplier, index) => (
                  <tr
                    key={supplier.supplierId}
                    className="border-b hover:bg-gray-50"
                  >
                    <td className="px-4 py-3 text-gray-500">{index + 1}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-cyan-100 rounded-full flex items-center justify-center">
                          <Truck className="w-5 h-5 text-cyan-600" />
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
                    <td className="px-4 py-3 font-medium text-gray-800">
                      {supplier.invoiceCount}
                    </td>
                    <td className="px-4 py-3 font-semibold text-gray-800">
                      {formatCurrency(supplier.totalPurchases)}
                    </td>
                    <td className="px-4 py-3 text-success-600">
                      {formatCurrency(supplier.totalPaid)}
                    </td>
                    <td className="px-4 py-3">
                      {supplier.outstanding > 0 ? (
                        <span className="font-medium text-orange-600">
                          {formatCurrency(supplier.outstanding)}
                        </span>
                      ) : (
                        <span className="text-gray-400">0</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {supplier.lastPurchaseDate
                        ? formatDateOnly(supplier.lastPurchaseDate)
                        : "-"}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={7}
                    className="px-4 py-8 text-center text-gray-400"
                  >
                    لا توجد بيانات في هذه الفترة
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
                • <strong>المشتريات:</strong> إجمالي قيمة الفواتير المؤكدة من كل
                مورد
              </li>
              <li>
                • <strong>عدد الفواتير:</strong> عدد الفواتير المنفذة من المورد
              </li>
              <li>
                • <strong>الفترة:</strong> تمثل تاريخ تأكيد الفاتورة
              </li>
              <li>
                • <strong>الغرض:</strong> تتبع حجم التعامل مع كل مورد والعلاقات
                التجارية
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default SupplierPurchasesReportPage;
