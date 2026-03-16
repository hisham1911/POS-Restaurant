import { useState } from "react";
import {
  Clock,
  Loader2,
  AlertCircle,
  DollarSign,
  Package,
  AlertTriangle,
  Info,
  ChevronDown,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDateOnly } from "@/utils/formatters";
import { useGetSlowMovingProductsReportQuery } from "@/api/productReportsApi";

export const SlowMovingProductsReportPage = () => {
  const [daysThreshold, setDaysThreshold] = useState(7);

  const { data, isLoading, isError, error } =
    useGetSlowMovingProductsReportQuery({ daysThreshold });
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

  const getStatusColor = (status: string) => {
    switch (status) {
      case "Dead Stock":
        return "text-red-600 bg-red-100";
      case "Very Slow":
        return "text-orange-600 bg-orange-100";
      default:
        return "text-yellow-600 bg-yellow-100";
    }
  };

  return (
    <div className="h-full overflow-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">المنتجات بطيئة الحركة</h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "المنتجات التي لم تُباع مؤخرًا"}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <label htmlFor="days-threshold" className="text-sm font-medium text-gray-700">
            فترة الركود:
          </label>
          <div className="relative">
            <select
              id="days-threshold"
              value={daysThreshold}
              onChange={(e) => setDaysThreshold(Number(e.target.value))}
              className="appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 min-w-[140px] text-gray-700 font-medium shadow-sm"
            >
              <option value={3}>3 أيام</option>
              <option value={7}>7 أيام</option>
              <option value={14}>14 يوم</option>
              <option value={30}>30 يوم</option>
              <option value={60}>60 يوم</option>
            </select>
            <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none transition-transform duration-200" />
          </div>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card className="bg-gradient-to-br from-orange-50 to-orange-100 border-orange-200">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-orange-500 rounded-xl flex items-center justify-center">
              <Package className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-orange-700 font-medium">منتجات بطيئة</p>
              <p className="text-2xl font-bold text-orange-600">
                {report?.totalSlowMovingProducts || 0}
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
              <p className="text-sm text-gray-500">قيمة المخزون المعرَّض</p>
              <p className="text-2xl font-bold text-red-600">
                {formatCurrency(report?.totalValueAtRisk || 0)}
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
              <p className="text-sm text-gray-500">الكمية المعرَّضة</p>
              <p className="text-2xl font-bold text-yellow-600">
                {report?.totalQuantityAtRisk || 0}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Table */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">تفاصيل المنتجات البطيئة</h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">المنتج</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">آخر بيع</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">المخزون</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">أيام بدون بيع</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">القيمة</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">الحالة</th>
              </tr>
            </thead>
            <tbody>
              {report?.slowMovingProducts && report.slowMovingProducts.length > 0 ? (
                report.slowMovingProducts.map((product) => (
                  <tr key={product.productId} className="border-b hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-orange-100 rounded-full flex items-center justify-center">
                          <Package className="w-5 h-5 text-orange-600" />
                        </div>
                        <div>
                          <span className="font-medium text-gray-800">{product.productName}</span>
                          {product.categoryName && (
                            <p className="text-xs text-gray-400">{product.categoryName}</p>
                          )}
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {product.lastSoldDate ? formatDateOnly(product.lastSoldDate) : "لم يُباع"}
                    </td>
                    <td className="px-4 py-3 font-medium text-gray-800">{product.currentStock}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1">
                        <Clock className="w-4 h-4 text-gray-400" />
                        <span className="font-medium text-gray-800">
                          {product.daysSinceLastSale >= 999 ? "∞" : product.daysSinceLastSale}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 font-semibold text-red-600">
                      {formatCurrency(product.stockValue)}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={`px-2 py-1 rounded-full text-xs font-medium ${getStatusColor(product.movementStatus)}`}
                      >
                        {product.movementStatus === "Dead Stock"
                          ? "مخزون راكد"
                          : product.movementStatus === "Very Slow"
                          ? "بطيء جدًا"
                          : "بطيء"}
                      </span>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-400">
                    لا توجد منتجات بطيئة الحركة
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>

      {/* Classification Info */}
      <Card className="bg-blue-50 border-blue-200">
        <div className="flex items-start gap-3">
          <Info className="w-5 h-5 text-blue-600 mt-0.5 flex-shrink-0" />
          <div className="flex-1">
            <h3 className="text-sm font-semibold text-blue-900 mb-2">
              معايير تصنيف المنتجات البطيئة
            </h3>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-sm">
              <div className="flex items-center gap-2">
                <span className="px-2 py-1 rounded-full bg-yellow-100 text-yellow-700 font-medium text-xs">
                  بطيء
                </span>
                <span className="text-gray-700">من 1 إلى 29 يوم بدون بيع</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="px-2 py-1 rounded-full bg-orange-100 text-orange-700 font-medium text-xs">
                  بطيء جدًا
                </span>
                <span className="text-gray-700">من 30 إلى 89 يوم بدون بيع</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="px-2 py-1 rounded-full bg-red-100 text-red-700 font-medium text-xs">
                  مخزون راكد
                </span>
                <span className="text-gray-700">90 يوم فأكثر بدون بيع</span>
              </div>
            </div>
            <p className="text-xs text-blue-700 mt-2">
              ملاحظة: الحالة ثابتة حسب عدد الأيام الفعلية - تغيير الفلتر يؤثر فقط على المنتجات المعروضة وليس على تصنيفها
            </p>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default SlowMovingProductsReportPage;
