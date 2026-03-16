import { useState } from "react";
import {
  Activity,
  Users,
  TrendingUp,
  Calendar,
  Loader2,
  AlertCircle,
  DollarSign,
  UserPlus,
  UserCheck,
  PieChart,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency } from "@/utils/formatters";
import { useGetCustomerActivityReportQuery } from "@/api/customerReportsApi";

export const CustomerActivityReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(1)).toISOString().split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);

  const { data, isLoading, isError, error } = useGetCustomerActivityReportQuery(
    {
      fromDate,
      toDate,
    },
  );
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
            تقرير نشاط العملاء
          </h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "تحليل سلوك العملاء والاحتفاظ"}
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
        <Card className="bg-gradient-to-br from-green-50 to-green-100 border-green-200">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-green-500 rounded-xl flex items-center justify-center">
              <UserPlus className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-green-700 font-medium">عملاء جدد</p>
              <p className="text-2xl font-bold text-green-600">
                {report?.newCustomers || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card className="bg-gradient-to-br from-blue-50 to-blue-100 border-blue-200">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-500 rounded-xl flex items-center justify-center">
              <UserCheck className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-blue-700 font-medium">عملاء عائدون</p>
              <p className="text-2xl font-bold text-blue-600">
                {report?.returningCustomers || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-purple-100 rounded-xl flex items-center justify-center">
              <TrendingUp className="w-6 h-6 text-purple-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">معدل الاحتفاظ</p>
              <p className="text-2xl font-bold text-purple-600">
                {report?.retentionRate || 0}%
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center">
              <Activity className="w-6 h-6 text-red-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">معدل التسرب</p>
              <p className="text-2xl font-bold text-red-600">
                {report?.churnRate || 0}%
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Revenue Breakdown */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <h3 className="text-lg font-bold text-gray-800 mb-4">
            الإيرادات حسب نوع العميل
          </h3>
          <div className="space-y-4">
            <div>
              <div className="flex justify-between mb-2">
                <div className="flex items-center gap-2">
                  <UserPlus className="w-4 h-4 text-green-600" />
                  <span className="text-gray-700">عملاء جدد</span>
                </div>
                <span className="font-bold text-green-600">
                  {formatCurrency(report?.newCustomerRevenue || 0)}
                </span>
              </div>
              <div className="h-3 bg-gray-100 rounded-full overflow-hidden">
                <div
                  className="h-full bg-green-500 rounded-full transition-all"
                  style={{
                    width: `${
                      report?.newCustomerRevenue &&
                      report?.returningCustomerRevenue
                        ? (report.newCustomerRevenue /
                            (report.newCustomerRevenue +
                              report.returningCustomerRevenue)) *
                          100
                        : 0
                    }%`,
                  }}
                />
              </div>
            </div>

            <div>
              <div className="flex justify-between mb-2">
                <div className="flex items-center gap-2">
                  <UserCheck className="w-4 h-4 text-blue-600" />
                  <span className="text-gray-700">عملاء عائدون</span>
                </div>
                <span className="font-bold text-blue-600">
                  {formatCurrency(report?.returningCustomerRevenue || 0)}
                </span>
              </div>
              <div className="h-3 bg-gray-100 rounded-full overflow-hidden">
                <div
                  className="h-full bg-blue-500 rounded-full transition-all"
                  style={{
                    width: `${
                      report?.newCustomerRevenue &&
                      report?.returningCustomerRevenue
                        ? (report.returningCustomerRevenue /
                            (report.newCustomerRevenue +
                              report.returningCustomerRevenue)) *
                          100
                        : 0
                    }%`,
                  }}
                />
              </div>
            </div>
          </div>
        </Card>

        <Card>
          <h3 className="text-lg font-bold text-gray-800 mb-4">
            متوسط قيمة العميل
          </h3>
          <div className="space-y-4">
            <div className="flex items-center justify-between p-4 bg-green-50 rounded-lg">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-green-100 rounded-full flex items-center justify-center">
                  <UserPlus className="w-5 h-5 text-green-600" />
                </div>
                <div>
                  <p className="text-sm text-gray-600">عملاء جدد</p>
                  <p className="text-xs text-gray-500">
                    {report?.newCustomers || 0} عميل
                  </p>
                </div>
              </div>
              <div className="text-left">
                <p className="text-xl font-bold text-green-600">
                  {formatCurrency(report?.averageNewCustomerValue || 0)}
                </p>
                <p className="text-xs text-gray-500">متوسط القيمة</p>
              </div>
            </div>

            <div className="flex items-center justify-between p-4 bg-blue-50 rounded-lg">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-blue-100 rounded-full flex items-center justify-center">
                  <UserCheck className="w-5 h-5 text-blue-600" />
                </div>
                <div>
                  <p className="text-sm text-gray-600">عملاء عائدون</p>
                  <p className="text-xs text-gray-500">
                    {report?.returningCustomers || 0} عميل
                  </p>
                </div>
              </div>
              <div className="text-left">
                <p className="text-xl font-bold text-blue-600">
                  {formatCurrency(report?.averageReturningCustomerValue || 0)}
                </p>
                <p className="text-xs text-gray-500">متوسط القيمة</p>
              </div>
            </div>
          </div>
        </Card>
      </div>

      {/* Customer Segments */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4 flex items-center gap-2">
          <PieChart className="w-5 h-5" />
          شرائح العملاء
        </h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الشريحة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  عدد العملاء
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  إجمالي الإيرادات
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  متوسط قيمة الطلب
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  إجمالي الطلبات
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.customerSegments &&
              report.customerSegments.length > 0 ? (
                report.customerSegments.map((segment) => (
                  <tr
                    key={segment.segmentName}
                    className="border-b hover:bg-gray-50"
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <Users className="w-4 h-4 text-gray-400" />
                        <span className="font-medium text-gray-800">
                          {segment.segmentName}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {segment.customerCount}
                    </td>
                    <td className="px-4 py-3 font-semibold text-success-600">
                      {formatCurrency(segment.totalRevenue)}
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {formatCurrency(segment.averageOrderValue)}
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {segment.totalOrders}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={5}
                    className="px-4 py-8 text-center text-gray-400"
                  >
                    لا توجد بيانات
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>

      {/* Insights */}
      <Card className="bg-gradient-to-br from-primary-50 to-primary-100 border-primary-200">
        <h3 className="text-lg font-bold text-primary-800 mb-4">
          💡 رؤى وتوصيات
        </h3>
        <div className="space-y-3">
          {report && (
            <>
              {report.retentionRate > 70 && (
                <div className="flex items-start gap-3 p-3 bg-white rounded-lg">
                  <div className="w-8 h-8 bg-success-100 rounded-full flex items-center justify-center flex-shrink-0">
                    <TrendingUp className="w-4 h-4 text-success-600" />
                  </div>
                  <div>
                    <p className="font-medium text-gray-800">
                      معدل احتفاظ ممتاز ({report.retentionRate}%)
                    </p>
                    <p className="text-sm text-gray-600 mt-1">
                      عملاؤك راضون ويعودون للشراء مرة أخرى. استمر في تقديم نفس
                      مستوى الخدمة.
                    </p>
                  </div>
                </div>
              )}

              {report.retentionRate < 50 && (
                <div className="flex items-start gap-3 p-3 bg-white rounded-lg">
                  <div className="w-8 h-8 bg-warning-100 rounded-full flex items-center justify-center flex-shrink-0">
                    <AlertCircle className="w-4 h-4 text-warning-600" />
                  </div>
                  <div>
                    <p className="font-medium text-gray-800">
                      معدل احتفاظ منخفض ({report.retentionRate}%)
                    </p>
                    <p className="text-sm text-gray-600 mt-1">
                      يجب تحسين تجربة العملاء. فكر في برنامج ولاء أو عروض خاصة
                      للعملاء العائدين.
                    </p>
                  </div>
                </div>
              )}

              {report.averageNewCustomerValue >
                report.averageReturningCustomerValue && (
                <div className="flex items-start gap-3 p-3 bg-white rounded-lg">
                  <div className="w-8 h-8 bg-blue-100 rounded-full flex items-center justify-center flex-shrink-0">
                    <DollarSign className="w-4 h-4 text-blue-600" />
                  </div>
                  <div>
                    <p className="font-medium text-gray-800">
                      العملاء الجدد ينفقون أكثر
                    </p>
                    <p className="text-sm text-gray-600 mt-1">
                      العملاء الجدد ينفقون{" "}
                      {formatCurrency(report.averageNewCustomerValue)} مقابل{" "}
                      {formatCurrency(report.averageReturningCustomerValue)}{" "}
                      للعملاء العائدين. ركز على جذب عملاء جدد.
                    </p>
                  </div>
                </div>
              )}
            </>
          )}
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
                • <strong>العملاء الجدد:</strong> العملاء الذين أجروا أول عملية
                شراء خلال الفترة المحددة.
              </li>
              <li>
                • <strong>العملاء العائدون:</strong> العملاء الذين لديهم مشتريات
                سابقة قبل بداية الفترة.
              </li>
              <li>
                • <strong>معدل الاحتفاظ:</strong> نسبة العملاء العائدين من
                إجمالي عملاء الفترة.
              </li>
              <li>
                • <strong>متوسط القيمة:</strong> متوسط إنفاق العميل الواحد خلال
                الفترة المحددة.
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default CustomerActivityReportPage;
