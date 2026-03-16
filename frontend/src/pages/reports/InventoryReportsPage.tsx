import { useState } from "react";
import {
  Package,
  AlertTriangle,
  TrendingDown,
  Download,
  Filter,
  Loader2,
  AlertCircle,
  ChevronDown,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDate } from "@/utils/formatters";
import { useGetBranchInventoryReportQuery } from "@/api/inventoryReportsApi";
import { useGetBranchesQuery } from "@/api/branchesApi";
import { useGetCategoriesQuery } from "@/api/categoriesApi";

export const InventoryReportsPage = () => {
  const { data: branchesData } = useGetBranchesQuery();
  const branches = branchesData?.data || [];

  const { data: categoriesData } = useGetCategoriesQuery();
  const categories = categoriesData?.data || [];

  const [selectedBranchId, setSelectedBranchId] = useState<number>(
    branches[0]?.id || 0,
  );
  const [selectedCategoryId, setSelectedCategoryId] = useState<
    number | undefined
  >();
  const [lowStockOnly, setLowStockOnly] = useState(false);

  const { data, isLoading, isError, error } = useGetBranchInventoryReportQuery(
    {
      branchId: selectedBranchId,
      categoryId: selectedCategoryId,
      lowStockOnly,
    },
    { skip: !selectedBranchId },
  );

  const report = data?.data;

  if (!selectedBranchId && branches.length > 0) {
    setSelectedBranchId(branches[0].id);
  }

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

  const handleExport = () => {
    const url = `/api/inventory-reports/branch/${selectedBranchId}/export?${
      selectedCategoryId ? `categoryId=${selectedCategoryId}&` : ""
    }${lowStockOnly ? "lowStockOnly=true" : ""}`;
    window.open(url, "_blank");
  };

  return (
    <div className="h-full overflow-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">تقرير المخزون</h1>
          <p className="text-gray-500 mt-1">حالة المخزون حسب الفرع</p>
        </div>
        <button
          onClick={handleExport}
          className="flex items-center gap-2 px-4 py-2 bg-primary-500 text-white rounded-lg hover:bg-primary-600 transition-colors"
        >
          <Download className="w-4 h-4" />
          تصدير CSV
        </button>
      </div>

      {/* Filters */}
      <Card>
        <div className="flex items-center gap-4 flex-wrap">
          <div className="flex items-center gap-2">
            <Filter className="w-5 h-5 text-gray-400" />
            <span className="text-sm font-medium text-gray-700">الفلاتر:</span>
          </div>

          <div className="relative">
            <select
              value={selectedBranchId}
              onChange={(e) => setSelectedBranchId(Number(e.target.value))}
              className="appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm min-w-[180px]"
            >
              {branches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </select>
            <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
          </div>

          <div className="relative">
            <select
              value={selectedCategoryId || ""}
              onChange={(e) =>
                setSelectedCategoryId(
                  e.target.value ? Number(e.target.value) : undefined,
                )
              }
              className="appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm min-w-[180px]"
            >
              <option value="">كل الفئات</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
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
              className="w-4 h-4 text-primary-600 border-gray-300 rounded focus:ring-primary-500"
            />
            <span className="text-sm text-gray-700">المخزون المنخفض فقط</span>
          </label>
        </div>
      </Card>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-primary-100 rounded-xl flex items-center justify-center">
              <Package className="w-6 h-6 text-primary-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">إجمالي المنتجات</p>
              <p className="text-2xl font-bold text-gray-800">
                {report?.totalProducts || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-50 rounded-xl flex items-center justify-center">
              <TrendingDown className="w-6 h-6 text-blue-500" />
            </div>
            <div>
              <p className="text-sm text-gray-500">إجمالي الكمية</p>
              <p className="text-2xl font-bold text-gray-800">
                {report?.totalQuantity || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-warning-50 rounded-xl flex items-center justify-center">
              <AlertTriangle className="w-6 h-6 text-warning-500" />
            </div>
            <div>
              <p className="text-sm text-gray-500">مخزون منخفض</p>
              <p className="text-2xl font-bold text-warning-600">
                {report?.lowStockCount || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-success-50 rounded-xl flex items-center justify-center">
              <Package className="w-6 h-6 text-success-500" />
            </div>
            <div>
              <p className="text-sm text-gray-500">قيمة المخزون</p>
              <p className="text-2xl font-bold text-success-600">
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
                  الفئة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الكمية
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  حد الطلب
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الحالة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  القيمة
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
                    className={`border-b hover:bg-gray-50 ${
                      item.isLowStock ? "bg-warning-50" : ""
                    }`}
                  >
                    <td className="px-4 py-3">
                      <div>
                        <p className="font-medium text-gray-800">
                          {item.productName}
                        </p>
                        {item.productSku && (
                          <p className="text-xs text-gray-500">
                            {item.productSku}
                          </p>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {item.categoryName || "-"}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={`font-semibold ${
                          item.isLowStock ? "text-warning-600" : "text-gray-800"
                        }`}
                      >
                        {item.quantity}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {item.reorderLevel}
                    </td>
                    <td className="px-4 py-3">
                      {item.isLowStock ? (
                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-warning-100 text-warning-800">
                          <AlertTriangle className="w-3 h-3 ml-1" />
                          منخفض
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-success-100 text-success-800">
                          متوفر
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 font-medium text-gray-800">
                      {formatCurrency(item.totalValue || 0)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">
                      {formatDate(item.lastUpdatedAt)}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={7}
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
                • <strong>المخزون:</strong> الكمية المتوفرة في كل فرع حسب الفلتر
              </li>
              <li>
                • <strong>القيمة:</strong> إجمالي قيمة المخزون بسعر التكلفة
              </li>
              <li>
                • <strong>المنخفض:</strong> يمكنك تصفية المنتجات التي تحتاج
                إعادة طلب
              </li>
              <li>
                • <strong>التصدير:</strong> يمكنك تصدير البيانات لملف إكسل
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default InventoryReportsPage;
