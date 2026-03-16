import { useState } from "react";
import {
  Package,
  Calendar,
  Loader2,
  AlertCircle,
  DollarSign,
  TrendingUp,
  ArrowRightLeft,
  ShoppingBag,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency } from "@/utils/formatters";
import { useGetProductMovementReportQuery } from "@/api/productReportsApi";

export const ProductMovementReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(1)).toISOString().split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);

  const { data, isLoading, isError, error } = useGetProductMovementReportQuery({
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
            تقرير حركة المنتجات
          </h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "تحليل حركة المنتجات"}
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
              <Package className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-primary-700 font-medium">
                إجمالي المنتجات
              </p>
              <p className="text-2xl font-bold text-primary-600">
                {report?.totalProducts || 0}
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
              <p className="text-sm text-gray-500">منتجات مباعة</p>
              <p className="text-2xl font-bold text-green-600">
                {report?.productsSold || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-orange-100 rounded-xl flex items-center justify-center">
              <Package className="w-6 h-6 text-orange-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">لم تُباع</p>
              <p className="text-2xl font-bold text-orange-600">
                {report?.productsNotSold || 0}
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
      </div>

      {/* Table */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          تفاصيل حركة المنتجات
        </h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المنتج
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المباع
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المشترى
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  محوّل
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الرصيد
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الإيرادات
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.productMovements &&
              report.productMovements.length > 0 ? (
                report.productMovements.map((product) => (
                  <tr
                    key={product.productId}
                    className="border-b hover:bg-gray-50"
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-purple-100 rounded-full flex items-center justify-center">
                          <Package className="w-5 h-5 text-purple-600" />
                        </div>
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
                      </div>
                    </td>
                    <td className="px-4 py-3 font-medium text-gray-800">
                      {product.quantitySold}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {product.purchasedQuantity}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1">
                        <ArrowRightLeft className="w-4 h-4 text-gray-400" />
                        <span className="text-green-600">
                          +{product.transferredIn}
                        </span>
                        <span className="text-red-600">
                          -{product.transferredOut}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 font-medium text-gray-800">
                      {product.closingStock}
                    </td>
                    <td className="px-4 py-3 font-semibold text-success-600">
                      {formatCurrency(product.totalRevenue)}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={6}
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
                • <strong>حركة المنتج:</strong> الكميات المباعة والمتبقية في
                المخزون
              </li>
              <li>
                • <strong>معدل الدوران:</strong> كم مرة تم بيع كمية المخزون
                الحالية خلال الفترة
              </li>
              <li>
                • <strong>القيمة:</strong> حاصل ضرب الكمية × السعر
              </li>
              <li>
                • <strong>الاتجاه:</strong> التصاعد أو التنازل في مستويات
                المخزون
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default ProductMovementReportPage;
