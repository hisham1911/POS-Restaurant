import { useState } from "react";
import {
  TrendingUp,
  Calendar,
  Loader2,
  AlertCircle,
  DollarSign,
  Package,
  Percent,
  TrendingDown,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency } from "@/utils/formatters";
import { useGetProfitableProductsReportQuery } from "@/api/productReportsApi";

export const ProfitableProductsReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(1)).toISOString().split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);

  const { data, isLoading, isError, error } =
    useGetProfitableProductsReportQuery({ fromDate, toDate });
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
            المنتجات الأكثر ربحية
          </h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "تحليل ربحية المنتجات"}
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
              <DollarSign className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-primary-700 font-medium">
                إجمالي الإيرادات
              </p>
              <p className="text-2xl font-bold text-primary-600">
                {formatCurrency(report?.totalRevenue || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center">
              <Package className="w-6 h-6 text-red-600" />
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
              <p className="text-sm text-gray-500">الربح</p>
              <p className="text-2xl font-bold text-success-600">
                {formatCurrency(report?.totalProfit || 0)}
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
              <p className="text-sm text-gray-500">نسبة الربح</p>
              <p className="text-2xl font-bold text-blue-600">
                {report?.averageProfitMargin || 0}%
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Top Profitable */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4 flex items-center gap-2">
          <TrendingUp className="w-5 h-5 text-green-600" />
          الأكثر ربحية
        </h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  #
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المنتج
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الكمية
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
                  النسبة
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.topProfitableProducts &&
              report.topProfitableProducts.length > 0 ? (
                report.topProfitableProducts.map((product, index) => (
                  <tr
                    key={product.productId}
                    className="border-b hover:bg-gray-50"
                  >
                    <td className="px-4 py-3 text-gray-500">{index + 1}</td>
                    <td className="px-4 py-3">
                      <div>
                        <span className="font-medium text-gray-800">
                          {product.productName}
                        </span>
                        {product.categoryName && (
                          <p className="text-xs text-gray-400">
                            {product.categoryName}
                          </p>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {product.quantitySold}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {formatCurrency(product.revenue)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {formatCurrency(product.cost)}
                    </td>
                    <td className="px-4 py-3 font-semibold text-success-600">
                      {formatCurrency(product.profit)}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={`px-2 py-1 rounded-full text-xs font-medium ${
                          product.profitMargin >= 30
                            ? "text-green-600 bg-green-100"
                            : product.profitMargin >= 15
                              ? "text-blue-600 bg-blue-100"
                              : "text-orange-600 bg-orange-100"
                        }`}
                      >
                        {product.profitMargin}%
                      </span>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={7}
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

      {/* Least Profitable */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4 flex items-center gap-2">
          <TrendingDown className="w-5 h-5 text-red-600" />
          الأقل ربحية
        </h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  #
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المنتج
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الكمية
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
                  النسبة
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.leastProfitableProducts &&
              report.leastProfitableProducts.length > 0 ? (
                report.leastProfitableProducts.map((product, index) => (
                  <tr
                    key={product.productId}
                    className="border-b hover:bg-gray-50"
                  >
                    <td className="px-4 py-3 text-gray-500">{index + 1}</td>
                    <td className="px-4 py-3">
                      <div>
                        <span className="font-medium text-gray-800">
                          {product.productName}
                        </span>
                        {product.categoryName && (
                          <p className="text-xs text-gray-400">
                            {product.categoryName}
                          </p>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {product.quantitySold}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {formatCurrency(product.revenue)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {formatCurrency(product.cost)}
                    </td>
                    <td className="px-4 py-3 font-semibold text-red-600">
                      {formatCurrency(product.profit)}
                    </td>
                    <td className="px-4 py-3">
                      <span className="px-2 py-1 rounded-full text-xs font-medium text-red-600 bg-red-100">
                        {product.profitMargin}%
                      </span>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={7}
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
                • <strong>الربح:</strong> يحسب بناءً على (السعر - التكلفة) ×
                الكمية المباعة
              </li>
              <li>
                • <strong>الإيرادات:</strong> إجمالي قيمة المبيعات قبل حساب
                التكاليف
              </li>
              <li>
                • <strong>التكاليف:</strong> تكلفة البضاعة المباعة فقط
              </li>
              <li>
                • <strong>نسبة الربح:</strong> الربح مقسومة على الإيرادات × 100
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default ProfitableProductsReportPage;
