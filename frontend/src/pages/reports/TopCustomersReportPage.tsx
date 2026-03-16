import { useState } from "react";
import {
  Users,
  TrendingUp,
  Calendar,
  Loader2,
  AlertCircle,
  DollarSign,
  ShoppingBag,
  Phone,
  AlertTriangle,
  ChevronDown,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDateOnly } from "@/utils/formatters";
import { useGetTopCustomersReportQuery } from "@/api/customerReportsApi";

export const TopCustomersReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(1)).toISOString().split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);
  const [topCount, setTopCount] = useState(20);

  const { data, isLoading, isError, error } = useGetTopCustomersReportQuery({
    fromDate,
    toDate,
    topCount,
  });
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
            تقرير أفضل العملاء
          </h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "العملاء الأكثر شراءً"}
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

      {/* Filters */}
      <Card>
        <div className="flex items-center gap-4">
          <span className="text-sm font-medium text-gray-700">
            عدد العملاء:
          </span>
          <div className="relative">
            <select
              value={topCount}
              onChange={(e) => setTopCount(Number(e.target.value))}
              className="appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm min-w-[140px]"
            >
              <option value={10}>أفضل 10</option>
              <option value={20}>أفضل 20</option>
              <option value={50}>أفضل 50</option>
              <option value={100}>أفضل 100</option>
            </select>
            <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
          </div>
        </div>
      </Card>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card className="bg-gradient-to-br from-primary-50 to-primary-100 border-primary-200">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-primary-500 rounded-xl flex items-center justify-center">
              <Users className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-primary-700 font-medium">
                إجمالي العملاء
              </p>
              <p className="text-2xl font-bold text-primary-600">
                {report?.totalCustomers || 0}
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
              <p className="text-sm text-gray-500">إجمالي الإيرادات</p>
              <p className="text-2xl font-bold text-success-600">
                {formatCurrency(report?.totalRevenue || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <TrendingUp className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">متوسط قيمة العميل</p>
              <p className="text-2xl font-bold text-blue-600">
                {formatCurrency(report?.averageCustomerValue || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-green-100 rounded-xl flex items-center justify-center">
              <Users className="w-6 h-6 text-green-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">عملاء جدد</p>
              <p className="text-2xl font-bold text-green-600">
                {report?.newCustomers || 0}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Top Customers Table */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          أفضل {topCount} عميل
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
                  عدد الطلبات
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  إجمالي المشتريات
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  متوسط الطلب
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  آخر طلب
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الرصيد المستحق
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.topCustomers && report.topCustomers.length > 0 ? (
                report.topCustomers.map((customer, index) => (
                  <tr
                    key={customer.customerId}
                    className="border-b hover:bg-gray-50"
                  >
                    <td className="px-4 py-3 text-gray-500">{index + 1}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-primary-100 rounded-full flex items-center justify-center">
                          <Users className="w-5 h-5 text-primary-600" />
                        </div>
                        <span className="font-medium text-gray-800">
                          {customer.customerName}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2 text-gray-600">
                        <Phone className="w-4 h-4" />
                        <span>{customer.phone || "-"}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <ShoppingBag className="w-4 h-4 text-gray-400" />
                        <span className="font-medium text-gray-800">
                          {customer.totalOrders}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 font-semibold text-success-600">
                      {formatCurrency(customer.totalSpent)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {formatCurrency(customer.averageOrderValue)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {formatDateOnly(customer.lastOrderDate)}
                    </td>
                    <td className="px-4 py-3">
                      {customer.outstandingBalance > 0 ? (
                        <div className="flex items-center gap-1">
                          <AlertTriangle className="w-4 h-4 text-warning-500" />
                          <span className="font-medium text-warning-600">
                            {formatCurrency(customer.outstandingBalance)}
                          </span>
                        </div>
                      ) : (
                        <span className="text-gray-400">-</span>
                      )}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={8}
                    className="px-4 py-8 text-center text-gray-400"
                  >
                    لا توجد بيانات عملاء في هذه الفترة
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
                • <strong>الترتيب:</strong> يتم ترتيب العملاء حسب إجمالي قيمة
                المشتريات خلال الفترة.
              </li>
              <li>
                • <strong>عدد الطلبات:</strong> إجمالي عدد الفواتير لكل عميل في
                الفترة المحددة.
              </li>
              <li>
                • <strong>متوسط الطلب:</strong> إجمالي المشتريات مقسوماً على عدد
                الطلبات.
              </li>
              <li>
                • <strong>نصيحة:</strong> ركّز على كبار العملاء بعروض خاصة
                للحفاظ على ولائهم.
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default TopCustomersReportPage;
