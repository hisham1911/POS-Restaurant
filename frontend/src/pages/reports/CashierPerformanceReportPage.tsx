import { useState } from "react";
import {
  Users,
  TrendingUp,
  Calendar,
  Loader2,
  AlertCircle,
  DollarSign,
  ShoppingBag,
  Star,
  Clock,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency } from "@/utils/formatters";
import { useGetCashierPerformanceReportQuery } from "@/api/employeeReportsApi";

export const CashierPerformanceReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(1)).toISOString().split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);

  const { data, isLoading, isError, error } =
    useGetCashierPerformanceReportQuery({ fromDate, toDate });
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

  const getRatingColor = (rating: string) => {
    switch (rating) {
      case "Excellent":
        return "text-green-600 bg-green-100";
      case "Good":
        return "text-blue-600 bg-blue-100";
      case "Average":
        return "text-yellow-600 bg-yellow-100";
      default:
        return "text-red-600 bg-red-100";
    }
  };

  return (
    <div className="h-full overflow-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">
            تقرير أداء الكاشير
          </h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "تحليل أداء الكاشيرات"}
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
              <Users className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-primary-700 font-medium">
                عدد الكاشيرات
              </p>
              <p className="text-2xl font-bold text-primary-600">
                {report?.totalCashiers || 0}
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
              <p className="text-sm text-gray-500">إجمالي الورديات</p>
              <p className="text-2xl font-bold text-blue-600">
                {report?.totalShifts || 0}
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
              <p className="text-sm text-gray-500">إجمالي المبيعات</p>
              <p className="text-2xl font-bold text-success-600">
                {formatCurrency(report?.totalRevenue || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-green-100 rounded-xl flex items-center justify-center">
              <ShoppingBag className="w-6 h-6 text-green-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">إجمالي الطلبات</p>
              <p className="text-2xl font-bold text-green-600">
                {report?.totalOrders || 0}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Table */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          تفاصيل أداء الكاشيرات
        </h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  #
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الكاشير
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الطلبات
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المبيعات
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  متوسط الطلب
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المرتجعات
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الخصومات
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  التقييم
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.cashierPerformance &&
              report.cashierPerformance.length > 0 ? (
                report.cashierPerformance.map((cashier, index) => (
                  <tr
                    key={cashier.userId}
                    className="border-b hover:bg-gray-50"
                  >
                    <td className="px-4 py-3 text-gray-500">{index + 1}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-primary-100 rounded-full flex items-center justify-center">
                          <Users className="w-5 h-5 text-primary-600" />
                        </div>
                        <div>
                          <span className="font-medium text-gray-800">
                            {cashier.userName}
                          </span>
                          <p className="text-xs text-gray-400">
                            {cashier.email}
                          </p>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3 font-medium text-gray-800">
                      {cashier.totalOrders}
                    </td>
                    <td className="px-4 py-3 font-semibold text-success-600">
                      {formatCurrency(cashier.totalRevenue)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {formatCurrency(cashier.averageOrderValue)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {cashier.refundedOrders}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {cashier.cancelledOrders}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <Star className="w-4 h-4 text-yellow-500" />
                        <span
                          className={`px-2 py-1 rounded-full text-xs font-medium ${getRatingColor(cashier.performanceRating)}`}
                        >
                          {cashier.performanceRating}
                        </span>
                      </div>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={8}
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
                • <strong>التقييم:</strong> يتم احتسابه بناءً على دقة العمليات
                ونسبة تحصيل الخزينة
              </li>
              <li>
                • <strong>الحالات:</strong> ممتاز (Excellent) | جيد (Good) |
                متوسط (Average) | ضعيف (Poor)
              </li>
              <li>
                • <strong>المبيعات:</strong> إجمالي قيمة المبيعات المكتملة من كل
                كاشير
              </li>
              <li>
                • <strong>الفترة:</strong> اختر التاريخ لعرض الأداء في نطاق محدد
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default CashierPerformanceReportPage;
