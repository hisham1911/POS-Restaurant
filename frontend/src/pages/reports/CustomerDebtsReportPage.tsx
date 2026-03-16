import { useState } from "react";
import {
  AlertTriangle,
  Users,
  DollarSign,
  Loader2,
  AlertCircle,
  Phone,
  Calendar,
  Clock,
  TrendingUp,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDateOnly } from "@/utils/formatters";
import { useGetCustomerDebtsReportQuery } from "@/api/customerReportsApi";

export const CustomerDebtsReportPage = () => {
  const { data, isLoading, isError, error } = useGetCustomerDebtsReportQuery();
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
            تقرير ديون العملاء
          </h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "المستحقات والديون المتأخرة"}
          </p>
        </div>
        <div className="flex items-center gap-2 text-gray-600">
          <Calendar className="w-5 h-5" />
          <span className="text-sm">
            {report?.reportDate ? formatDateOnly(report.reportDate) : ""}
          </span>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card className="bg-gradient-to-br from-warning-50 to-warning-100 border-warning-200">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-warning-500 rounded-xl flex items-center justify-center">
              <AlertTriangle className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-warning-700 font-medium">
                إجمالي المستحقات
              </p>
              <p className="text-2xl font-bold text-warning-600">
                {formatCurrency(report?.totalOutstandingAmount || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center">
              <Clock className="w-6 h-6 text-red-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">ديون متأخرة</p>
              <p className="text-2xl font-bold text-red-600">
                {formatCurrency(report?.totalOverdueAmount || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <Users className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">عملاء لديهم ديون</p>
              <p className="text-2xl font-bold text-blue-600">
                {report?.totalCustomersWithDebt || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-orange-100 rounded-xl flex items-center justify-center">
              <AlertTriangle className="w-6 h-6 text-orange-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">ديون متأخرة</p>
              <p className="text-2xl font-bold text-orange-600">
                {report?.overdueCustomersCount || 0}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Aging Analysis */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          تحليل عمر الديون
        </h3>
        <div className="space-y-4">
          {report?.agingAnalysis && report.agingAnalysis.length > 0 ? (
            report.agingAnalysis.map((bracket) => (
              <div key={bracket.bracket}>
                <div className="flex justify-between mb-2">
                  <div>
                    <span className="font-medium text-gray-700">
                      {bracket.bracket}
                    </span>
                    <span className="text-sm text-gray-500 mr-2">
                      ({bracket.customerCount} عميل)
                    </span>
                  </div>
                  <div className="text-left">
                    <span className="font-bold text-gray-800">
                      {formatCurrency(bracket.totalAmount)}
                    </span>
                    <span className="text-sm text-gray-500 mr-2">
                      ({bracket.percentage.toFixed(1)}%)
                    </span>
                  </div>
                </div>
                <div className="h-3 bg-gray-100 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-gradient-to-r from-warning-400 to-warning-600 rounded-full transition-all"
                    style={{ width: `${bracket.percentage}%` }}
                  />
                </div>
              </div>
            ))
          ) : (
            <p className="text-gray-400 text-center py-4">لا توجد بيانات</p>
          )}
        </div>
      </Card>

      {/* Customer Debts Table */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          تفاصيل ديون العملاء
        </h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  #
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  العميل
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  رقم الهاتف
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المبلغ المستحق
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  حد الائتمان
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  عدد الطلبات غير المدفوعة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  أقدم طلب غير مدفوع
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  آخر طلب
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الحالة
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.customerDebts && report.customerDebts.length > 0 ? (
                report.customerDebts.map((debt, index) => (
                  <tr
                    key={debt.customerId}
                    className={`border-b hover:bg-gray-50 ${
                      debt.isOverLimit ? "bg-red-50" : ""
                    }`}
                  >
                    <td className="px-4 py-3 text-gray-500">{index + 1}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-warning-100 rounded-full flex items-center justify-center">
                          <Users className="w-5 h-5 text-warning-600" />
                        </div>
                        <span className="font-medium text-gray-800">
                          {debt.customerName}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2 text-gray-600">
                        <Phone className="w-4 h-4" />
                        <span>{debt.phone || "-"}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 font-bold text-warning-600">
                      {formatCurrency(debt.totalDue)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {debt.creditLimit > 0
                        ? formatCurrency(debt.creditLimit)
                        : "-"}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <span className="inline-flex items-center justify-center w-8 h-8 rounded-full bg-gray-100 text-gray-700 font-medium">
                        {debt.unpaidOrdersCount}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {debt.oldestUnpaidOrderDate
                        ? formatDateOnly(debt.oldestUnpaidOrderDate)
                        : "-"}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {debt.lastOrderDate
                        ? formatDateOnly(debt.lastOrderDate)
                        : "-"}
                      {debt.daysSinceLastOrder > 0 && (
                        <span className="text-xs text-gray-400 block">
                          منذ {debt.daysSinceLastOrder} يوم
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      {debt.isOverLimit ? (
                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-red-100 text-red-800">
                          <AlertTriangle className="w-3 h-3 ml-1" />
                          تجاوز الحد
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-warning-100 text-warning-800">
                          <Clock className="w-3 h-3 ml-1" />
                          مستحق
                        </span>
                      )}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={9}
                    className="px-4 py-8 text-center text-gray-400"
                  >
                    لا توجد ديون مستحقة
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
                • <strong>إجمالي المستحقات:</strong> مجموع جميع الديون غير
                المسددة لجميع العملاء.
              </li>
              <li>
                • <strong>الفواتير المتأخرة:</strong> الفواتير التي تجاوزت موعد
                استحقاقها ولم تُسدد بعد.
              </li>
              <li>
                • <strong>أولوية السداد:</strong> يتم ترتيب العملاء حسب أقدم
                فاتورة مستحقة ثم حسب المبلغ.
              </li>
              <li>
                • <strong>تنبيه:</strong> المبالغ المتأخرة أكثر من 30 يوماً
                تتطلب متابعة فورية.
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default CustomerDebtsReportPage;
