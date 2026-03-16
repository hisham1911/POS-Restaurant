import { useState } from "react";
import {
  TrendingUp,
  ShoppingBag,
  DollarSign,
  Calendar,
  Loader2,
  AlertCircle,
  BarChart3,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDateOnly } from "@/utils/formatters";
import { useGetSalesReportQuery } from "@/api/reportsApi";

export const SalesReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(1)).toISOString().split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);

  const { data, isLoading, isError, error } = useGetSalesReportQuery({
    fromDate,
    toDate,
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
          <h1 className="text-2xl font-bold text-gray-800">تقرير المبيعات</h1>
          <p className="text-gray-500 mt-1">تحليل شامل للمبيعات</p>
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
              <DollarSign className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-primary-700 font-medium">
                إجمالي المبيعات
              </p>
              <p className="text-2xl font-bold text-primary-600">
                {formatCurrency(report?.totalSales || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center">
              <TrendingUp className="w-6 h-6 text-red-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">التكلفة</p>
              <p className="text-2xl font-bold text-red-600">
                {formatCurrency(report?.totalCost || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-success-100 rounded-xl flex items-center justify-center">
              <TrendingUp className="w-6 h-6 text-success-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">إجمالي الربح</p>
              <p className="text-2xl font-bold text-success-600">
                {formatCurrency(report?.grossProfit || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <ShoppingBag className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">عدد الطلبات</p>
              <p className="text-2xl font-bold text-gray-800">
                {report?.totalOrders || 0}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Average Order Value */}
      <Card>
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-gray-500 mb-1">متوسط قيمة الطلب</p>
            <p className="text-3xl font-bold text-gray-800">
              {formatCurrency(report?.averageOrderValue || 0)}
            </p>
          </div>
          <BarChart3 className="w-12 h-12 text-gray-300" />
        </div>
      </Card>

      {/* Daily Sales Chart */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          المبيعات اليومية
        </h3>
        <div className="space-y-2 max-h-96 overflow-y-auto">
          {report?.dailySales && report.dailySales.length > 0 ? (
            report.dailySales.map((day) => {
              const maxSales = Math.max(
                ...report.dailySales.map((d) => d.sales),
              );
              const percentage =
                maxSales > 0 ? (day.sales / maxSales) * 100 : 0;

              return (
                <div key={day.date} className="space-y-1">
                  <div className="flex items-center justify-between">
                    <span className="text-sm text-gray-600">
                      {formatDateOnly(day.date)}
                    </span>
                    <div className="text-left">
                      <span className="font-medium text-gray-800">
                        {formatCurrency(day.sales)}
                      </span>
                      <span className="text-gray-400 text-sm mr-2">
                        ({day.orders} طلب)
                      </span>
                    </div>
                  </div>
                  <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-gradient-to-r from-primary-500 to-primary-600 rounded-full transition-all"
                      style={{ width: `${percentage}%` }}
                    />
                  </div>
                </div>
              );
            })
          ) : (
            <p className="text-gray-400 text-center py-8">لا توجد بيانات</p>
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
                • <strong>المبيعات:</strong> إجمالي الطلبات المكتملة فقط (لا
                تشمل الملغاة أو المعلقة)
              </li>
              <li>
                • <strong>الإيرادات:</strong> صافي المبيعات بعد خصم المرتجعات
                والخصومات
              </li>
              <li>
                • <strong>متوسط الطلب:</strong> إجمالي المبيعات ÷ عدد الطلبات
              </li>
              <li>
                • <strong>الفترة:</strong> حسب تاريخ إتمام الطلب وليس تاريخ
                إنشائه
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default SalesReportPage;
