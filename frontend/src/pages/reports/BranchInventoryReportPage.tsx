import { useState } from "react";
import {
  Package,
  AlertTriangle,
  TrendingDown,
  Download,
  Loader2,
  AlertCircle,
  Filter,
  ChevronDown,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDateTimeFull } from "@/utils/formatters";
import { useGetBranchInventoryReportQuery } from "@/api/inventoryReportsApi";
import { useGetCategoriesQuery } from "@/api/categoriesApi";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentUser } from "@/store/slices/authSlice";

export const BranchInventoryReportPage = () => {
  const currentUser = useAppSelector(selectCurrentUser);
  const branchId = currentUser?.branchId || 0;

  const [categoryId, setCategoryId] = useState<number | undefined>(undefined);
  const [lowStockOnly, setLowStockOnly] = useState(false);

  const { data: categoriesData } = useGetCategoriesQuery();
  const { data, isLoading, isError, error } = useGetBranchInventoryReportQuery({
    branchId,
    categoryId,
    lowStockOnly,
  });

  const report = data?.data;
  const categories = categoriesData?.data || [];

  const handleExport = () => {
    window.open(
      `/api/inventory-reports/branch/${branchId}/export?${categoryId ? `categoryId=${categoryId}&` : ""}${lowStockOnly ? "lowStockOnly=true" : ""}`,
      "_blank",
    );
  };

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
            تقرير مخزون الفرع
          </h1>
          <p className="text-gray-500 mt-1">{report?.branchName || "الفرع"}</p>
        </div>
        <button
          onClick={handleExport}
          className="flex items-center gap-2 px-4 py-2 bg-success-500 text-white rounded-lg hover:bg-success-600 transition-colors"
        >
          <Download className="w-4 h-4" />
          تصدير CSV
        </button>
      </div>

      {/* Filters */}
      <Card>
        <div className="flex items-center gap-4">
          <Filter className="w-5 h-5 text-gray-400" />
          <div className="flex-1 flex items-center gap-4">
            <div className="relative">
              <select
                value={categoryId || ""}
                onChange={(e) =>
                  setCategoryId(
                    e.target.value ? Number(e.target.value) : undefined,
                  )
                }
                className="appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm min-w-[180px]"
              >
                <option value="">جميع الفئات</option>
                {categories.map((cat) => (
                  <option key={cat.id} value={cat.id}>
                    {cat.name}
                  </option>
                ))}
              </select>
              <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
            </div>

            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={lowStockOnly}
                onChange={(e) => setLowStockOnly(e.target.checked)}
                className="w-4 h-4 text-primary-600 rounded focus:ring-primary-500"
              />
              <span className="text-sm text-gray-700">المخزون المنخفض فقط</span>
            </label>
          </div>
        </div>
      </Card>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
              <Package className="w-5 h-5 text-blue-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">إجمالي المنتجات</p>
              <p className="text-2xl font-bold text-gray-800">
                {report?.totalProducts || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
              <Package className="w-5 h-5 text-green-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">إجمالي الكمية</p>
              <p className="text-2xl font-bold text-gray-800">
                {report?.totalQuantity || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-orange-100 rounded-lg flex items-center justify-center">
              <AlertTriangle className="w-5 h-5 text-orange-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">مخزون منخفض</p>
              <p className="text-2xl font-bold text-orange-600">
                {report?.lowStockCount || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-purple-100 rounded-lg flex items-center justify-center">
              <TrendingDown className="w-5 h-5 text-purple-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">قيمة المخزون</p>
              <p className="text-2xl font-bold text-purple-600">
                {formatCurrency(report?.totalValue || 0)}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Inventory Table */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">تفاصيل المخزون</h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المنتج
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  SKU
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الفئة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الكمية
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  مستوى إعادة الطلب
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الحالة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  متوسط التكلفة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  القيمة الإجمالية
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  آخر تحديث
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.items && report.items.length > 0 ? (
                report.items.map((item) => (
                  <tr
                    key={item.productId}
                    className="border-b hover:bg-gray-50"
                  >
                    <td className="px-4 py-3 font-medium text-gray-800">
                      {item.productName}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {item.productSku || "-"}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {item.categoryName || "-"}
                    </td>
                    <td className="px-4 py-3 font-semibold text-gray-800">
                      {item.quantity}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {item.reorderLevel}
                    </td>
                    <td className="px-4 py-3">
                      {item.isLowStock ? (
                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-orange-100 text-orange-800">
                          <AlertTriangle className="w-3 h-3 ml-1" />
                          مخزون منخفض
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-green-100 text-green-800">
                          متوفر
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {item.averageCost
                        ? formatCurrency(item.averageCost)
                        : "-"}
                    </td>
                    <td className="px-4 py-3 font-semibold text-primary-600">
                      {item.totalValue ? formatCurrency(item.totalValue) : "-"}
                    </td>
                    <td className="px-4 py-3 text-xs text-gray-500">
                      {formatDateTimeFull(item.lastUpdatedAt)}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={9}
                    className="px-4 py-8 text-center text-gray-400"
                  >
                    لا توجد منتجات
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
                • <strong>المخزون:</strong> الكمية الحالية لكل منتج في الفرع
              </li>
              <li>
                • <strong>القيمة:</strong> الكمية × تكلفة الشراء (متوسط مرجح)
              </li>
              <li>
                • <strong>المنخفض:</strong> المنتجات التي وصل مخزونها لحد
                الإنذار المحدد
              </li>
              <li>
                • <strong>الفلتر:</strong> يمكنك التصفية بالفئة أو عرض المنخفض
                فقط
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default BranchInventoryReportPage;
