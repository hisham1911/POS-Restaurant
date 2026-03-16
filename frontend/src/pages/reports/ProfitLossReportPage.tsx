import { useState } from "react";
import {
  TrendingUp,
  TrendingDown,
  DollarSign,
  ShoppingBag,
  Receipt,
  Loader2,
  AlertCircle,
  Calendar,
  PieChart,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency } from "@/utils/formatters";
import { useGetProfitLossReportQuery } from "@/api/financialReportsApi";

export const ProfitLossReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(1)).toISOString().split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);

  const { data, isLoading, isError, error } = useGetProfitLossReportQuery({
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

  const isProfit = (report?.netProfit || 0) >= 0;

  return (
    <div className="h-full overflow-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">
            تقرير الأرباح والخسائر
          </h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "تحليل مالي شامل"}
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

      {/* Net Profit Card */}
      <Card className="bg-gradient-to-br from-primary-50 to-primary-100 border-primary-200">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-primary-700 font-medium mb-1">
              صافي الربح
            </p>
            <p
              className={`text-4xl font-bold ${isProfit ? "text-success-600" : "text-red-600"}`}
            >
              {formatCurrency(report?.netProfit || 0)}
            </p>
            <p className="text-sm text-primary-600 mt-2">
              هامش الربح: {report?.netProfitMargin || 0}%
            </p>
          </div>
          <div
            className={`w-16 h-16 rounded-full flex items-center justify-center ${
              isProfit ? "bg-success-100" : "bg-red-100"
            }`}
          >
            {isProfit ? (
              <TrendingUp className="w-8 h-8 text-success-600" />
            ) : (
              <TrendingDown className="w-8 h-8 text-red-600" />
            )}
          </div>
        </div>
      </Card>

      {/* Revenue Section */}
      <div>
        <h2 className="text-lg font-bold text-gray-800 mb-4">الإيرادات</h2>
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <Card>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
                <ShoppingBag className="w-5 h-5 text-blue-600" />
              </div>
              <div>
                <p className="text-xs text-gray-500">إجمالي المبيعات</p>
                <p className="text-lg font-bold text-gray-800">
                  {formatCurrency(report?.grossSales || 0)}
                </p>
              </div>
            </div>
          </Card>

          <Card>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-orange-100 rounded-lg flex items-center justify-center">
                <TrendingDown className="w-5 h-5 text-orange-600" />
              </div>
              <div>
                <p className="text-xs text-gray-500">الخصومات</p>
                <p className="text-lg font-bold text-orange-600">
                  {formatCurrency(report?.totalDiscount || 0)}
                </p>
              </div>
            </div>
          </Card>

          <Card>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
                <DollarSign className="w-5 h-5 text-green-600" />
              </div>
              <div>
                <p className="text-xs text-gray-500">صافي المبيعات</p>
                <p className="text-lg font-bold text-green-600">
                  {formatCurrency(report?.netSales || 0)}
                </p>
              </div>
            </div>
          </Card>

          <Card>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-purple-100 rounded-lg flex items-center justify-center">
                <Receipt className="w-5 h-5 text-purple-600" />
              </div>
              <div>
                <p className="text-xs text-gray-500">الضرائب</p>
                <p className="text-lg font-bold text-purple-600">
                  {formatCurrency(report?.totalTax || 0)}
                </p>
              </div>
            </div>
          </Card>
        </div>
      </div>

      {/* Profit Analysis */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Gross Profit */}
        <Card>
          <h3 className="text-lg font-bold text-gray-800 mb-4">إجمالي الربح</h3>
          <div className="space-y-4">
            <div className="flex justify-between items-center">
              <span className="text-gray-600">صافي المبيعات</span>
              <span className="font-semibold text-green-600">
                {formatCurrency(report?.netSales || 0)}
              </span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-600">تكلفة البضاعة المباعة</span>
              <span className="font-semibold text-red-600">
                ({formatCurrency(report?.totalCost || 0)})
              </span>
            </div>
            <div className="border-t pt-3 flex justify-between items-center">
              <span className="font-bold text-gray-800">إجمالي الربح</span>
              <span className="font-bold text-primary-600 text-xl">
                {formatCurrency(report?.grossProfit || 0)}
              </span>
            </div>
            <div className="bg-primary-50 rounded-lg p-3">
              <div className="flex justify-between items-center">
                <span className="text-sm text-primary-700">
                  هامش الربح الإجمالي
                </span>
                <span className="font-bold text-primary-600">
                  {report?.grossProfitMargin || 0}%
                </span>
              </div>
            </div>
          </div>
        </Card>

        {/* Expenses Breakdown */}
        <Card>
          <h3 className="text-lg font-bold text-gray-800 mb-4 flex items-center gap-2">
            <PieChart className="w-5 h-5" />
            المصروفات حسب الفئة
          </h3>
          <div className="space-y-3">
            {report?.expensesByCategory &&
            report.expensesByCategory.length > 0 ? (
              report.expensesByCategory.map((category) => (
                <div key={category.categoryId}>
                  <div className="flex justify-between mb-1">
                    <span className="text-sm text-gray-600">
                      {category.categoryName}
                    </span>
                    <span className="text-sm font-medium">
                      {formatCurrency(category.totalAmount)}
                    </span>
                  </div>
                  <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-red-500 rounded-full"
                      style={{ width: `${category.percentage}%` }}
                    />
                  </div>
                  <p className="text-xs text-gray-400 mt-1">
                    {category.expenseCount} مصروف •{" "}
                    {category.percentage.toFixed(1)}%
                  </p>
                </div>
              ))
            ) : (
              <p className="text-gray-400 text-center py-4">لا توجد مصروفات</p>
            )}

            {report?.totalExpenses && report.totalExpenses > 0 && (
              <div className="border-t pt-3 mt-3">
                <div className="flex justify-between items-center">
                  <span className="font-bold text-gray-800">
                    إجمالي المصروفات
                  </span>
                  <span className="font-bold text-red-600">
                    {formatCurrency(report.totalExpenses)}
                  </span>
                </div>
              </div>
            )}
          </div>
        </Card>
      </div>

      {/* Additional Metrics */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">مؤشرات إضافية</h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div>
            <p className="text-sm text-gray-500 mb-1">عدد الطلبات</p>
            <p className="text-2xl font-bold text-gray-800">
              {report?.totalOrders || 0}
            </p>
          </div>
          <div>
            <p className="text-sm text-gray-500 mb-1">متوسط قيمة الطلب</p>
            <p className="text-2xl font-bold text-gray-800">
              {formatCurrency(report?.averageOrderValue || 0)}
            </p>
          </div>
          <div>
            <p className="text-sm text-gray-500 mb-1">المرتجعات</p>
            <p className="text-2xl font-bold text-red-600">
              {formatCurrency(report?.refundsAmount || 0)}
            </p>
          </div>
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
                • <strong>الربح الإجمالي:</strong> الإيرادات - تكلفة البضاعة
                المباعة.
              </li>
              <li>
                • <strong>صافي الربح:</strong> الربح الإجمالي - المصروفات
                التشغيلية - المرتجعات.
              </li>
              <li>
                • <strong>هامش الربح:</strong> (صافي الربح ÷ الإيرادات) × 100 =
                النسبة المئوية للربح.
              </li>
              <li>
                • <strong>نصيحة:</strong> هامش الربح الصحي يجب أن يكون أعلى من
                20% لضمان استدامة المشروع.
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default ProfitLossReportPage;
