import { useState } from "react";
import {
  Calculator,
  Calendar,
  Loader2,
  AlertCircle,
  DollarSign,
  TrendingUp,
  Package,
  Percent,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency } from "@/utils/formatters";
import { useGetCogsReportQuery } from "@/api/productReportsApi";

export const CogsReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(1)).toISOString().split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);

  const { data, isLoading, isError, error } = useGetCogsReportQuery({
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
          <h1 className="text-2xl font-bold text-gray-800">
            تكلفة البضاعة المباعة (COGS)
          </h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "تحليل تكلفة المبيعات"}
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

      {/* COGS Summary */}
      <Card className="bg-gradient-to-br from-blue-50 to-blue-100 border-blue-200">
        <div className="p-4">
          <h3 className="text-lg font-bold text-blue-800 mb-4 flex items-center gap-2">
            <Calculator className="w-5 h-5" />
            حساب تكلفة البضاعة المباعة
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="text-center p-4 bg-white rounded-xl">
              <p className="text-sm text-gray-500 mb-1">مخزون أول المدة</p>
              <p className="text-xl font-bold text-gray-800">
                {formatCurrency(report?.openingInventoryValue || 0)}
              </p>
            </div>
            <div className="text-center p-4 bg-white rounded-xl">
              <p className="text-sm text-gray-500 mb-1">+ المشتريات</p>
              <p className="text-xl font-bold text-blue-600">
                {formatCurrency(report?.totalPurchases || 0)}
              </p>
            </div>
            <div className="text-center p-4 bg-white rounded-xl">
              <p className="text-sm text-gray-500 mb-1">- مخزون آخر المدة</p>
              <p className="text-xl font-bold text-gray-800">
                {formatCurrency(report?.closingInventoryValue || 0)}
              </p>
            </div>
          </div>
          <div className="mt-4 text-center p-4 bg-white rounded-xl border-2 border-blue-300">
            <p className="text-sm text-gray-500 mb-1">
              = تكلفة البضاعة المباعة
            </p>
            <p className="text-3xl font-bold text-blue-700">
              {formatCurrency(report?.costOfGoodsSold || 0)}
            </p>
          </div>
        </div>
      </Card>

      {/* Financial Summary */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-primary-100 rounded-xl flex items-center justify-center">
              <DollarSign className="w-6 h-6 text-primary-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">إجمالي المبيعات</p>
              <p className="text-2xl font-bold text-primary-600">
                {formatCurrency(report?.totalRevenue || 0)}
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
              <p className="text-sm text-gray-500">الربح الإجمالي</p>
              <p className="text-2xl font-bold text-success-600">
                {formatCurrency(report?.grossProfit || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <Percent className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">هامش الربح الإجمالي</p>
              <p className="text-2xl font-bold text-blue-600">
                {report?.grossProfitMargin || 0}%
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Category Breakdown */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          التفاصيل حسب التصنيف
        </h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  التصنيف
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الإيرادات
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  التكلفة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الربح
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  هامش الربح
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.categoryBreakdown &&
              report.categoryBreakdown.length > 0 ? (
                report.categoryBreakdown.map((cat) => (
                  <tr
                    key={cat.categoryId}
                    className="border-b hover:bg-gray-50"
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-purple-100 rounded-full flex items-center justify-center">
                          <Package className="w-5 h-5 text-purple-600" />
                        </div>
                        <span className="font-medium text-gray-800">
                          {cat.categoryName}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {formatCurrency(cat.revenue)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {formatCurrency(cat.cogs)}
                    </td>
                    <td className="px-4 py-3 font-semibold text-success-600">
                      {formatCurrency(cat.grossProfit)}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={`px-2 py-1 rounded-full text-xs font-medium ${
                          cat.grossProfitMargin >= 30
                            ? "text-green-600 bg-green-100"
                            : cat.grossProfitMargin >= 15
                              ? "text-blue-600 bg-blue-100"
                              : "text-orange-600 bg-orange-100"
                        }`}
                      >
                        {cat.grossProfitMargin}%
                      </span>
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
                • <strong>تكلفة البضاعة المباعة (COGS):</strong> تكلفة المنتجات
                المباعة فقط
              </li>
              <li>
                • <strong>المعادلة:</strong> مخزون أول + المشتريات - مخزون آخر
              </li>
              <li>
                • <strong>تشمل:</strong> تكلفة الإنتاج والشراء فقط، لا تشمل
                مصاريف تشغيلية
              </li>
              <li>
                • <strong>الهدف:</strong> حساب الربح الإجمالي بدقة (المبيعات -
                COGS)
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default CogsReportPage;
