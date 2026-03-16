import { useState } from "react";
import {
  AlertTriangle,
  Package,
  Building2,
  DollarSign,
  Loader2,
  AlertCircle,
  ChevronDown,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency } from "@/utils/formatters";
import { useGetLowStockSummaryReportQuery } from "@/api/inventoryReportsApi";
import { useGetBranchesQuery } from "@/api/branchesApi";

export const LowStockSummaryReportPage = () => {
  const [branchId, setBranchId] = useState<number | undefined>(undefined);

  const { data: branchesData } = useGetBranchesQuery();
  const { data, isLoading, isError, error } = useGetLowStockSummaryReportQuery({
    branchId,
  });

  const report = data?.data;
  const branches = branchesData?.data || [];

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
            تقرير المخزون المنخفض
          </h1>
          <p className="text-gray-500 mt-1">المنتجات التي تحتاج إعادة تخزين</p>
        </div>
        <div className="relative">
          <select
            value={branchId || ""}
            onChange={(e) =>
              setBranchId(e.target.value ? Number(e.target.value) : undefined)
            }
            className="appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm min-w-[180px]"
          >
            <option value="">جميع الفروع</option>
            {branches.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </select>
          <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card className="bg-gradient-to-br from-orange-50 to-orange-100 border-orange-200">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-orange-500 rounded-xl flex items-center justify-center">
              <AlertTriangle className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-orange-700 font-medium">
                منتجات منخفضة
              </p>
              <p className="text-2xl font-bold text-orange-600">
                {report?.totalLowStockItems || 0}
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
              <p className="text-sm text-gray-500">منتجات حرجة</p>
              <p className="text-2xl font-bold text-red-600">
                {report?.criticalItems || 0}
              </p>
              <p className="text-xs text-gray-400">كمية = 0</p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <Building2 className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">فروع متأثرة</p>
              <p className="text-2xl font-bold text-gray-800">
                {report?.affectedBranches || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-purple-100 rounded-xl flex items-center justify-center">
              <DollarSign className="w-6 h-6 text-purple-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">قيمة إعادة التخزين</p>
              <p className="text-2xl font-bold text-purple-600">
                {formatCurrency(report?.estimatedRestockValue || 0)}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Low Stock Items */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          المنتجات منخفضة المخزون
        </h3>
        <div className="space-y-4">
          {report?.items && report.items.length > 0 ? (
            report.items.map((item) => (
              <div
                key={item.productId}
                className="border border-gray-200 rounded-lg p-4 hover:bg-gray-50"
              >
                <div className="flex items-start justify-between mb-3">
                  <div className="flex-1">
                    <div className="flex items-center gap-2 mb-1">
                      <h4 className="font-semibold text-gray-800">
                        {item.productName}
                      </h4>
                      {item.productSku && (
                        <span className="text-xs text-gray-500">
                          ({item.productSku})
                        </span>
                      )}
                    </div>
                    {item.categoryName && (
                      <p className="text-sm text-gray-500">
                        {item.categoryName}
                      </p>
                    )}
                  </div>
                  <div className="text-left">
                    <p className="text-sm text-gray-500">النقص</p>
                    <p className="text-xl font-bold text-orange-600">
                      {item.shortage}
                    </p>
                  </div>
                </div>

                <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-3">
                  <div>
                    <p className="text-xs text-gray-500">الكمية الحالية</p>
                    <p className="font-semibold text-gray-800">
                      {item.totalQuantity}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">الحد الأدنى</p>
                    <p className="font-semibold text-gray-800">
                      {item.totalReorderLevel}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">متوسط التكلفة</p>
                    <p className="font-semibold text-gray-800">
                      {item.averageCost
                        ? formatCurrency(item.averageCost)
                        : "-"}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">تكلفة إعادة التخزين</p>
                    <p className="font-semibold text-purple-600">
                      {item.estimatedRestockCost
                        ? formatCurrency(item.estimatedRestockCost)
                        : "-"}
                    </p>
                  </div>
                </div>

                {/* Branch Details */}
                <div className="border-t pt-3">
                  <p className="text-xs text-gray-500 mb-2">تفاصيل الفروع:</p>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                    {item.branchDetails.map((branch) => (
                      <div
                        key={branch.branchId}
                        className={`p-2 rounded text-xs ${
                          branch.isCritical
                            ? "bg-red-50 border border-red-200"
                            : "bg-orange-50 border border-orange-200"
                        }`}
                      >
                        <div className="flex justify-between items-center">
                          <span className="font-medium">
                            {branch.branchName}
                          </span>
                          <span
                            className={
                              branch.isCritical
                                ? "text-red-600"
                                : "text-orange-600"
                            }
                          >
                            {branch.quantity} / {branch.reorderLevel}
                          </span>
                        </div>
                        <p
                          className={
                            branch.isCritical
                              ? "text-red-600"
                              : "text-orange-600"
                          }
                        >
                          {branch.isCritical ? "⚠️ حرج" : "نقص"}:{" "}
                          {branch.shortage}
                        </p>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            ))
          ) : (
            <p className="text-gray-400 text-center py-8">
              لا توجد منتجات منخفضة المخزون 🎉
            </p>
          )}
        </div>
      </Card>

      {/* Branch Statistics */}
      {report?.branchStats && report.branchStats.length > 0 && (
        <Card>
          <h3 className="text-lg font-bold text-gray-800 mb-4">
            إحصائيات الفروع
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {report.branchStats.map((branch) => (
              <div
                key={branch.branchId}
                className="border border-gray-200 rounded-lg p-4"
              >
                <h4 className="font-semibold text-gray-800 mb-3">
                  {branch.branchName}
                </h4>
                <div className="space-y-2">
                  <div className="flex justify-between">
                    <span className="text-sm text-gray-600">مخزون منخفض:</span>
                    <span className="font-semibold text-orange-600">
                      {branch.lowStockCount}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-sm text-gray-600">حرج:</span>
                    <span className="font-semibold text-red-600">
                      {branch.criticalCount}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-sm text-gray-600">
                      قيمة إعادة التخزين:
                    </span>
                    <span className="font-semibold text-purple-600">
                      {formatCurrency(branch.estimatedRestockValue)}
                    </span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </Card>
      )}

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
                • <strong>المنخفض:</strong> المنتجات التي وصل مخزونها أقل من حد
                الإنذار
              </li>
              <li>
                • <strong>حد الإنذار:</strong> يحدد لكل منتج على حدة من إعدادات
                المنتج
              </li>
              <li>
                • <strong>قيمة النقص:</strong> تكلفة إعادة تعبئة المخزون للحد
                الأدنى
              </li>
              <li>
                • <strong>الإجراء:</strong> راجع المنتجات الحمراء واطلب إعادة
                تخزين فوري
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default LowStockSummaryReportPage;
