import { useState } from "react";
import { useGetLowStockItemsQuery } from "../../api/inventoryApi";
import { useAppSelector } from "../../store/hooks";
import { selectCurrentBranch, selectBranches } from "../../store/slices/branchSlice";
import { AlertTriangle, Package, Filter, ChevronDown } from "lucide-react";

export default function LowStockAlerts() {
  const currentBranch = useAppSelector(selectCurrentBranch);
  const branches = useAppSelector(selectBranches);
  const [selectedBranchId, setSelectedBranchId] = useState<number | undefined>(
    currentBranch?.id
  );

  const {
    data: lowStockItems,
    isLoading,
    error,
  } = useGetLowStockItemsQuery(selectedBranchId);

  // Group by branch
  const groupedByBranch = lowStockItems?.reduce((acc, item) => {
    if (!acc[item.branchId]) {
      acc[item.branchId] = {
        branchName: item.branchName,
        items: [],
      };
    }
    acc[item.branchId].items.push(item);
    return acc;
  }, {} as Record<number, { branchName: string; items: typeof lowStockItems }>);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <AlertTriangle className="w-12 h-12 text-red-500 mx-auto mb-4" />
          <p className="text-red-600">حدث خطأ في تحميل التنبيهات</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">تنبيهات المخزون المنخفض</h2>
          <p className="text-sm text-gray-600 mt-1">
            المنتجات التي وصلت إلى حد إعادة الطلب
          </p>
        </div>

        {/* Branch Filter */}
        <div className="flex items-center gap-2">
          <Filter className="w-5 h-5 text-gray-400" />
          <div className="relative">
            <select
              value={selectedBranchId || "all"}
              onChange={(e) =>
                setSelectedBranchId(
                  e.target.value === "all" ? undefined : Number(e.target.value)
                )
              }
              className="appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm min-w-[180px]"
            >
              <option value="all">جميع الفروع</option>
              {branches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </select>
            <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
          </div>
        </div>
      </div>

      {/* Alert Summary */}
      <div className="bg-red-50 border border-red-200 rounded-lg p-4">
        <div className="flex items-center gap-3">
          <AlertTriangle className="w-8 h-8 text-red-600" />
          <div>
            <p className="text-lg font-semibold text-red-900">
              {lowStockItems?.length || 0} منتج يحتاج إعادة طلب
            </p>
            <p className="text-sm text-red-700">
              {selectedBranchId
                ? `في فرع ${currentBranch?.name}`
                : `عبر ${Object.keys(groupedByBranch || {}).length} فرع`}
            </p>
          </div>
        </div>
      </div>

      {/* Low Stock Items */}
      {lowStockItems && lowStockItems.length > 0 ? (
        <div className="space-y-4">
          {selectedBranchId ? (
            // Single branch view
            <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead className="bg-gray-50 border-b border-gray-200">
                    <tr>
                      <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                        المنتج
                      </th>
                      <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                        الكمية المتاحة
                      </th>
                      <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                        حد إعادة الطلب
                      </th>
                      <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                        النقص
                      </th>
                    </tr>
                  </thead>
                  <tbody className="bg-white divide-y divide-gray-200">
                    {lowStockItems.map((item) => (
                      <tr key={item.id} className="hover:bg-gray-50">
                        <td className="px-6 py-4">
                          <div className="text-sm font-medium text-gray-900">
                            {item.productName}
                          </div>
                          {item.productSku && (
                            <div className="text-xs text-gray-500">
                              كود: {item.productSku}
                            </div>
                          )}
                        </td>
                        <td className="px-6 py-4">
                          <span className="text-sm font-semibold text-red-600">
                            {item.quantity}
                          </span>
                        </td>
                        <td className="px-6 py-4">
                          <span className="text-sm text-gray-900">
                            {item.reorderLevel}
                          </span>
                        </td>
                        <td className="px-6 py-4">
                          <span className="text-sm font-semibold text-orange-600">
                            {Math.max(0, item.reorderLevel - item.quantity)}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : (
            // Multi-branch view
            <div className="space-y-4">
              {Object.entries(groupedByBranch || {}).map(([branchId, data]) => (
                <div
                  key={branchId}
                  className="bg-white rounded-lg border border-gray-200 overflow-hidden"
                >
                  <div className="bg-gray-50 px-6 py-3 border-b border-gray-200">
                    <h3 className="text-lg font-semibold text-gray-900">
                      {data.branchName}
                    </h3>
                    <p className="text-sm text-gray-600">
                      {data.items.length} منتج منخفض
                    </p>
                  </div>
                  <div className="overflow-x-auto">
                    <table className="w-full">
                      <thead className="bg-gray-50 border-b border-gray-200">
                        <tr>
                          <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                            المنتج
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                            الكمية المتاحة
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                            حد إعادة الطلب
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                            النقص
                          </th>
                        </tr>
                      </thead>
                      <tbody className="bg-white divide-y divide-gray-200">
                        {data.items.map((item) => (
                          <tr key={item.id} className="hover:bg-gray-50">
                            <td className="px-6 py-4">
                              <div className="text-sm font-medium text-gray-900">
                                {item.productName}
                              </div>
                              {item.productSku && (
                                <div className="text-xs text-gray-500">
                                  كود: {item.productSku}
                                </div>
                              )}
                            </td>
                            <td className="px-6 py-4">
                              <span className="text-sm font-semibold text-red-600">
                                {item.quantity}
                              </span>
                            </td>
                            <td className="px-6 py-4">
                              <span className="text-sm text-gray-900">
                                {item.reorderLevel}
                              </span>
                            </td>
                            <td className="px-6 py-4">
                              <span className="text-sm font-semibold text-orange-600">
                                {Math.max(0, item.reorderLevel - item.quantity)}
                              </span>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      ) : (
        <div className="bg-white rounded-lg border border-gray-200 p-12">
          <div className="text-center">
            <Package className="w-16 h-16 text-green-500 mx-auto mb-4" />
            <h3 className="text-lg font-semibold text-gray-900 mb-2">
              لا توجد تنبيهات
            </h3>
            <p className="text-gray-600">
              جميع المنتجات فوق حد إعادة الطلب
            </p>
          </div>
        </div>
      )}
    </div>
  );
}
